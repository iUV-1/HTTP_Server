using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Socket socket = server.AcceptSocket(); // wait for client
// Lesson 2
// socket.Send(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n\n"));

// Lesson 3
byte[] responseBuffer = new byte[1024];
int bytesRead = socket.Receive(responseBuffer); // Receive packets from client

var lines = Encoding.UTF8.GetString(responseBuffer).Split("\r\n"); // Split the package according to CRLF line break
var line0 = lines[0].Split(" "); // Split the first line of the package by space 
var (method, path, httpVer) = (line0[0], line0[1], line0[2]);
var splittedPath = path.Split("/");
Console.WriteLine("method: " + method + "\n" + "path: " + path + "\n" + "httpVer: " + httpVer); // Print the method, path and HTTP version
foreach(var value in splittedPath)
{
    Console.WriteLine(value);
}
// CN2
//if (path == "/echo")
string response;
if (path == "/")
{
    response = $"{httpVer} 200 OK\r\n\r\n";
}
else
{
    switch (splittedPath[1])
    {
        case "echo":
            response = $"{httpVer} 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {splittedPath[2].Length.ToString()}\r\n\r\n{splittedPath[2]}\n";
            break;
        default:
            response = $"{httpVer} 404 Not Found\r\n\r\n";
            break;
    }
}

// Check if the request is a GET request and the path is "/"
// Includes the HTTP version used by the client
// //var response = path == "/" ? $"{httpVer} 200 OK\r\n\r\n" 
//: $"{httpVer} 404 Not Found\r\n\r\n"; 

Console.WriteLine(response); // Print the response
socket.Send(Encoding.UTF8.GetBytes(response)); // Send the response accordingly
Console.WriteLine("Response sent");

