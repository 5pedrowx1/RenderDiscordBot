# ===== Stage 1: Build =====
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copia o arquivo .csproj para restaurar as dependências
# Ajuste o caminho abaixo se o seu arquivo .csproj estiver em outro local
COPY ["RenderDiscordBot/RenderDiscordBot.csproj", "RenderDiscordBot/"]

# Restaura as dependências do projeto
RUN dotnet restore "RenderDiscordBot/RenderDiscordBot.csproj"

# Copia o restante do código-fonte para o container
COPY . .

# Define o diretório de trabalho como a pasta do projeto e compila (publica) a aplicação
WORKDIR "/src/RenderDiscordBot"
RUN dotnet publish "RenderDiscordBot.csproj" -c Release -o /app/publish

# ===== Stage 2: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# Copia os arquivos compilados no estágio anterior para a imagem final
COPY --from=build /app/publish .

# Define o comando de entrada que inicia a aplicação
ENTRYPOINT ["dotnet", "RenderDiscordBot.dll"]
