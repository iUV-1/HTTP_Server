using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        TcpListener server = new TcpListener(IPAddress.Any, 4221);
        server.Start();
        while (true)
        {
            var cilent = await server.AcceptTcpClientAsync();
            Console.WriteLine("Cilent connected");
            // discard the result of HandleCilentAsync
            _ = HandleCilentAsync(cilent);
        }
    }

    static async Task HandleCilentAsync(TcpClient cilent)
    {
        try
        {
            await using (NetworkStream stream = cilent.GetStream())
            {
                const int BUFFER_SIZE = 1024;
                const string HOST = "host";
                const string USER_AGENT = "user-agent";
                const string ACCEPT = "accept";
                
                Dictionary<string, string> headers = new Dictionary<string, string>();
                
                byte[] responseBuffer = new byte[BUFFER_SIZE];

                int bytesRead =
                    await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length); // Receive packets from client
                var lines = Encoding.UTF8.GetString(responseBuffer)
                    .Split("\r\n"); // Split the package according to CRLF line break
                
                // Status 
                var line0 = lines[0].Split(" "); // Split the first line of the package by space 
                var (method, path, httpVer) = (line0[0], line0[1], line0[2]);

                // Headers
                // Split the headers into appropriate variables. If that properties doesn't exist then it is the default value (empty string)
                // lines.Length - 2 is a cheap hack because the last 2 lines of what I splitted is nonsense (One is an empty line and the other is a null line?)
                // TODO: Look into why this happen and come up with a proper fix.
                for (int i = 1; i < lines.Length - 2; i++)
                {
                    var header = lines[i].Split(":");
                    headers[header[0].ToLower()] = header[1];
                }

                string host = headers[HOST];
                string userAgent = headers[USER_AGENT];
                string accept = "";

                // Split the path
                var splittedPath = path.Split("/");
                // Debug logging
                Console.WriteLine("//Status line");
                Console.WriteLine("method: " + method + "\n" + "path: " + path + "\n" + "httpVer: " +
                                  httpVer); // Print the method, path and HTTP version
                Console.WriteLine("//Splitted path");
                foreach (var value in splittedPath)
                {
                    Console.WriteLine(value);
                }

                string response;
                if (path == "/")
                {
                    // Check if the request is a GET request and the path is "/"
                    // Includes the HTTP version used by the client 
                    await RespondAsync(status: 200, httpVer: httpVer, stream);
                }
                else if (splittedPath.Length >= 2)
                {
                    string content, contentType, contentLength;
                    switch (splittedPath[1])
                    {
                        case "files":
                            break;

                        case "user-agent":
                            content = userAgent.Trim();
                            contentType = "text/plain";
                            contentLength = content.Length.ToString();

                            await RespondAsync(status: 200, httpVer: httpVer, stream,
                                                content, contentType, contentLength);
                            break;

                        case "echo":
                            content = splittedPath[2];
                            contentType = "text/plain";
                            contentLength = content.Length.ToString();
                            await RespondAsync(status: 200, httpVer: httpVer, stream,
                                content, contentType, contentLength);
                            response =
                                $"{httpVer} 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {splittedPath[2].Length.ToString()}\r\n\r\n{splittedPath[2]}\n";
                            break;
                        default:
                            response = $"{httpVer} 404 Not Found\r\n\r\n";
                            await RespondAsync(status: 404, httpVer: httpVer, stream);
                            break;
                    }
                }
                else
                {
                    response = $"{httpVer} 404 Not Found\r\n\r\n";
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
        string content = "", string contentType = "", string contentLength = "")
    {
        string response;
        switch (status)
        {
            case 200:
                response = $"{httpVer} 200 OK\r\n";

                if (content == "" && contentType == "" && contentLength == "")
                {
                    // No headers needed
                    break;
                }
                response += $"Content-Type: {contentType}\r\n" +
                            $"Content-Length: {contentLength}\r\n\r\n" +
                            $"{content}";
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
        
        Console.WriteLine("//Response");
        Console.WriteLine(response); // Print the response
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response)); // Serialize the response and send it.

        Console.WriteLine("Response sent");
    }
    //static async Task RespondwithFiles(string status, string contentType, )
}
