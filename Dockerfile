# Use a imagem base do .NET
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

# Use a imagem base do SDK para build
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["RenderDiscordBot/RenderDiscordBot.csproj", "RenderDiscordBot/"]
RUN dotnet restore "RenderDiscordBot/RenderDiscordBot.csproj"
COPY . .
WORKDIR "/src/RenderDiscordBot"
RUN dotnet build "RenderDiscordBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RenderDiscordBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RenderDiscordBot.dll"]
