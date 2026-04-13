# ── Build stage ──────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Restore first (cache layer)
COPY src/SchoolApi.csproj ./src/
RUN dotnet restore src/SchoolApi.csproj

# Build
COPY src/ ./src/
RUN dotnet publish src/SchoolApi.csproj -c Release -o /app/publish

# ── Runtime stage ────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:3002
EXPOSE 3002

ENTRYPOINT ["dotnet", "SchoolApi.dll"]
