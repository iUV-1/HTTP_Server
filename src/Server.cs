using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.IO.Compression;

class Program
{
    static async Task Main(string[] args)
    {
        string directory = string.Empty;
        // Parse directory from argument 
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "-d" || args[i] == "--directory") && i + 1 < args.Length)
            {
                directory = args[i + 1];
                break;
            }
        }

        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            Console.WriteLine($"Main: Working with directory: {directory}");
        }
        else
        {
            Console.WriteLine("Main: Directory not specified, using the base folder");
        }
        
        TcpListener server = new TcpListener(IPAddress.Any, 4221);
        server.Start();
        // Constantly waits for a new cilent 
        while (true)
        {
            var cilent = await server.AcceptTcpClientAsync();
            Console.WriteLine("Cilent connected");
            // discard the result of HandleCilentAsync
            _ = HandleCilentAsync(cilent, directory);
        }
    }

    static async Task HandleCilentAsync(TcpClient cilent, string directory)
    {
        try
        {
            await using (NetworkStream stream = cilent.GetStream())
            {
                const int BUFFER_SIZE = 1024;
                const string HOST = "host";
                const string USER_AGENT = "user-agent";
                const string ACCEPT = "accept";
                const string ACCEPT_ENCODING = "accept-encoding";
                
                Dictionary<string, string> headers = new Dictionary<string, string>();
                
                byte[] responseBuffer = new byte[BUFFER_SIZE];

                int bytesRead =
                    await stream.ReadAsync(responseBuffer, 0, BUFFER_SIZE); // Receive packets from client

                byte[] strippedBuffer = StripBuffer(responseBuffer);
                var lines = Encoding.UTF8.GetString(strippedBuffer)
                    .Split("\r\n"); // Split the package according to CRLF line break
                Console.WriteLine("Lines: ");
                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }
                
                // Status 
                var line0 = lines[0].Split(" "); // Split the first line of the package by space 
                var (method, path, httpVer) = (line0[0], line0[1], line0[2]);

                // Headers
                // Split the headers and put them in a dictionary.
                // The last 2 lines are the request body. We don't need it for parsing the header
                Console.WriteLine("//Headers");
                for (int i = 1; i < lines.Length - 2; i++)
                {
                    var header = lines[i].Split(":");
                    header[1] = header[1].TrimStart();
                    Console.WriteLine($"{header[0].ToLower()}:{header[1]}");
                    headers[header[0].ToLower()] = header[1];
                }
                
                // Getting the value into appropriate vars
                // If it doesn't exist then don't throw a fuzz, just leave it empty
                // spoopy
                string host, userAgent, accept, acceptEncoding;
                headers.TryGetValue(HOST, out host);
                headers.TryGetValue(USER_AGENT, out userAgent);
                headers.TryGetValue(ACCEPT, out accept);
                headers.TryGetValue(ACCEPT_ENCODING, out acceptEncoding);
                
                // Request body
                string requestBody = lines[lines.Length - 1];

                // Split the path
                var splittedPath = path.Split("/");
                
                // Debug logging
                Console.WriteLine("//Status line");
                Console.WriteLine("method: " + method + "\n" + "path: " + path + "\n" + "httpVer: " +
                                  httpVer); 
                Console.WriteLine("//Splitted path");
                foreach (var value in splittedPath)
                {
                    Console.WriteLine(value);
                }
                
                if (path == "/" && method == "GET")
                {
                    // Check if the request is a GET request and the path is "/"
                    // Includes the HTTP version used by the client 
                    await RespondAsync(status: 200, httpVer: httpVer, stream);
                }
                else if (splittedPath.Length >= 2)
                {
                    string content, contentType;
                    switch (splittedPath[1])
                    {
                        case "files":
                            string filename = splittedPath[2];
                            // Hacky hacky hack hack
                            // POST: Writefile
                            if (method == "POST")
                            {
                                try
                                {
                                    await using (StreamWriter writer = new StreamWriter($"{directory}/{filename}"))
                                    {
                                        await writer.WriteAsync(requestBody);
                                    }
                                    await RespondAsync(201, httpVer, stream);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"/files/ Writefile: ${ex.Message}");
                                    await RespondAsync(500, httpVer, stream);
                                }
                                break;
                            }
                            // GET: Readfile
                            try
                            {
                                content = await File.ReadAllTextAsync($"{directory}/{filename}");
                                contentType = "application/octet-stream";
                                await RespondwithFilesAsync(httpVer, stream, content, contentType, acceptEncoding: acceptEncoding);
                            }
                            catch (IOException ex)
                            {
                                Console.WriteLine($"/files/ Readfile: {ex.Message}");
                                await RespondAsync(404, httpVer, stream);
                            }
                            break;    

                        case "user-agent":
                            content = userAgent.Trim();
                            contentType = "text/plain";

                            await RespondAsync(status: 200, httpVer: httpVer, stream,
                                                content, contentType, acceptEncoding: acceptEncoding);
                            break;

                        case "echo":
                            content = splittedPath[2];
                            contentType = "text/plain";
                            await RespondAsync(status: 200, httpVer: httpVer, stream,
                                content, contentType, acceptEncoding: acceptEncoding);
                            break;
                        default:
                            await RespondAsync(status: 404, httpVer: httpVer, stream);
                            break;
                    }
                }
                else
                {
                    await RespondAsync(status: 404, httpVer: httpVer, stream);
                }

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HandleCilentAsync: {ex.Message}");
        }
        finally
        {
            cilent.Close();
            Console.WriteLine("Cilent Disconnected.");
        }
    }

    static async Task RespondAsync(int status, string httpVer, NetworkStream stream,
        string content = "", string contentType = "", string contentLength = "", string acceptEncoding = "")
    {
        string response;
        bool encodeWithGzip = false;
        switch (status)
        {
            case 200:
                // Headers
                response = $"{httpVer} 200 OK\r\n";
                if (content == "" && contentType == "" && contentLength == "" && acceptEncoding == "")
                {
                    response += "\r\n";
                    // No headers needed
                    break;
                }
                
                response += $"Content-Type: {contentType}\r\n"; // what is this doing here?
                
                encodeWithGzip = CheckForEncoding(acceptEncoding);

                if (encodeWithGzip)
                {
                    /*// Encode the response with gzip
                    var compressedContent = Compress(content);
                    // Headers
                    response += $"Content-Length: {compressedContent.Length}\r\n";
                    response += "Content-Encoding: gzip\r\n\r\n";
                    // response in this case is the headers.
                    var responseBytes = AppendResponse(response, compressedContent);
                    
                    Console.WriteLine("//Response w/ gzip");
                    Console.WriteLine(Encoding.UTF8.GetString(responseBytes)); 
                    await stream.WriteAsync(responseBytes); // Send the response.
                    Console.WriteLine("Response sent");*/
                    await EncodeWithGzipAsync(content, response, stream);
                }
                else
                {
                    response += $"Content-Length: {content.Length}\r\n";
                    response += "\r\n";
                    response += $"{content}";
                }
                break;
            
            case 201:
                response = $"{httpVer} 201 Created\r\n\r\n";
                break;
            
            case 404:
                response = $"{httpVer} 404 Not Found\r\n\r\n";
                break;
            
            case 500:
                response = $"{httpVer} 500 Internal Server Error\r\n\r\n";
                break;
            
            default:
                response = $"{httpVer} 404 Not Found\r\n\r\n";
                break;
        }

        if (encodeWithGzip)
        {
            return;
        }
        Console.WriteLine("//Response");
        Console.WriteLine(response); // Log the response
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response)); // Serialize the response and send it.

        Console.WriteLine("Response sent");
    }

    // IMPORTANT: This function assume that the response is successful and will return a 200 response
    // If it's an error, please use RespondAsync() instead.
    static async Task RespondwithFilesAsync( string httpVer, NetworkStream stream,
        string content, string contentType = "", string acceptEncoding = "")
    {
        string response;
        bool encodeWithGzip = false;
        
        response = $"{httpVer} 200 OK\r\n"
                   + $"Content-Type: {contentType}\r\n";
        
        encodeWithGzip = CheckForEncoding(acceptEncoding);
        if (encodeWithGzip)
        {
            /*// Encode the response with gzip
            var compressedContent = Compress(content);
            
            // Header
            response += "Content-Encoding: gzip\r\n";
            response += $"Content-Length: {compressedContent.Length}\r\n\r\n";
            
            var responseBytes = AppendResponse(response, compressedContent);

            Console.WriteLine("//Response w/ gzip");
            Console.WriteLine(Encoding.UTF8.GetString(responseBytes)); 
            
            await stream.WriteAsync(responseBytes); // Send the response.
            Console.WriteLine("Response sent");*/

            await EncodeWithGzipAsync(content, response, stream);
        }
        else
        {
            int contentLength = Encoding.UTF8.GetBytes(content).Length;
            response += $"Content-Length: {contentLength}\r\n\r\n";
            response += $"{content}";

            Console.WriteLine("//Response");
            Console.WriteLine(response); // Print the response
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response)); // Serialize the response and send it.

            Console.WriteLine("Response sent");
        }
    }

    // Strip NULL bytes out of a byte array
    static byte[] StripBuffer(byte[] buffer)
    {
        int i = buffer.Length - 1;
        while(buffer[i] == 0)
            --i;
        byte[] strippedBuffer = new byte[i+1];
        Array.Copy(buffer, strippedBuffer, i+1);
        return strippedBuffer;
    }

    static byte[] Compress(string data)
    {
        byte[] compressed = Encoding.UTF8.GetBytes(data);
        using (var compressedStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(compressed, 0, compressed.Length);
            }
            return compressedStream.ToArray();
        }
    }

    static byte[] AppendResponse(string header, byte[] response)
    {
        byte[] encodedHeader = Encoding.UTF8.GetBytes(header);
        byte[] encodedResponse = new byte[encodedHeader.Length + response.Length];
        Buffer.BlockCopy(encodedHeader, 0, encodedResponse, 0, header.Length);
        Buffer.BlockCopy(response, 0, encodedResponse, encodedHeader.Length, response.Length);

        return encodedResponse;
    }

    static bool CheckForEncoding(string acceptEncoding)
    {
        if (!String.IsNullOrEmpty(acceptEncoding) ) // empty
        {
            foreach (var encoding in acceptEncoding.Split(", "))
            {
                if (encoding == "gzip")
                {
                    return true;
                }
            }
        }

        return false;
    }

    static async Task EncodeWithGzipAsync(string content, string response, NetworkStream stream)
    {
        // Encode the response with gzip
        var compressedContent = Compress(content);
            
        // Header
        response += "Content-Encoding: gzip\r\n";
        response += $"Content-Length: {compressedContent.Length}\r\n\r\n";
            
        var responseBytes = AppendResponse(response, compressedContent);

        Console.WriteLine("//Response w/ gzip");
        Console.WriteLine(Encoding.UTF8.GetString(responseBytes)); 
            
        await stream.WriteAsync(responseBytes); // Send the response.
        Console.WriteLine("Response sent");
    }
    
}
