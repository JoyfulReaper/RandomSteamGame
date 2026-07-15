FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY . .

RUN dotnet restore RandomSteamGame.slnx

# Production gate: the image does not build unless all tests pass.
RUN dotnet test RandomSteamGame.slnx \
    --configuration Release \
    --no-restore

RUN dotnet publish RandomSteamGame/RandomSteamGame.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

USER root

# curl is used by the container health check.
# libsqlite3-0 avoids relying on the image accidentally containing
# the native SQLite library.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        curl \
        libsqlite3-0 \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p \
        /app/Data \
        /data-protection \
    && chown -R "${APP_UID}:${APP_UID}" \
        /app \
        /data-protection

COPY --from=build /app/publish .

RUN chown -R "${APP_UID}:${APP_UID}" /app

USER ${APP_UID}

EXPOSE 5182

ENTRYPOINT ["dotnet", "RandomSteamGame.dll"]
