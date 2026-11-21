# Multi-stage build: Vue client + .NET 10 API

# 1) Build the Vue client
FROM node:22-bookworm-slim AS client-build
WORKDIR /src/client

# Install dependencies first (leverage Docker layer caching)
COPY client/package*.json ./
RUN npm ci

# Copy the rest of the client and build
COPY client/ .
RUN npm run build

# 2) Restore and publish the .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY server/JellyFish.Api/JellyFish.Api.csproj server/JellyFish.Api/
RUN dotnet restore server/JellyFish.Api/JellyFish.Api.csproj

# Copy the rest of the source and publish
COPY server/JellyFish.Api/ server/JellyFish.Api/
RUN dotnet publish server/JellyFish.Api/JellyFish.Api.csproj -c Release -o /app/publish

# Copy the built client into wwwroot of the published app
RUN mkdir -p /app/publish/wwwroot
COPY --from=client-build /src/client/dist/ /app/publish/wwwroot/

# 3) Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Ensure time zone database is available for TimeZoneInfo (e.g., America/Denver)
RUN apk add --no-cache tzdata

# Copy published output
COPY --from=build /app/publish .

# Expose and configure default ASP.NET Core port
ENV ASPNETCORE_URLS=http://+:8085
EXPOSE 8085

ENTRYPOINT ["dotnet", "JellyFish.Api.dll"]

