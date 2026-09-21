# Fluxo de Caixa — Controle Diário

> **Desafio técnico — Arquiteto de Soluções**

Sistema de controle de fluxo de caixa diário para comerciantes, com registro de lançamentos (débitos e créditos), consulta de saldo consolidado diário, **autenticação federada via Keycloak (OIDC)** e **categorização de lançamentos**. Construído com **Event Sourcing + CQRS + Clean Architecture** sobre .NET 10, com frontend **Blazor WebAssembly + MudBlazor**.

---

## Tecnologias

| Tecnologia | Versão | Função |
|---|---|---|
| .NET | 10 (LTS) | Runtime e SDK |
| C# | 14 | Linguagem |
| Marten | 8.x | Event Sourcing + Document Store (PostgreSQL) |
| **Keycloak** | 26.0 | **Identity Provider** — OIDC, Authorization Code + PKCE, tokens RS256 |
| PostgreSQL | 16 | Banco de dados (Event Store + Consolidado) |
| RabbitMQ | 3 | Message Broker (desacoplamento) |
| MassTransit | 8.x | Abstração de mensageria (consumer/publisher) |
| MediatR | 12.x | CQRS — Commands e Queries |
| FluentValidation | 11.x | Validação de entrada |
| Serilog | 4.x | Logging estruturado (JSON) |
| Blazor WASM | 10.x | Frontend SPA (WebAssembly) |
| MudBlazor | 7.x | Component library (Material Design) |
| JWT Bearer | 10.x | Validação de token na API (resource server) |
| Microsoft.AspNetCore.OpenApi | 10.x | Geração nativa do documento OpenAPI (contrato em JSON, sem UI embarcada) |
| nginx | alpine | Servidor web para Blazor WASM (Docker) |
| Docker Compose | v2 | Orquestração local (6 containers) |
| xUnit | 2.x | Framework de testes |
| FluentAssertions | 7.x | Asserções expressivas |
| NSubstitute | 5.x | Mocking |
| Bogus | 35.x | Geração de dados fake |
| Testcontainers | 4.x | Testes de integração (PostgreSQL + RabbitMQ) |
| NBomber | 5.x | Testes de carga |

---

## Arquitetura

O sistema segue **Clean Architecture** com camadas bem definidas e dependências direcionadas para o centro (Domain):

```
API → Application → Domain ← Infrastructure
                      ↑
                Consolidação
                
Web (Blazor WASM) ──OIDC/PKCE──→ Keycloak
         └─────Bearer token─────→ API (valida RS256 via JWKS)
```

- **Domain** — Eventos, Value Objects (Dinheiro, CategoriaLancamento), Agregados. Zero dependências externas.
- **Application** — Commands, Queries, Handlers via MediatR. Validação via FluentValidation.
- **Infrastructure** — Marten (Event Store), MassTransit (RabbitMQ), Serilog.
- **Consolidação** — Consumer RabbitMQ + repositório Npgsql (tabela `consolidado_diario`).
- **API** — Endpoints Minimal API + validação do token do Keycloak + documento OpenAPI + middleware. Não emite token nem guarda senha.
- **Web** — Blazor WebAssembly SPA com MudBlazor (Material Design).

