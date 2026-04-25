FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src


COPY ["CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Domain/CityDiscovery.RecommendationService.Domain.csproj", "CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Domain/"]
COPY ["CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Application/CityDiscovery.RecommendationService.Application.csproj", "CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Application/"]
COPY ["CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Infrastructure/CityDiscovery.RecommendationService.Infrastructure.csproj", "CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Infrastructure/"]
COPY ["CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Workers/CityDiscovery.RecommendationService.Workers.csproj", "CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Workers/"]
RUN dotnet restore "CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Workers/CityDiscovery.RecommendationService.Workers.csproj"

# Tum solution icerigini kopyala (Domain, Application, Infrastructure, Shared vs.)
COPY . .

WORKDIR "/src/CityDiscovery.RecommendationService/CityDiscovery.RecommendationService/src/CityDiscovery.RecommendationService.Workers"
RUN dotnet build "CityDiscovery.RecommendationService.Workers.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CityDiscovery.RecommendationService.Workers.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CityDiscovery.RecommendationService.Workers.dll"]

