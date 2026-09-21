# 🧪 Tutorial — Testando o Sistema de Fluxo de Caixa do Comerciante

> Guia prático, passo a passo, para subir o ambiente e testar todas as funcionalidades do sistema de fluxo de caixa via Docker Compose.

---

## Passo a Passo — Testando o Fluxo de Caixa

### Pré-requisitos

| Ferramenta | Descrição |
|---|---|
| **Docker Desktop** | Instalado e **rodando** (ícone verde na bandeja do sistema) |
| **curl** | Disponível no terminal (Linux/macOS nativo; Windows via Git Bash ou WSL) |
| **jq** | Para formatar JSON no terminal — `sudo apt install jq` ou `choco install jq` |
| **PowerShell** | Alternativa ao curl (seção dedicada no final deste tutorial) |
| **Postman** | Opcional — para quem prefere interface gráfica |

---

### Passo 1: Subir o ambiente

Clone o repositório e inicie os containers:

```bash
git clone https://github.com/cassio4ntunes-sudo/desafio_arquitetura_solucoes.git
cd desafio_arquitetura_solucoes
docker compose up --build -d --wait
```

O `--wait` só retorna quando a API responde `/health/ready`. Confira os containers:

```bash
docker compose ps
```

Saída esperada (todos com status `healthy` ou `Up`):

| Container | Porta(s) | Descrição |
|---|---|---|
| **web** | `5010` | Interface web (frontend) |
| **api** | `5000` | API REST (.NET) |
| **keycloak** | `8081` | Identity Provider (OIDC) — realm importado automaticamente |
| **postgres** | `5433` | Banco de dados PostgreSQL |
| **rabbitmq** | `5672` / `15672` | Message broker (AMQP / Painel de gestão) |

> 💡 **Dica:** Se algum container reiniciar, aguarde ~30 segundos e execute `docker compose ps` novamente. O `api` pode demorar um pouco mais enquanto aguarda o banco ficar disponível.

---

### Passo 2: Acessar a interface web

1. Abra o navegador e acesse: **http://localhost:5010**
2. Faça login com o comerciante de demonstração — **demo@loja.com** / **Senha123!** — que já vem com 7 dias de consolidado semeado. Ou crie uma conta nova pelo formulário de registro (ela começa zerada).
3. Cada comerciante enxerga apenas os próprios lançamentos e o próprio saldo.
4. Explore o **dashboard** — ele exibe o resumo dos lançamentos e o saldo consolidado.

> 📌 A interface web consome a mesma API que testaremos nos próximos passos.

---

### Passo 3: Criar conta (no Keycloak)

O cadastro **não passa pela API** — é o Keycloak que guarda usuários e credenciais. Há duas formas:

1. **Pela interface:** em http://localhost:5010, clique em **Entrar** e depois em **Register** na tela do Keycloak.
2. **Usando o comerciante de demonstração:** `demo@loja.com` / `Senha123!`, que já vem com 7 dias de consolidado semeado.

> 🔐 A senha é digitada na página do Keycloak e **nunca transita pela aplicação**. A API não tem endpoint de registro nem de login: ela só valida tokens.

---

### Passo 4: Obter token

No browser, o fluxo é Authorization Code + PKCE (você já o exercita ao clicar em **Entrar**). Para a linha de comando, o realm expõe o cliente `fluxocaixa-testes` com *direct grant* — que existe **apenas para automação**:

```bash
TOKEN=$(curl -s -X POST \
  http://localhost:8081/realms/fluxocaixa/protocol/openid-connect/token \
  -d 'client_id=fluxocaixa-testes' \
  -d 'grant_type=password' \
  -d 'username=demo@loja.com' \
  -d 'password=Senha123!' | jq -r .access_token)

echo $TOKEN
```

**Inspecione o token** — repare no algoritmo e nas claims:

```bash
# Cabeçalho: alg = RS256 (assimétrico), não HS256
echo "$TOKEN" | jq -R 'split(".")[0] | @base64d | fromjson | {alg, kid}'

# Payload: iss, aud e o sub que vira o ComercianteId
echo "$TOKEN" | jq -R 'split(".")[1] | @base64d | fromjson | {iss, aud, sub, preferred_username}'
```

```json
{ "alg": "RS256", "kid": "..." }
{
  "iss": "http://localhost:8081/realms/fluxocaixa",
  "aud": "fluxocaixa-api",
  "sub": "11111111-1111-1111-1111-111111111111",
  "preferred_username": "demo@loja.com"
}
```

> O `sub` é a chave de todo o isolamento: é ele que a API usa como `ComercianteId`.

