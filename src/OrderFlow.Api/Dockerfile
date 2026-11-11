FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copia apenas os csproj primeiro para aproveitar o cache de layers do Docker
COPY ["OrderFlow.sln", "./"]
COPY ["src/OrderFlow.Contracts/OrderFlow.Contracts.csproj", "src/OrderFlow.Contracts/"]
COPY ["src/OrderFlow.Api/OrderFlow.Api.csproj", "src/OrderFlow.Api/"]
COPY ["src/OrderFlow.Worker/OrderFlow.Worker.csproj", "src/OrderFlow.Worker/"]
RUN dotnet restore "src/OrderFlow.Api/OrderFlow.Api.csproj"

# Copia o código-fonte e publica o binário otimizado
COPY ["src/OrderFlow.Contracts/", "src/OrderFlow.Contracts/"]
COPY ["src/OrderFlow.Api/", "src/OrderFlow.Api/"]
RUN dotnet publish "src/OrderFlow.Api/OrderFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Imagem final leve apenas com o ASP.NET Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]