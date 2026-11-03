# syntax=docker/dockerfile:1.7
# RechnungsWerk Web-App: Bau mit dem .NET SDK, Laufzeit im schlanken ASP.NET-Image als Nicht-root-Benutzer.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/RechnungsWerk.Core/RechnungsWerk.Core.csproj src/RechnungsWerk.Core/
COPY src/RechnungsWerk.Web/RechnungsWerk.Web.csproj src/RechnungsWerk.Web/
RUN dotnet restore src/RechnungsWerk.Web/RechnungsWerk.Web.csproj
COPY src/ src/
RUN dotnet publish src/RechnungsWerk.Web/RechnungsWerk.Web.csproj \
        --configuration Release --no-restore --output /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Data__Path=/data \
    DOTNET_NOLOGO=1
# Datenordner: SQLite-Datenbank, DataProtection-Schlüssel, Einrichtungscode – gehört dem Benutzer "app" (UID 1654).
RUN mkdir -p /data && chown app:app /data
COPY --from=build /app ./
COPY LICENSE ./LICENSE
USER app
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "RechnungsWerk.Web.dll"]
