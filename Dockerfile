# Use the official .NET SDK image as a base image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the solution file
COPY ["LisoLaser.Backend.sln", "."]

# Copy the API project file and its directory structure
COPY ["LisoLaser.Backend.csproj", "."]

# Restore NuGet packages for the API project
RUN dotnet restore "LisoLaser.Backend.csproj"

# Copy all source code
COPY . .

# Change working directory to the API project
WORKDIR "/src"

# Build the application in Release configuration
RUN dotnet publish "LisoLaser.Backend.csproj" -c Release -o /app/publish

# Use the official .NET runtime image as a base image for the final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Copy the published application from the build stage
COPY --from=build /app/publish .

# Set the entry point to run your .NET API application
ENTRYPOINT ["dotnet", "LisoLaser.Backend.dll"]