📐 Para diagramas C4, sequência e ADRs completos, veja [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Como Executar

### Pré-requisitos

- [Docker](https://www.docker.com/) 24+ com Docker Compose v2

### Subindo o ambiente

```bash
# Clonar o repositório
git clone https://github.com/jhenriquecosta/DesafioArquitetura.FluxoCaixa.git
cd DesafioArquitetura.FluxoCaixa

# Subir todos os serviços (Keycloak + API + Web + PostgreSQL + RabbitMQ)
# O --wait só retorna quando a API responde /health/ready
docker compose up --build -d --wait

# Verificar se está rodando
docker compose ps
```

### Acessos

| Serviço | URL | Credenciais |
|---|---|---|
| **Frontend (Blazor)** | http://localhost:5010 | `demo@loja.com` / `Senha123!` (login pelo Keycloak) |
| API REST (`carrefour.ms.fluxodecaixa`) | http://localhost:5000 | Bearer token emitido pelo Keycloak |
| Worker (`carrefour.wkr.consolidacao`) | http://localhost:5001/health/ready | Só probes de saúde — sem endpoint de negócio |
| **Keycloak** | http://localhost:8081 | Console admin: `admin` / `admin` |
| Descoberta OIDC do realm | http://localhost:8081/realms/fluxocaixa/.well-known/openid-configuration | — |
| Documento OpenAPI | http://localhost:5000/openapi/v1.json | — |
| Health (liveness / readiness) | http://localhost:5000/health/live · `/health/ready` | — |
| RabbitMQ Management | http://localhost:15672 | guest / guest |
| PostgreSQL | localhost:5433 | postgres / postgres |

> O comerciante `demo@loja.com` já vem com 7 dias de consolidado semeado. O `sub` dele no Keycloak (`1111...1111`) é exatamente o `comerciante_id` do seed — é isso que liga o token aos dados. Contas novas começam zeradas.

> ℹ️ O Keycloak usa a porta **8081** (e não a 8080) para não colidir com aplicações já em execução na máquina.

### Configuração

| Variável | Obrigatória | Padrão | Descrição |
|---|---|---|---|
| `Keycloak__Authority` | ✅ **Sim** | — | Issuer do realm, como o **browser** o enxerga. **A API não sobe sem ele** |
| `Keycloak__MetadataAddress` | Não | (usa o Authority) | URL de descoberta alcançável de dentro da rede Docker. Necessária porque browser e API chegam ao Keycloak por hostnames diferentes |
| `Keycloak__Audience` | Não | `fluxocaixa-api` | Audiência exigida no token |
| `Keycloak__RequireHttpsMetadata` | Não | `true` | Só `false` em ambiente local, onde o Keycloak roda sem TLS |
| `Cors__OrigensPermitidas__0` | Não | `http://localhost:5010` | Origens liberadas para o browser (sem `AllowAnyOrigin`) |
| `ConnectionStrings__Marten` | ✅ Sim | — | PostgreSQL (event store + read models) |
| `RabbitMq__Host` / `__Username` / `__Password` | ✅ Sim | — | Broker de mensagens |

Não há mais segredo de assinatura a gerenciar na aplicação: a chave privada fica no Keycloak e a API só consome a pública via JWKS.

### Parar e limpar

```bash
# Parar os serviços
docker compose down

# Parar e remover volumes (reset completo do banco)
docker compose down -v
```

> ⚠️ Se você já subiu uma versão anterior deste projeto, rode `docker compose down -v` antes de subir novamente. O schema de `consolidado_diario` mudou (passou a ter `comerciante_id` na chave primária) e o `CREATE TABLE IF NOT EXISTS` não migra um volume existente.

---

## Exemplos de Uso (curl)

### 1. Criar conta (comerciante)

O cadastro é feito no **Keycloak**, não pela API. Abra http://localhost:5010, clique em **Entrar** e depois em **Register** na tela do Keycloak — ou use o comerciante `demo@loja.com` que já vem semeado.

> A API não tem endpoint de registro nem de login: ela é um *resource server* e nunca vê a senha do comerciante. Ver [ADR-07](ARCHITECTURE.md#adr-07-keycloak-como-identity-provider-oidc).

### 2. Obter um token para usar no curl

No browser o fluxo é Authorization Code + PKCE. Para linha de comando, o realm expõe o cliente `fluxocaixa-testes` com *direct grant* (existe apenas para automação):

```bash
TOKEN=$(curl -s -X POST \
  http://localhost:8081/realms/fluxocaixa/protocol/openid-connect/token \
  -d 'client_id=fluxocaixa-testes' \
  -d 'grant_type=password' \
  -d 'username=demo@loja.com' \
  -d 'password=Senha123!' | jq -r .access_token)
```

**Conferir as claims do token:**

```bash
echo "$TOKEN" | jq -R 'split(".")[1] | @base64d | fromjson | {iss, aud, sub, preferred_username}'
```

```json
{
  "iss": "http://localhost:8081/realms/fluxocaixa",
  "aud": "fluxocaixa-api",
  "sub": "11111111-1111-1111-1111-111111111111",
  "preferred_username": "demo@loja.com"
}
```

> O `sub` é o `ComercianteId` usado para escopar todos os dados — é ele que liga o token ao consolidado.

### 3. Listar categorias disponíveis

```bash
curl -s http://localhost:5000/auth/categorias
```

**Resposta:** `200 OK`
```json
["Alimentação", "Transporte", "Salário", "Vendas", "Aluguel", "Serviços", "Impostos", "Outros"]
```

### 4. Registrar um lançamento de crédito

```bash
curl -s -X POST http://localhost:5000/lancamentos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"valor": 1500.50, "tipo": "Credito", "data": "2026-04-14", "descricao": "Venda de produtos", "categoria": "Vendas"}'
```

**Resposta:** `201 Created`
```json
{ "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6" }
```

### 5. Registrar um lançamento de débito

```bash
curl -s -X POST http://localhost:5000/lancamentos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"valor": 350.00, "tipo": "Debito", "data": "2026-04-14", "descricao": "Pagamento fornecedor", "categoria": "Serviços"}'
```

### 6. Consultar lançamentos do dia

```bash
curl -s "http://localhost:5000/lancamentos?data=2026-04-14&pagina=1&tamanhoPagina=20" \
  -H "Authorization: Bearer $TOKEN"
```

**Resposta:** `200 OK`
```json
{
  "itens": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "valor": 1500.50,
      "tipo": "Credito",
      "moeda": "BRL",
      "descricao": "Venda de produtos",
      "categoria": "Vendas",
      "data": "2026-04-14",
      "registradoEm": "2026-04-14T19:30:00Z"
    }
  ],
  "pagina": 1,
  "tamanhoPagina": 20,
  "total": 2,
  "totalPaginas": 1
}
```

> **Nota:** Cada comerciante só vê seus próprios lançamentos (filtrado pelo JWT `sub` claim).

### 7. Consultar saldo consolidado diário

```bash
curl -s "http://localhost:5000/consolidado/diario?data=2026-04-14" \
  -H "Authorization: Bearer $TOKEN"
```

**Resposta:** `200 OK`
```json
{
  "data": "2026-04-14",
  "totalCreditos": 1500.50,
  "totalDebitos": 350.00,
  "saldoLiquido": 1150.50,
  "quantidadeLancamentos": 2
}
```

> **Notas:**
> - O consolidado é **por comerciante**: o saldo é escopado pela claim `sub` do token, e dois comerciantes nunca enxergam o movimento um do outro.
> - A atualização é assíncrona via RabbitMQ — pode haver um breve delay (< 1s) entre o registro do lançamento e a atualização do saldo.
> - Um dia sem movimento responde `200` com zeros, não `404`: ausência de lançamento é uma resposta de negócio válida.

---

## Testes

```bash
# Testes unitários (sem dependências externas)
dotnet test tests/FluxoDeCaixa.Tests.Unit

# Testes de integração (requer Docker rodando — Testcontainers sobe PostgreSQL + RabbitMQ)
dotnet test tests/FluxoDeCaixa.Tests.Integration

# Teste de carga — valida o RNF de 50 req/s (requer o ambiente no ar)
docker compose up -d --wait
dotnet test tests/FluxoDeCaixa.Tests.Load

# Unitários + integração
dotnet test --filter "FullyQualifiedName~Tests.Unit|FullyQualifiedName~Tests.Integration"
```

| Tipo | Escopo | Ferramentas |
|---|---|---|
| Unitários | Domain, Application, Consolidação | xUnit, FluentAssertions, NSubstitute, Bogus |
| Integração | E2E autenticado (API → PostgreSQL → RabbitMQ → Consumer), isolamento entre comerciantes e idempotência do consumer | Testcontainers, WebApplicationFactory |
| Carga | 50 req/s por 2 min no consolidado, autenticado e sobre dados criados pela própria API | NBomber |

**Cenários que provam os requisitos do desafio:**

| Requisito | Teste |
|---|---|
| Lançamentos não caem se a consolidação cair (RNF do enunciado) | `Escrita_permanece_disponivel_independente_da_consolidacao` |
| Serviços de fato separados (o MS não consome a fila) | `SeparacaoDeServicosTests` |
| 50 req/s com ≤ 5% de perda (RNF do enunciado) | `Deve_suportar_50_requisicoes_por_segundo_por_2_minutos` |
| Isolamento entre comerciantes | `Consolidado_de_um_comerciante_nao_vaza_para_outro` |
| Reentrega de mensagem não duplica dinheiro | `Reentrega_do_mesmo_evento_nao_soma_duas_vezes` |
| Endpoints de negócio exigem autenticação | `Deve_recusar_acesso_sem_token` |

### Provando o RNF na prática — derrube a consolidação

O requisito central do desafio pode ser verificado à mão em um minuto. Com o ambiente no ar:

```bash
# 1. Derruba SÓ o worker de consolidação
docker compose stop carrefour.wkr.consolidacao

# 2. Registre lançamentos — todos devem responder 201
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5000/lancamentos \
  -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \
  -d "{\"valor\":10.00,\"tipo\":\"Credito\",\"data\":\"$(date -u +%F)\",\"descricao\":\"worker fora\",\"categoria\":\"Vendas\"}"

# 3. As mensagens ficam retidas na fila
docker compose exec rabbitmq rabbitmqctl list_queues name messages

# 4. Religue — o acúmulo é processado
docker compose start carrefour.wkr.consolidacao
```

Resultado observado nesta implementação:

```
worker parado    → POST /lancamentos ×5 = 201, 201, 201, 201, 201
                   MS /health/ready     = 200
                   fila LancamentoRegistrado = 5 mensagens retidas
                   consolidado congelado em 16 lançamentos
worker religado  → consolidado = 21 lançamentos (16 + 5)
                   créditos 3350,75 → 3400,75  (+5 × 10,00)
```

**Nenhuma requisição perdida, nenhum valor duplicado.** A retenção é da fila; a ausência de duplicação vem do inbox de idempotência (ADR-10).

---

## Estrutura do Projeto

```
src/
├── FluxoDeCaixa.Domain/              # Domínio (zero dependências externas)
│   ├── Eventos/                      # LancamentoRegistrado (event sourcing)
│   ├── ObjetosDeValor/               # Dinheiro, TipoLancamento, CategoriaLancamento
│   ├── Agregados/                    # Lancamento (aggregate root)
│   └── ModelosDeLeitura/             # LancamentoResumo (read model)
├── FluxoDeCaixa.Application/         # Casos de uso (CQRS via MediatR)
│   ├── Comandos/                     # RegistrarLancamento (+ ComercianteId, Categoria)
│   ├── Consultas/                    # ConsultarLancamentos (filtrado por comerciante)
│   ├── Validacao/                    # FluentValidation (valor, tipo, categoria)
│   └── Interfaces/                   # Portas (ILancamentoEventStore, etc.)
├── FluxoDeCaixa.Infrastructure/      # Marten, MassTransit, Serilog
│   ├── Persistencia/                 # MartenConfiguration, EventStore, QueryStore
│   └── Mensageria/                   # MassTransitConfiguration, Publisher impl
├── FluxoDeCaixa.Consolidacao/        # Consumer + repositório do read model (biblioteca)
│   ├── Consumidores/                 # LancamentoRegistradoConsumer (usado só pelo WKR)
│   ├── Repositorios/                 # PostgresConsolidadoRepository (Npgsql)
│   ├── Consultas/                    # ConsultarConsolidadoDiario (usado só pelo MS)
│   └── Modelos/                      # ConsolidadoDiario (read model)
│
│   ── SERVIÇOS (unidades de deploy) ──────────────────────────────────────
│
├── Carrefour.MS.FluxoDeCaixa/        # 🟦 Lançamentos: API REST + escrita + publicação
│   ├── Endpoints/                    # Lançamentos, Consolidado (leitura), Metadados
│   ├── DTOs/                         # Request/Response records
│   ├── Extensoes/                    # ClaimsPrincipalExtensions (sub → ComercianteId)
│   ├── Saude/                        # PostgresHealthCheck
│   ├── Middleware/                   # CorrelationIdMiddleware
│   └── Dockerfile
├── Carrefour.WKR.Consolidacao/       # 🟦 Consolidação: consome a fila e materializa
│   ├── Saude/                        # PostgresHealthCheck
│   ├── Program.cs                    # MassTransit + consumer; sem endpoint de negócio
│   └── Dockerfile
└── FluxoDeCaixa.Web/                 # Frontend Blazor WebAssembly + MudBlazor
    ├── Pages/                        # Dashboard, Lançamentos, Autenticacao (OIDC)
    ├── Layout/                       # MainLayout (sidebar, appbar, AuthorizeView)
    ├── Services/                     # ApiClient, ApiAuthorizationMessageHandler
    ├── Models/                       # DTOs client-side
    ├── Dockerfile                    # Multi-stage (SDK → nginx:alpine)
    └── nginx.conf                    # SPA routing + gzip

tests/
├── FluxoDeCaixa.Tests.Unit/          # Testes unitários (42 testes)
├── FluxoDeCaixa.Tests.Integration/   # E2E com Testcontainers — hospeda MS e WKR separados
└── FluxoDeCaixa.Tests.Load/          # Teste de carga (NBomber)
```

> **Bibliotecas × serviços.** O prefixo `Carrefour.*` marca as duas unidades implantáveis; as bibliotecas mantêm `FluxoDeCaixa.*` porque não têm deploy próprio. O worker não referencia `FluxoDeCaixa.Infrastructure`: ele não toca no event store, então não arrasta o Marten.

---

## Decisões Arquiteturais

| Decisão | Justificativa |
|---|---|
| **Event Sourcing via Marten** (não EF Core) | PostgreSQL nativo, append-only, event store + document store em um único banco |
| **RabbitMQ via MassTransit** | Desacoplamento real entre lançamentos e consolidação; consumer assíncrono com retry (500ms/2s/10s) |
| **Tabela separada `consolidado_diario`** (Npgsql) | Queries simples, schema controlado, UPSERT atômico (INSERT ON CONFLICT) com PK `(comerciante_id, data)` |
| **Inbox de idempotência no consumer** | A entrega do RabbitMQ é at-least-once: sem dedup por `LancamentoId`, uma reentrega somaria o valor duas vezes no saldo |
| **Keycloak como IdP** (não autenticação própria) | Identidade é capacidade genérica: comprar vence construir. Elimina de uma vez senha no banco, segredo de assinatura na aplicação, ausência de MFA e de revogação |
| **RS256 com JWKS** (não HS256) | Chave privada só no Keycloak; a API valida com a pública. Um parceiro pode validar o token sem jamais receber a chave que assina |
| **Clean Architecture** (4 camadas) | Separação clara de responsabilidades; Domain sem dependências externas |
| **Persist-then-Publish** | Marten persiste evento primeiro (source of truth), RabbitMQ publica segundo (best-effort) |
| **CQRS com MediatR** | Commands e Queries separados; pipeline de validação com FluentValidation |
| **API como resource server** | A API valida token, não emite. Nunca vê senha e não tem tabela de usuários — o `sub` do token é o `ComercianteId` |
| **Blazor WASM + MudBlazor** | SPA compilada para WebAssembly; Material Design components; zero servidor para UI |
| **nginx para Blazor em Docker** | Servir arquivos estáticos com gzip, caching e SPA routing (fallback → index.html) |
| **Categorias predefinidas** | 8 categorias fixas validadas no backend; CategoriaLancamento como constantes no Domain |

---

## API Endpoints

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| `GET` | `/auth/categorias` | ❌ | Listar categorias disponíveis |
| `POST` | `/lancamentos` | ✅ | Registrar lançamento (débito/crédito) |
| `GET` | `/lancamentos?data=...` | ✅ | Consultar lançamentos do dia (paginado) |
| `GET` | `/consolidado/diario?data=...` | ✅ | Consultar saldo consolidado do dia |
| `GET` | `/health/live` | ❌ | Liveness probe |
| `GET` | `/health/ready` | ❌ | Readiness probe (valida o PostgreSQL) |

📐 Para detalhes completos com diagramas, veja [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Documentação Adicional

**Requisitos obrigatórios do desafio:**

| Documento | Cobre |
|---|---|
| 🗺️ [docs/DOMINIOS-E-CAPACIDADES.md](docs/DOMINIOS-E-CAPACIDADES.md) | Mapeamento de domínios funcionais, capacidades de negócio, bounded contexts e context mapping |
| 📋 [docs/REQUISITOS.md](docs/REQUISITOS.md) | Requisitos funcionais e não funcionais refinados, com critério de aceite e rastreabilidade ao enunciado |
| 🎯 [docs/ARQUITETURA-ALVO.md](docs/ARQUITETURA-ALVO.md) | Arquitetura alvo em nuvem (multi-conta AWS), dimensionamento e **arquitetura de transição** (Strangler Fig) |
| 🧭 [docs/DECISOES-ARQUITETURAIS.md](docs/DECISOES-ARQUITETURAIS.md) | **Mapa das decisões** — como os 17 ADRs se encadeiam, o que cada um habilita e onde aparece na arquitetura alvo |
| 📐 [ARCHITECTURE.md](ARCHITECTURE.md) | Diagramas C4, sequência, camadas Clean Architecture e os 17 ADRs |

**Diferenciais:**

| Documento | Cobre |
|---|---|
| 💰 [docs/CUSTOS.md](docs/CUSTOS.md) | Estimativa de custos de infraestrutura e licenças, com premissas e comparativo de alternativas |
| 📊 [docs/OBSERVABILIDADE.md](docs/OBSERVABILIDADE.md) | Monitoramento, métricas de negócio, SLIs/SLOs e alertas |
| 🔐 [docs/SEGURANCA.md](docs/SEGURANCA.md) | Critérios de segurança para integração, modelo de ameaças STRIDE e lacunas conhecidas |

**Apoio:**

- 📖 [TUTORIAL.md](TUTORIAL.md) — Passo a passo completo para testar o sistema (curl + PowerShell)
- ❓ [FAQ.md](FAQ.md) — Perguntas e respostas sobre decisões arquiteturais

---

## Pré-requisitos para Desenvolvimento Local

| Requisito | Versão |
|---|---|
| .NET SDK | 10.0.401+ (fixado via `global.json`) |
| Docker | 24+ com Docker Compose v2 |
| IDE (sugestão) | Visual Studio 2022, Rider, ou VS Code com C# Dev Kit |

```bash
# Verificar versão do SDK
dotnet --version
# Esperado: 10.0.4xx

# Restaurar dependências e compilar
dotnet restore
dotnet build
```
