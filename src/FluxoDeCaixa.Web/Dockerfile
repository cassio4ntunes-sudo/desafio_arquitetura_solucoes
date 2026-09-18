# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

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

COPY . .
RUN dotnet publish src/FluxoDeCaixa.Web -c Release -o /app/publish --no-restore

# Runtime stage — nginx serves the static Blazor WASM files
FROM nginx:alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/FluxoDeCaixa.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