> ⚠️ Guarde este token — ele é necessário em todas as chamadas autenticadas a seguir. Validade: **15 minutos**. Se receber `401` no meio do tutorial, basta reexecutar o comando acima.

---

### Passo 5: Listar categorias

Consulte as categorias de lançamento disponíveis no sistema:

```bash
curl -s http://localhost:5000/auth/categorias | jq
```

**Resposta esperada:**

```json
[
  { "id": 1, "nome": "Alimentação" },
  { "id": 2, "nome": "Transporte" },
  { "id": 3, "nome": "Vendas" },
  { "id": 4, "nome": "Serviços" },
  { "id": 5, "nome": "Outros" }
]
```

> 📌 Anote os `id`s — você usará esses valores ao registrar lançamentos.

---

### Passo 6: Registrar lançamentos

Agora vamos registrar movimentações financeiras do comerciante. Criaremos **3 lançamentos** — dois de **crédito** (entradas) e um de **débito** (saída).

#### 6.1 — Crédito: Venda no cartão (categoria "Vendas")

```bash
curl -s -X POST http://localhost:5000/lancamentos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tipo": "credito",
    "valor": 1500.00,
    "categoriaId": 3,
    "descricao": "Venda no cartão - Cliente Maria"
  }' | jq
```

**Resposta esperada:**

```json
{
  "id": "...",
  "tipo": "credito",
  "valor": 1500.00,
  "descricao": "Venda no cartão - Cliente Maria",
  "data": "2026-04-14T..."
}
```

#### 6.2 — Crédito: Prestação de serviço (categoria "Serviços")

```bash
curl -s -X POST http://localhost:5000/lancamentos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tipo": "credito",
    "valor": 850.00,
    "categoriaId": 4,
    "descricao": "Consultoria técnica - Empresa ABC"
  }' | jq
```

#### 6.3 — Débito: Pagamento de fornecedor (categoria "Alimentação")

```bash
curl -s -X POST http://localhost:5000/lancamentos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tipo": "debito",
    "valor": 620.50,
    "categoriaId": 1,
    "descricao": "Pagamento fornecedor de insumos"
  }' | jq
```

> 📊 **Resumo dos lançamentos criados:**
>
> | # | Tipo | Valor | Categoria | Descrição |
> |---|---|---|---|---|
> | 1 | Crédito | R$ 1.500,00 | Vendas | Venda no cartão - Cliente Maria |
> | 2 | Crédito | R$ 850,00 | Serviços | Consultoria técnica - Empresa ABC |
> | 3 | Débito | R$ 620,50 | Alimentação | Pagamento fornecedor de insumos |

---

### Passo 7: Consultar lançamentos do dia

Liste todos os lançamentos registrados em uma data específica (com paginação):

```bash
curl -s "http://localhost:5000/lancamentos?data=2026-04-14&pagina=1&tamanhoPagina=20" \
  -H "Authorization: Bearer $TOKEN" | jq
```

**Resposta esperada:**

```json
{
  "dados": [
    {
      "id": "...",
      "tipo": "credito",
      "valor": 1500.00,
      "descricao": "Venda no cartão - Cliente Maria",
      "categoria": "Vendas",
      "data": "2026-04-14T..."
    },
    {
      "id": "...",
      "tipo": "credito",
      "valor": 850.00,
      "descricao": "Consultoria técnica - Empresa ABC",
      "categoria": "Serviços",
      "data": "2026-04-14T..."
    },
    {
      "id": "...",
      "tipo": "debito",
      "valor": 620.50,
      "descricao": "Pagamento fornecedor de insumos",
      "categoria": "Alimentação",
      "data": "2026-04-14T..."
    }
  ],
  "pagina": 1,
  "tamanhoPagina": 20,
  "totalRegistros": 3
}
```

> 💡 Altere o parâmetro `data` para a data atual (formato `AAAA-MM-DD`) se os lançamentos foram criados hoje.

---

### Passo 8: Consultar saldo consolidado

Verifique o saldo consolidado do dia, que agrega créditos e débitos:

```bash
curl -s "http://localhost:5000/consolidado/diario?data=2026-04-14" \
  -H "Authorization: Bearer $TOKEN" | jq
```

**Resposta esperada:**

```json
{
  "data": "2026-04-14",
  "totalCreditos": 2350.00,
  "totalDebitos": 620.50,
  "saldoLiquido": 1729.50,
  "quantidadeLancamentos": 3
}
```

