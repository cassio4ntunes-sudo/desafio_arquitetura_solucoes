# ─────────────────────────────────────────────────────────────────────────────
# FluxoDeCaixa.Web — frontend Blazor WebAssembly
#
# Contexto de build = raiz deste repositório. O frontend nunca compartilhou
# código com o backend: fala com a API por HTTP e com o Keycloak por OIDC.
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config global.json ./
COPY src/FluxoDeCaixa.Web/*.csproj src/FluxoDeCaixa.Web/
RUN dotnet restore src/FluxoDeCaixa.Web/FluxoDeCaixa.Web.csproj

COPY src/ src/
RUN dotnet publish src/FluxoDeCaixa.Web/FluxoDeCaixa.Web.csproj \
      -c Release -o /app/publish --no-restore

# nginx serve os arquivos estáticos compilados para WebAssembly
FROM nginx:alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/FluxoDeCaixa.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
