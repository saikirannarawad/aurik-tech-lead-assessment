# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first for a cache-friendly restore.
COPY Aurik.Monitoring.sln ./
COPY src/Aurik.Monitoring.Domain/Aurik.Monitoring.Domain.csproj src/Aurik.Monitoring.Domain/
COPY src/Aurik.Monitoring.Application/Aurik.Monitoring.Application.csproj src/Aurik.Monitoring.Application/
COPY src/Aurik.Monitoring.Infrastructure/Aurik.Monitoring.Infrastructure.csproj src/Aurik.Monitoring.Infrastructure/
COPY src/Aurik.Monitoring.Api/Aurik.Monitoring.Api.csproj src/Aurik.Monitoring.Api/
COPY tests/Aurik.Monitoring.UnitTests/Aurik.Monitoring.UnitTests.csproj tests/Aurik.Monitoring.UnitTests/
COPY tests/Aurik.Monitoring.IntegrationTests/Aurik.Monitoring.IntegrationTests.csproj tests/Aurik.Monitoring.IntegrationTests/

RUN dotnet restore src/Aurik.Monitoring.Api/Aurik.Monitoring.Api.csproj

# Bring in the rest and publish.
COPY src/ src/
COPY seed/ seed/

RUN dotnet publish src/Aurik.Monitoring.Api/Aurik.Monitoring.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=build /src/seed /app/seed

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "Aurik.Monitoring.Api.dll"]
