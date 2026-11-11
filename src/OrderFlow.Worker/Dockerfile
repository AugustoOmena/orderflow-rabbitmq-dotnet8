FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ["OrderFlow.sln", "./"]
COPY ["src/OrderFlow.Contracts/OrderFlow.Contracts.csproj", "src/OrderFlow.Contracts/"]
COPY ["src/OrderFlow.Api/OrderFlow.Api.csproj", "src/OrderFlow.Api/"]
COPY ["src/OrderFlow.Worker/OrderFlow.Worker.csproj", "src/OrderFlow.Worker/"]
RUN dotnet restore "src/OrderFlow.Worker/OrderFlow.Worker.csproj"

COPY ["src/OrderFlow.Contracts/", "src/OrderFlow.Contracts/"]
COPY ["src/OrderFlow.Worker/", "src/OrderFlow.Worker/"]
RUN dotnet publish "src/OrderFlow.Worker/OrderFlow.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Imagem final leve apenas com o .NET Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Worker.dll"]