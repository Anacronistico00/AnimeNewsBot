FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . .

RUN dotnet restore Anime_NewsProva_bot.csproj
RUN dotnet publish Anime_NewsProva_bot.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

COPY --from=build /app/out .

# Crea la directory Storage e un utente non-root per sicurezza
RUN mkdir -p Storage && \
    adduser --disabled-password --gecos '' appuser && \
    chown -R appuser:appuser /app

USER appuser

ENTRYPOINT ["dotnet", "Anime_NewsProva_bot.dll"]