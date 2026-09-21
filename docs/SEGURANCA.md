# Segurança e Critérios para Consumo de Serviços

Cobre os controles **implementados**, os **critérios de integração** que um consumidor externo precisa atender e o **modelo de ameaças** que orientou as decisões.

---

## 1. Controles implementados

| Controle | Implementação | Onde |
|---|---|---|
| **Identity Provider** | **Keycloak 26** — realm versionado e importado no boot; nenhuma configuração manual | `keycloak/realm-fluxocaixa.json` |
| Autenticação (browser) | **OIDC Authorization Code + PKCE (S256)**, cliente público sem secret. A senha é digitada no Keycloak e nunca transita pela aplicação | `Web/Program.cs` |
| Validação de token | **RS256** verificado contra o JWKS do realm; `iss` e `aud` validados; `ClockSkew` de 1 min | `Api/Program.cs` |
| Senhas | **Não existem na aplicação.** Armazenamento, hash e política ficam no Keycloak | — |
| Política de senha | Mínimo 8 caracteres, 1 maiúscula, 1 dígito, diferente do usuário | realm |
| Proteção a força bruta | `bruteForceProtected`: bloqueio após 10 falhas, com espera incremental | realm |
| Tempo de vida do token | **15 min** (era 24 h), com sessão gerenciada pelo IdP e logout federado | realm |
| Autorização | Política `comerciante` (`RequireAuthenticatedUser` + claim `sub`) em todos os grupos de rota de negócio | `LancamentosEndpoints`, `ConsolidadoEndpoints` |
| **Isolamento multi-inquilino** | `ComercianteId` vem **sempre** da claim `sub` do token — nunca do body ou da query; chave primária `(comerciante_id, data)` no consolidado | `ClaimsPrincipalExtensions`, `PostgresConsolidadoRepository` |
| Gestão de segredo | **Não há segredo de assinatura na aplicação** — a chave privada vive no Keycloak. A API falha ao subir sem `Keycloak:Authority` | `Api/Program.cs` |
| Separação de fluxos | ROPC (*direct grant*) existe apenas no cliente `fluxocaixa-testes`, isolado da automação; o cliente do browser só aceita PKCE | realm |
| Rate limiting | 60 req/s no consolidado; 10 req/min no auth (freio a força bruta) | `Program.cs` |
| CORS | Lista explícita de origens por configuração; sem `AllowAnyOrigin` | `Program.cs` |
| Validação de entrada | FluentValidation em pipeline do MediatR, antes do domínio | `ValidacaoBehavior` |
| Injeção de SQL | 100% das consultas parametrizadas (Npgsql) ou via LINQ do Marten | Repositórios |
| Vazamento por mensagem de erro | ProblemDetails sem stack trace; o `500` expõe apenas o correlation id | `Program.cs` |
| Rastreabilidade | Correlation id por requisição + event store append-only | `CorrelationIdMiddleware` |

> **Decisão deliberada:** a ausência de fallback na configuração de identidade torna o deploy "mais frágil" de propósito. Um `Authority` embutido significaria que um deploy desconfigurado aceitaria tokens de um emissor que não é o nosso. Falhar no boot é o comportamento correto.

