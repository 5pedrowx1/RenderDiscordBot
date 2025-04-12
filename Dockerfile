# ========== Stage 1: Build ==========

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copia o arquivo de projeto e restaura as dependências
COPY ["RenderDiscordBot.csproj", "./"]
RUN dotnet restore "./RenderDiscordBot.csproj"

# Copia o restante do código-fonte
COPY . .

# Publica em modo Release
RUN dotnet publish "RenderDiscordBot.csproj" -c Release -o /app/publish

# ========== Stage 2: Runtime ==========

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# Copia os binários publicados
COPY --from=build /app/publish ./

# Copia o arquivo serviceAccountKey.enc para a pasta /app dentro do container
COPY serviceAccountKey.enc /app/serviceAccountKey.enc

# Define o comando de entrada
ENTRYPOINT ["dotnet", "RenderDiscordBot.dll"]
