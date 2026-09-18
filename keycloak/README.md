# Realm do Keycloak — `fluxocaixa`

O arquivo [`realm-fluxocaixa.json`](realm-fluxocaixa.json) é importado automaticamente no boot do container (`start-dev --import-realm`). **Nenhuma configuração manual é necessária** — a identidade do sistema é infraestrutura versionada, não cliques em console.

## O que o realm define

| Item | Valor | Por quê |
|---|---|---|
| Realm | `fluxocaixa` | Isolado do `master`, que fica só para administração |
| Cliente do frontend | `fluxocaixa-web` | Público, **Authorization Code + PKCE (S256)**. Sem client secret — num SPA não há onde guardá-lo com segurança |
| Cliente da API | `fluxocaixa-api` | *Bearer-only*: existe apenas como alvo de audiência. A API não inicia fluxo nenhum |
| Cliente de testes | `fluxocaixa-testes` | *Direct grant* (ROPC) para obter token sem browser. **Só para automação** — não deve existir em produção |
| Papel | `comerciante` | Atribuído aos usuários; disponível na claim `realm_access.roles` |
| Tempo de vida do token | 15 min | Reduz a janela de um token vazado; a sessão SSO dura 30 min ociosa |
| Política de senha | 8+ caracteres, 1 maiúscula, 1 dígito, ≠ usuário | — |
| Brute force | Bloqueio após 10 falhas, espera incremental | — |
| Auto-registro | Habilitado | Permite que o avaliador crie uma conta pela tela do Keycloak |

## Usuários pré-cadastrados

Senha de todos: `Senha123!`

| Usuário | `sub` (= `ComercianteId`) | Uso |
|---|---|---|
| `demo@loja.com` | `11111111-1111-1111-1111-111111111111` | Demonstração — **tem 7 dias de consolidado semeado** |
| `comerciante-a@teste.com` | `22222222-2222-2222-2222-222222222222` | Testes de integração |
| `comerciante-b@teste.com` | `33333333-3333-3333-3333-333333333333` | Testes de integração (isolamento multi-inquilino) |

> Os ids são **fixos de propósito**: o `sub` de `demo@loja.com` é exatamente o `comerciante_id` usado em [`scripts/seed-data.sql`](../scripts/seed-data.sql). É isso que faz o consolidado semeado aparecer ao logar com ele.

## Duas armadilhas que valem registro

**1. Não declare `clientScopes` no realm.** Declarar o array `clientScopes` faz o Keycloak criar **apenas** os escopos listados, substituindo os embutidos (`basic`, `profile`, `email`, `roles`, `web-origins`, `acr`). Como a claim `sub` vem do escopo `basic`, o token sai sem `sub` — e o `ComercianteId` fica inacessível. Por isso o mapper de audiência está anexado **diretamente a cada cliente**, via `protocolMappers`.

**2. O importador rejeita campos desconhecidos.** Não há como deixar comentários dentro do JSON: qualquer chave não reconhecida (inclusive uma como `"_comentario"`) aborta a importação com `Unrecognized field`. Esta documentação existe por causa disso.

## Issuer vs. backchannel no Docker

O browser alcança o Keycloak em `http://localhost:8081`; a API, em `http://keycloak:8080`. Mas o claim `iss` do token precisa ser **um só**, senão a validação falha.

A solução está no `compose.yaml`:

- `KC_HOSTNAME=http://localhost:8081` — fixa o issuer público, que é o que vai no token
- `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true` — deixa o `jwks_uri` do documento de descoberta resolver pelo hostname da requisição

Assim, quando a API busca a descoberta por `keycloak:8080`, recebe `issuer: http://localhost:8081/...` (que ela valida) e `jwks_uri: http://keycloak:8080/...` (que ela consegue alcançar). Do lado da API, isso aparece como `Keycloak:Authority` e `Keycloak:MetadataAddress` configurados separadamente.

## Administração

Console em http://localhost:8081 — usuário `admin`, senha `admin`.

> Credenciais fixas e `start-dev` (banco H2 em memória, HTTP sem TLS) são adequados **apenas ao ambiente local**. Para produção: `start`, PostgreSQL dedicado, TLS, hostname real, credenciais em secret manager e cluster multi-AZ. Ver [docs/ARQUITETURA-ALVO.md](../docs/ARQUITETURA-ALVO.md) e [docs/SEGURANCA.md](../docs/SEGURANCA.md).

## Exportar o realm após mudanças no console

Se você ajustar algo pelo console e quiser versionar:

```bash
docker compose exec keycloak /opt/keycloak/bin/kc.sh export \
  --dir /tmp/export --realm fluxocaixa --users realm_file
docker compose cp keycloak:/tmp/export/fluxocaixa-realm.json ./keycloak/
```

Revise o arquivo antes de commitar — o export traz muito ruído e pode conter segredos gerados.
