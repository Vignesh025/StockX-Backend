FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StockX.API/StockX.API.csproj StockX.API/
COPY StockX.Core/StockX.Core.csproj StockX.Core/
COPY StockX.Infrastructure/StockX.Infrastructure.csproj StockX.Infrastructure/
COPY StockX.Services/StockX.Services.csproj StockX.Services/

RUN dotnet restore StockX.API/StockX.API.csproj

COPY . .
RUN dotnet publish StockX.API/StockX.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "StockX.API.dll"]
