# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy NuGet.Config and solution/project files for restore
COPY NuGet.Config .
COPY FluxoDeCaixa.sln .
COPY src/FluxoDeCaixa.Domain/*.csproj src/FluxoDeCaixa.Domain/
COPY src/FluxoDeCaixa.Application/*.csproj src/FluxoDeCaixa.Application/
COPY src/FluxoDeCaixa.Infrastructure/*.csproj src/FluxoDeCaixa.Infrastructure/
COPY src/FluxoDeCaixa.Consolidacao/*.csproj src/FluxoDeCaixa.Consolidacao/
COPY src/FluxoDeCaixa.Api/*.csproj src/FluxoDeCaixa.Api/
COPY src/FluxoDeCaixa.Web/*.csproj src/FluxoDeCaixa.Web/
COPY tests/FluxoDeCaixa.Tests.Unit/*.csproj tests/FluxoDeCaixa.Tests.Unit/
COPY tests/FluxoDeCaixa.Tests.Integration/*.csproj tests/FluxoDeCaixa.Tests.Integration/
COPY tests/FluxoDeCaixa.Tests.Load/*.csproj tests/FluxoDeCaixa.Tests.Load/

RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/FluxoDeCaixa.Api -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl para o healthcheck do compose/orquestrador consultar /health/ready
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "FluxoDeCaixa.Api.dll"]
