# ========================
# STAGE 1: Build
# ========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o global.json (se existir)
COPY global.json ./

# Copia o arquivo .csproj e restaura as dependências
COPY ["RenderDiscordBot/RenderDiscordBot.csproj", "RenderDiscordBot/"]
RUN dotnet restore "RenderDiscordBot/RenderDiscordBot.csproj"

# Copia o restante dos arquivos do projeto
COPY . .

# Define o diretório de trabalho e publica a aplicação
WORKDIR "/src/RenderDiscordBot"
RUN dotnet publish "RenderDiscordBot.csproj" -c Release -o /app/publish --no-restore

# ========================
# STAGE 2: Runtime
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copia os arquivos publicados da stage de build
COPY --from=build /app/publish .

# Copia outros arquivos necessários (ex: chave ou config)
COPY RenderDiscordBot/serviceAccountKey.enc /app/serviceAccountKey.enc

# Define o comando padrão de inicialização
ENTRYPOINT ["dotnet", "RenderDiscordBot.dll"]
