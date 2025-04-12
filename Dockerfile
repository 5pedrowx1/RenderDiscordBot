# ===== Stage 1: Build =====
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copia o arquivo de projeto (note o caminho relativo)
COPY ["RenderDiscordBot/RenderDiscordBot.csproj", "RenderDiscordBot/"]
RUN dotnet restore "RenderDiscordBot/RenderDiscordBot.csproj"

# Copia o restante do código-fonte
COPY . .
WORKDIR "/src/RenderDiscordBot"
RUN dotnet publish "RenderDiscordBot.csproj" -c Release -o /app/publish

# ===== Stage 2: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# Copia os arquivos publicados do estágio de build
COPY --from=build /app/publish .

# Copia o arquivo serviceAccountKey.enc
COPY RenderDiscordBot/serviceAccountKey.enc /app/serviceAccountKey.enc

# Define o comando de entrada para iniciar a aplicação
ENTRYPOINT ["dotnet", "RenderDiscordBot.dll"]