> ✅ **Conferência:**
> - **Total de créditos:** R$ 1.500,00 + R$ 850,00 = **R$ 2.350,00**
> - **Total de débitos:** R$ 620,50
> - **Saldo líquido:** R$ 2.350,00 − R$ 620,50 = **R$ 1.729,50**

---

### Passo 9: Testar segurança

#### 9.1 — Requisição sem token → HTTP 401

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/lancamentos
```

**Resposta esperada:** `401`

#### 9.2 — Requisição com token inválido → HTTP 401

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/lancamentos \
  -H "Authorization: Bearer token_invalido_aqui"
```

**Resposta esperada:** `401`

#### 9.3 — Requisição com categoria inexistente → HTTP 400

```bash
curl -s -X POST http://localhost:5000/lancamentos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "tipo": "credito",
    "valor": 100.00,
    "categoriaId": 9999,
    "descricao": "Categoria inválida"
  }' | jq
```

**Resposta esperada (HTTP 400):**

```json
{
  "sucesso": false,
  "erros": ["Categoria não encontrada"]
}
```

#### 9.4 — Isolamento entre comerciantes

O consolidado é **por comerciante**. Crie uma segunda conta e confirme que ela não enxerga o movimento da primeira:

```bash
# Token de um comerciante novo, sem nenhum lançamento
TOKEN2=$(curl -s -X POST \
  http://localhost:8081/realms/fluxocaixa/protocol/openid-connect/token \
  -d 'client_id=fluxocaixa-testes' \
  -d 'grant_type=password' \
  -d 'username=comerciante-b@teste.com' \
  -d 'password=Senha123!' | jq -r .access_token)

# Mesma data em que o primeiro comerciante lançou
curl -s "http://localhost:5000/consolidado/diario?data=2026-04-14" \
  -H "Authorization: Bearer $TOKEN2" | jq
```

**Resposta esperada:** `200 OK` com tudo zerado — nunca os valores do outro comerciante.

```json
{
  "data": "2026-04-14",
  "totalCreditos": 0.00,
  "totalDebitos": 0.00,
  "saldoLiquido": 0.00,
  "quantidadeLancamentos": 0
}
```

> 🔐 O `ComercianteId` vem sempre da claim `sub` do JWT — nunca do corpo ou da query string, que o cliente controla. No banco, a chave primária de `consolidado_diario` é `(comerciante_id, data)`.

---

### Passo 10: Verificar isolamento (requisito não-funcional)

Um requisito importante da arquitetura é que o **serviço de lançamentos não depende diretamente do serviço de consolidação**. A comunicação entre eles é feita via **RabbitMQ** (mensageria assíncrona).

Para comprovar isso:

1. **Simule a parada do consumidor de consolidação.** Você pode fazer isso parando o container ou processo responsável pelo consolidado:

   ```bash
   # Identifique o container do consumidor (se separado) ou simule pausando:
   docker compose pause rabbitmq
   ```

2. **Registre um novo lançamento** (como no Passo 6). A requisição **deve retornar sucesso (HTTP 200/201)**, pois o lançamento é salvo no banco e a mensagem é enfileirada no RabbitMQ.

3. **Retome o RabbitMQ:**

   ```bash
   docker compose unpause rabbitmq
   ```

4. **Consulte o consolidado** (Passo 8) — o novo lançamento será processado e refletido no saldo.

> 🏗️ **Por que isso importa?** Em um cenário real, se o serviço de consolidação sair do ar temporariamente, os comerciantes continuam registrando vendas normalmente. Quando o consolidado voltar, ele processa todas as mensagens da fila. Isso garante **resiliência** e **disponibilidade**.

---

### Passo 11: Explorar o contrato OpenAPI

A API publica seu contrato OpenAPI gerado automaticamente a partir dos endpoints:

1. Baixe o documento: **http://localhost:5000/openapi/v1.json**
2. Inspecione os endpoints e os schemas de request/response:

```bash
curl -s http://localhost:5000/openapi/v1.json | jq ".paths | keys"
```

3. Para navegar visualmente ou disparar chamadas, importe esse JSON em qualquer cliente OpenAPI — Postman, Insomnia, Bruno ou a extensão REST Client do VS Code.

> 💡 A API serve o **contrato**, não uma UI. A geração é nativa do .NET 10 (`Microsoft.AspNetCore.OpenApi`), sem pacote de interface embarcado — o que mantém a superfície HTTP do serviço restrita a endpoints de negócio e health.

---

### Passo 12: Executar testes automatizados

Se você tiver o **.NET SDK** instalado localmente, pode rodar os testes:

#### Testes unitários

