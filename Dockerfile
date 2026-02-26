# Estágio de Build: Usando o SDK 10.0 mais recente
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Restauração de pacotes
COPY *.csproj ./
RUN dotnet restore

# Build e Publicação
COPY . ./
RUN dotnet publish -c Release -o out

# Estágio de Runtime: Apenas o necessário para rodar
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

# Porta padrão do ASP.NET Core 10 no Docker é 8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GerenciadorReservas.dll"]