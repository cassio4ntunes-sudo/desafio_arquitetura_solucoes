# ─────────────────────────────────────────────────────────────────────────────
# MS.FluxoDeCaixa — serviço de lançamentos
#
# O contexto de build é a RAIZ DESTE REPOSITÓRIO. Nada fora daqui é necessário
# para produzir a imagem: é a prova de que o serviço é autossuficiente (ADR-18).
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Camada de restore — só os manifestos, para aproveitar cache entre builds
COPY NuGet.Config global.json ./
COPY src/FluxoDeCaixa.Domain/*.csproj          src/FluxoDeCaixa.Domain/
COPY src/FluxoDeCaixa.Application/*.csproj     src/FluxoDeCaixa.Application/
COPY src/FluxoDeCaixa.Infrastructure/*.csproj  src/FluxoDeCaixa.Infrastructure/
COPY src/FluxoDeCaixa.Consolidacao/*.csproj    src/FluxoDeCaixa.Consolidacao/
COPY src/MS.FluxoDeCaixa/*.csproj    src/MS.FluxoDeCaixa/
RUN dotnet restore src/MS.FluxoDeCaixa/MS.FluxoDeCaixa.csproj

# Os testes ficam fora da imagem de propósito
COPY src/ src/
RUN dotnet publish src/MS.FluxoDeCaixa/MS.FluxoDeCaixa.csproj \
      -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl para o healthcheck do orquestrador consultar /health/ready
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "MS.FluxoDeCaixa.dll"]
