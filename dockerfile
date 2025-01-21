# Use a lightweight .NET runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .

# Restore dependencies
RUN dotnet restore "codecrafters-http-server.csproj"

# Build the project
RUN dotnet publish "codecrafters-http-server.csproj" -c Release -o /app/publish

# Final stage: production image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose the port your HTTP server listens on
EXPOSE 4221

# Define the entry point for the container
ENTRYPOINT ["dotnet", "codecrafters-http-server.dll", "-p", "4221", "-d", "/app/data"]