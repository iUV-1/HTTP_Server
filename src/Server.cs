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
                byte[] responseBuffer = new byte[1024];

                int bytesRead =
                    await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length); // Receive packets from client
                var lines = Encoding.UTF8.GetString(responseBuffer)
                    .Split("\r\n"); // Split the package according to CRLF line break
                // Status line
                var line0 = lines[0].Split(" "); // Split the first line of the package by space 
                var (method, path, httpVer) = (line0[0], line0[1], line0[2]);

                // Headers
                // Split the headers into appropriate variables. If that properties doesn't exist then it is the default value (empty string)
                string host = "", userAgent = "", accept = "";
                for (int i = 1; i < lines.Length; i++)
                {
                    var header = lines[i].Split(":");
                    switch (header[0].ToLower())
                    {
                        case "host":
                            host = header[1];
                            break;
                        case "user-agent":
                            userAgent = header[1];
                            break;
                        case "accept":
                            accept = header[1];
                            break;
                        default:
                            break;
                    }
                }

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
                    response = $"{httpVer} 200 OK\r\n\r\n";
                }
                else if (splittedPath.Length >= 2)
                {
                    switch (splittedPath[1])
                    {
                        case "user-agent":
                            string content = userAgent.Trim();

                            string status = $"{httpVer} 200 OK";
                            string contentType = "Content-Type: text/plain";
                            string contentLength = $"Content-Length: {content.Length.ToString()}";
                            response = $"{status}\r\n{contentType}\r\n{contentLength}\r\n\r\n{content}";
                            break;

                        case "echo":
                            response =
                                $"{httpVer} 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {splittedPath[2].Length.ToString()}\r\n\r\n{splittedPath[2]}\n";
                            break;
                        default:
                            response = $"{httpVer} 404 Not Found\r\n\r\n";
                            break;
                    }
                }
                else
                {
                    response = $"{httpVer} 404 Not Found\r\n\r\n";
                }

                Console.WriteLine("//Response");
                Console.WriteLine(response); // Print the response
                await stream.WriteAsync(Encoding.UTF8.GetBytes(response)); // Serialize the response and send it.

                Console.WriteLine("Response sent");
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

}

// Uncomment this block to pass the first stage
