# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as a separate layer so it is cached unless the project file changes.
COPY TraderAlgoApi/TraderAlgoApi.csproj TraderAlgoApi/
RUN dotnet restore TraderAlgoApi/TraderAlgoApi.csproj

# Copy the rest of the source and publish a framework-dependent build.
COPY . .
RUN dotnet publish TraderAlgoApi/TraderAlgoApi.csproj -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# Render routes external HTTPS traffic to whatever port the container listens on.
# Its default is 10000; bind Kestrel to it over plain HTTP (TLS is terminated at the edge).
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "TraderAlgoApi.dll"]