```bash
dotnet test tests/FluxoDeCaixa.Tests.Unit
```

#### Testes de integração

```bash
dotnet test tests/FluxoDeCaixa.Tests.Integration
```

> ⚠️ Os testes de integração podem exigir que o ambiente Docker esteja rodando, pois se conectam ao banco e ao broker reais.

---

### Passo 13: Limpar o ambiente

Quando terminar os testes, remova todos os containers e volumes:

```bash
docker compose down -v
```

Isso irá:
- ✅ Parar e remover todos os containers
- ✅ Remover os volumes (dados do PostgreSQL e RabbitMQ)
- ✅ Liberar as portas 5000, 5010, 5433, 5672 e 15672

---

## 🖥️ Seção PowerShell — Comandos Equivalentes

Para quem prefere usar **PowerShell** no Windows, seguem os comandos equivalentes usando `Invoke-RestMethod`.

### Registrar usuário

O cadastro é feito no Keycloak (tela de **Register** em http://localhost:5010 → **Entrar**), não pela API.

### Obter token no Keycloak

```powershell
$form = @{
    client_id  = "fluxocaixa-testes"
    grant_type = "password"
    username   = "demo@loja.com"
    password   = "Senha123!"
}

$response = Invoke-RestMethod `
    -Uri "http://localhost:8081/realms/fluxocaixa/protocol/openid-connect/token" `
    -Method POST `
    -Body $form

$TOKEN = $response.access_token
Write-Host "Token: $TOKEN"
```

### Listar categorias

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/auth/categorias"
```

### Registrar lançamento (crédito)

```powershell
$lancamento = @{
    tipo        = "credito"
    valor       = 1500.00
    categoriaId = 3
    descricao   = "Venda no cartão - Cliente Maria"
} | ConvertTo-Json

$headers = @{ Authorization = "Bearer $TOKEN" }

Invoke-RestMethod -Uri "http://localhost:5000/lancamentos" `
    -Method POST `
    -ContentType "application/json" `
    -Headers $headers `
    -Body $lancamento
```

### Registrar lançamento (débito)

```powershell
$lancamento = @{
    tipo        = "debito"
    valor       = 620.50
    categoriaId = 1
    descricao   = "Pagamento fornecedor de insumos"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/lancamentos" `
    -Method POST `
    -ContentType "application/json" `
    -Headers $headers `
    -Body $lancamento
```

### Consultar lançamentos do dia

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/lancamentos?data=2026-04-14&pagina=1&tamanhoPagina=20" `
    -Headers $headers
```

### Consultar saldo consolidado

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/consolidado/diario?data=2026-04-14" `
    -Headers $headers
```

### Testar requisição sem token (segurança)

```powershell
try {
    Invoke-RestMethod -Uri "http://localhost:5000/lancamentos"
} catch {
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.Value__)"
    # Esperado: 401
}
```

### Testar token inválido

```powershell
try {
    $badHeaders = @{ Authorization = "Bearer token_invalido_aqui" }
    Invoke-RestMethod -Uri "http://localhost:5000/lancamentos" `
        -Headers $badHeaders
} catch {
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.Value__)"
    # Esperado: 401
}
```

### Limpar ambiente

```powershell
docker compose down -v
```

---

## 📋 Resumo Rápido dos Endpoints

| Método | Endpoint | Autenticação | Descrição |
|---|---|---|---|
| — | Registro e login | — | **No Keycloak** (http://localhost:8081), não na API |
| `GET` | `/auth/categorias` | ❌ Não | Listar categorias |
| `POST` | `/lancamentos` | ✅ Bearer | Registrar lançamento |
| `GET` | `/lancamentos?data=...` | ✅ Bearer | Listar lançamentos por data |
| `GET` | `/consolidado/diario?data=...` | ✅ Bearer | Saldo consolidado do dia (do comerciante do token) |
| `GET` | `/health/live` | ❌ Não | Liveness probe |
| `GET` | `/health/ready` | ❌ Não | Readiness probe |

---

## 🔗 Links Úteis

| Recurso | URL |
|---|---|
| Interface Web | http://localhost:5010 |
| API REST | http://localhost:5000 |
| Contrato OpenAPI (JSON) | http://localhost:5000/openapi/v1.json |
| Painel RabbitMQ | http://localhost:15672 (guest/guest) |
| Console do Keycloak | http://localhost:8081 (admin/admin) |
| Health / readiness | http://localhost:5000/health/ready |

---

> 📝 **Nota:** As respostas JSON exatas podem variar conforme a versão do sistema. Os exemplos acima representam a estrutura esperada.
