# SAT Platform server for Render / any Docker host.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SatPlatform/SatPlatform.csproj SatPlatform/
RUN dotnet restore SatPlatform/SatPlatform.csproj
COPY SatPlatform/ SatPlatform/
RUN dotnet publish SatPlatform/SatPlatform.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Npgsql probes for Kerberos (libgssapi) on startup; without it an error line is logged
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 tzdata curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "SatPlatform.dll"]
