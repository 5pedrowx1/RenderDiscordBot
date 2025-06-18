# ========================
# STAGE 1: Build
# ========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# (Optional) Copy global.json if it exists
COPY global.json ./

# Copy the project file and restore dependencies
COPY ["RenderDiscordBot/RenderDiscordBot.csproj", "RenderDiscordBot/"]
RUN dotnet restore "RenderDiscordBot/RenderDiscordBot.csproj"

# Copy all the project files
COPY . .

# Set working directory to the project folder and publish the application
WORKDIR "/src/RenderDiscordBot"
RUN dotnet publish "RenderDiscordBot.csproj" -c Release -o /app/publish --no-restore

# ========================
# STAGE 2: Runtime
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Copy the wwwroot things
COPY --from=build /src/RenderDiscordBot/wwwroot ./wwwroot

# (Optional) Copy additional files, such as a service account key
COPY RenderDiscordBot/serviceAccountKey.enc /app/serviceAccountKey.enc

# Define the entrypoint
ENTRYPOINT ["dotnet", "RenderDiscordBot.dll"]
