# ===== Stage 1: Build =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copie o arquivo global.json (certifique-se de que ele está na raiz)
COPY global.json ./

# Copia o arquivo .csproj para restaurar as dependências
COPY ["RenderDiscordBot/RenderDiscordBot.csproj", "RenderDiscordBot/"]

# Restaura as dependências do projeto
RUN dotnet restore "RenderDiscordBot/RenderDiscordBot.csproj"

# Copia o restante do código-fonte para o container
COPY . .

# Define o diretório de trabalho e publica a aplicação no modo Release
WORKDIR "/src/RenderDiscordBot"
RUN dotnet publish "RenderDiscordBot.csproj" -c Release -o /app/publish

# ===== Stage 2: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copia os arquivos publicados no estágio de build para a imagem final
COPY --from=build /app/publish .

# Opcional: copie outros arquivos necessários, por exemplo, um arquivo de chave
COPY RenderDiscordBot/serviceAccountKey.enc /app/serviceAccountKey.enc

# Define o comando de entrada para iniciar a aplicação
ENTRYPOINT ["dotnet", "RenderDiscordBot.dll"]