> **Por que o IdP fecha esta seção:** autenticar na própria aplicação (tabela `usuarios`, BCrypt, JWT HS256) deixaria quatro lacunas de risco **alto** e **médio** em aberto por construção — ausência de revogação, HS256 impedindo validação por terceiros, ausência de MFA e ausência de política de senha. Delegar ao Keycloak resolve as quatro por configuração, não por código. Ver [ADR-07](../ARCHITECTURE.md#adr-07-keycloak-como-identity-provider-oidc).

---

## 2. Critérios de segurança para consumo (integração)

Requisitos que um sistema consumidor — ERP do comerciante, app parceiro, outro serviço interno — deve atender.

### 2.1 Autenticação por tipo de consumidor

| Consumidor | Mecanismo | Credencial |
|---|---|---|
| Frontend (browser) | OAuth 2.0 Authorization Code + PKCE | Token de curta duração; refresh rotativo |
| Serviço parceiro (server-to-server) | OAuth 2.0 Client Credentials | `client_id` + `client_secret` rotacionado a cada 90 dias |
| Serviço interno na mesma VPC | mTLS + JWT | Certificado emitido por CA privada |
| Batch / ETL | Client Credentials com escopo somente-leitura | Credencial dedicada, nunca a de usuário |

> **Já atendido:** os tokens são **RS256** e o realm publica a chave pública em JWKS (`/realms/fluxocaixa/protocol/openid-connect/certs`). Um parceiro valida o token sem jamais possuir a chave que assina. Para acesso server-to-server, basta criar um cliente com *Client Credentials* no realm.

### 2.2 Escopos mínimos

Princípio do menor privilégio — um parceiro que só lê relatório não recebe permissão de escrita:

| Escopo | Permite |
|---|---|
| `lancamentos:escrever` | `POST /lancamentos` |
| `lancamentos:ler` | `GET /lancamentos` |
| `consolidado:ler` | `GET /consolidado/diario` |

### 2.3 Transporte e rede

- **TLS 1.2+ obrigatório** em toda comunicação externa; HSTS na borda; TLS 1.3 preferencial.
- Componentes internos (banco, broker) em subnets privadas, sem IP público; acesso só por security group da aplicação.
- WAF na borda com as regras do OWASP Top 10.

### 2.4 Contrato de consumo

| Critério | Requisito |
|---|---|
| Rate limit | 60 req/s por cliente; `429` com `Retry-After` |
| Idempotência | Header `Idempotency-Key` em `POST /lancamentos` (evolução) — o consumidor deve enviá-lo e tratar reenvio com segurança |
| Timeout | 5 s de leitura; o consumidor implementa **circuit breaker** e **retry com backoff exponencial e jitter** |
| Versionamento | Versão na rota (`/v1/...`); mudança incompatível só em nova versão, com 6 meses de convivência |
| Payload | Limite de 1 MB; `Content-Type: application/json` obrigatório |
| Auditoria | Toda chamada registrada com `client_id`, correlation id, IP de origem e resultado |

### 2.5 Proteção de dados

| Dado | Em trânsito | Em repouso | Em log |
|---|---|---|---|
| Senha | TLS | Hash no Keycloak — nunca na aplicação | **Nunca** |
| Token JWT | TLS | Não persistido no servidor | **Nunca** (mascarado) |
| Valor do lançamento | TLS | Criptografia do volume (RDS KMS) | Permitido |
| E-mail do comerciante | TLS | Criptografia do volume | Mascarado (`j***@loja.com`) |

**LGPD:** e-mail e nome são dados pessoais. Base legal é a execução de contrato. Retenção de 5 anos por exigência fiscal, sobrepondo-se ao direito de exclusão enquanto vigente. Direito de portabilidade atendido por exportação do extrato. Uma exclusão de conta anonimiza o cadastro **sem** apagar os eventos financeiros — o event store é append-only e o registro contábil precisa sobreviver.

---

## 3. Modelo de ameaças (STRIDE)

| Ameaça | Cenário | Mitigação | Estado |
|---|---|---|---|
| **S**poofing | Forjar token para agir como outro comerciante | Assinatura **RS256** validada contra o JWKS do realm; chave privada só no Keycloak | ✅ Implementado e **testado** |
| **S**poofing | Força bruta de senha | Brute-force protection do Keycloak (bloqueio após 10 falhas, espera incremental) + rate limit | ✅ Implementado |
| **S**poofing | Token de outro cliente do realm aceito pela API | Audiência `fluxocaixa-api` validada em todo token | ✅ Implementado |
| **T**ampering | Alterar `comerciante_id` no payload para lançar na conta alheia | Id vem exclusivamente da claim `sub`; jamais do corpo da requisição | ✅ Implementado |
| **T**ampering | Alterar lançamento já registrado | Event store append-only; sem endpoint de update ou delete | ✅ Implementado |
| **R**epudiation | Comerciante nega ter feito um lançamento | Evento imutável com timestamp UTC, id do comerciante e correlation id | ✅ Implementado |
| **I**nformation Disclosure | **Ler o consolidado de outro comerciante** | PK `(comerciante_id, data)` + consulta escopada pela claim | ✅ Implementado e **testado** |
| **I**nformation Disclosure | Stack trace expondo estrutura interna | ProblemDetails sem detalhes de exceção | ✅ Implementado |
| **D**enial of Service | Inundar o consolidado no pico | Rate limiting + autoscaling + read model materializado | ✅ Implementado |
| **D**enial of Service | Consolidação cair e derrubar os lançamentos | Desacoplamento assíncrono via RabbitMQ | ✅ Implementado e testado |
| **E**levation of Privilege | Usuário comum acessar dado administrativo | Não há papel administrativo no escopo atual | ⚠️ N/A hoje |
| **T**ampering | Reentrega de mensagem duplicar valor no saldo | Inbox de idempotência com PK no `LancamentoId` | ✅ Implementado e testado |

---

## 4. Lacunas conhecidas e prioridade

Declaradas abertamente — um controle que não existe é risco, não seja qual for o texto do documento.

### Fechadas pela adoção do Keycloak (ADR-07)

| Lacuna anterior | Como foi fechada |
|---|---|
| ~~JWT sem revogação~~ | Token de 15 min + sessão no IdP; logout federado encerra a sessão no Keycloak |
| ~~HS256 em vez de RS256~~ | RS256 com JWKS público — parceiro valida sem receber a chave que assina |
| ~~Sem política de complexidade de senha~~ | Política no realm: 8+ caracteres, maiúscula, dígito, diferente do usuário |
| ~~Sem MFA~~ | Suportado pelo Keycloak; basta habilitar OTP no realm (não ativado por padrão para não atrapalhar a avaliação) |
| ~~Segredo sem rotação automática~~ | Não há mais segredo na aplicação; a rotação de chaves é função do Keycloak |

### Ainda abertas

| Lacuna | Risco | Prioridade | Correção |
|---|---|---|---|
| Keycloak em `start-dev` (H2, HTTP puro) | Adequado só ao ambiente local; sem persistência confiável nem TLS | **Alta** (para produção) | `start` com PostgreSQL dedicado, TLS e hostname real; cluster multi-AZ |
| Credenciais de admin do Keycloak fixas (`admin`/`admin`) | Console administrativo exposto no ambiente local | **Alta** (para produção) | Secret manager + rede privada; console nunca exposto publicamente |
| Cliente `fluxocaixa-testes` com ROPC | ROPC é desencorajado no OAuth 2.1 | Média | Remover o cliente no realm de produção — existe apenas para automação |
| MFA não ativado | Conta comprometida por senha única | Média | Habilitar OTP como *required action* no realm |
| Sem varredura de dependências no CI | CVE em pacote passa despercebida — a primeira varredura manual achou **CVE crítica no Marten 8.29.3** | **Alta** | `dotnet list package --vulnerable` como step + Dependabot |
| Sem auditoria de acesso por `client_id` | Dificulta investigação de incidente | Baixa | Ativar *event listener* de auditoria no Keycloak e exportar para o SIEM |
| Rate limit por instância, não distribuído | Limite efetivo multiplica pelo número de tasks | Baixa | Mover para o API Gateway ou usar limitador com Redis |
