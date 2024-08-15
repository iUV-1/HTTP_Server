## Overview
Custom HTTP server built from scratch using C# .NET. 
By default this program listen to port 4221, 
This project uses .NET 8.0. Make sure to install it before using it.

## Features
- File Upload and Download with endpoint /files/
- Gzip Encoding by specifying Accept-Encoding: gzip header
- Mulitple concurrent users.

## Installation
1. Clone the Repository:
   ```bash
   git clone https://github.com/iUV-1/HTTP_Server.git
   ```
2. Build the project:
   ```bash
   dotnet build
   ```
3. Run the project:
   ```bash
   dotnet run
   ```

## Configuration
Two arguments can be passed to this project:
-d or --directory: Set the directory for receving and transfering files
-p or --port: Set port number to use.
