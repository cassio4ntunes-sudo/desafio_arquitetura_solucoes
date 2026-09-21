# ─────────────────────────────────────────────────────────────────────────────
# Carrefour.WKR.Consolidacao — worker de consolidação
#
# Contexto de build = raiz deste repositório. Repare no quanto é menor que o do
# serviço de lançamentos: aqui não há Marten, nem Application, nem OpenAPI.
# O worker carrega apenas o contrato consumido e o acesso ao read model.
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config global.json ./
COPY src/FluxoDeCaixa.Domain/*.csproj        src/FluxoDeCaixa.Domain/
COPY src/FluxoDeCaixa.Consolidacao/*.csproj  src/FluxoDeCaixa.Consolidacao/
COPY src/Carrefour.WKR.Consolidacao/*.csproj src/Carrefour.WKR.Consolidacao/
RUN dotnet restore src/Carrefour.WKR.Consolidacao/Carrefour.WKR.Consolidacao.csproj

COPY src/ src/
RUN dotnet publish src/Carrefour.WKR.Consolidacao/Carrefour.WKR.Consolidacao.csproj \
      -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Carrefour.WKR.Consolidacao.dll"]
