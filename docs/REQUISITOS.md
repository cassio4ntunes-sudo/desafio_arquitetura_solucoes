# Requisitos Refinados — Funcionais e Não Funcionais

Refinamento dos requisitos a partir do enunciado do desafio. O enunciado traz duas linhas de negócio ("serviço de controle de lançamentos", "serviço do consolidado diário") e um parágrafo de RNF; aqui eles viram requisitos verificáveis, com critério de aceite e com o teste que os cobre.

---

## 1. Requisitos funcionais

| ID | Requisito | Critério de aceite | Prioridade | Verificado por |
|---|---|---|---|---|
| **RF-01** | Registrar lançamento de débito ou crédito | `POST /lancamentos` com valor, tipo, data, descrição e categoria retorna `201` com o id gerado | Must | `LancamentoConsolidadoFlowTests`, `RegistrarLancamentoHandlerTests` |
| **RF-02** | Rejeitar lançamento inválido | Valor ≤ 0, tipo fora de {Credito, Debito} ou categoria desconhecida retornam `400` com os campos em erro | Must | `RegistrarLancamentoValidatorTests` |
| **RF-03** | Lançamento é imutável | Persistido como evento append-only; não há endpoint de alteração ou exclusão | Must | `LancamentoTests`, ADR-01 |
| **RF-04** | Consultar extrato do dia, paginado | `GET /lancamentos?data&pagina&tamanhoPagina` retorna itens do comerciante com metadados de paginação | Must | `ConsultarLancamentosHandlerTests` |
| **RF-05** | Consultar consolidado diário | `GET /consolidado/diario?data` retorna total de créditos, total de débitos, saldo líquido e quantidade | Must | `LancamentoConsolidadoFlowTests` |
| **RF-06** | Dia sem movimento responde saldo zero | Retorna `200` com zeros, não `404` — ausência de lançamento é resposta de negócio válida | Should | `ConsultarConsolidadoDiarioHandlerTests` |
| **RF-07** | Consolidado reflete créditos e débitos somados | Saldo líquido = créditos − débitos, para a data e o comerciante | Must | `Deve_consolidar_lancamento_debito_e_credito_somados` |
| **RF-08** | Autenticar comerciante | Login federado no Keycloak (OIDC Authorization Code + PKCE); a API valida token RS256 e recusa `401` sem ele | Must | `Deve_recusar_acesso_sem_token` |
| **RF-09** | Isolar dados entre comerciantes | Nenhum comerciante lê lançamento ou consolidado de outro, em nenhum endpoint | Must | `Consolidado_de_um_comerciante_nao_vaza_para_outro` |
| **RF-10** | Classificar lançamento por categoria | Categoria de lista fechada validada no backend; `GET /auth/categorias` publica as opções | Should | `RegistrarLancamentoValidatorTests` |
| **RF-11** | Interface web para o comerciante | SPA com login, dashboard de saldo e registro/consulta de lançamentos | Could | Manual — ver [TUTORIAL.md](../TUTORIAL.md) |

---

## 2. Requisitos não funcionais

O enunciado define dois RNF explícitos (RNF-01 e RNF-02). Os demais foram derivados do domínio — um sistema financeiro multi-inquilino impõe requisitos que o enunciado assume sem declarar.

### 2.1 Disponibilidade e resiliência

| ID | Requisito | Meta | Como é atingido | Verificado por |
|---|---|---|---|---|
| **RNF-01** | *(enunciado)* O controle de lançamentos não fica indisponível se a consolidação cair | Escrita disponível com o consumer 100% fora | Comunicação exclusivamente assíncrona via RabbitMQ; a escrita nunca chama a consolidação | `Escrita_permanece_disponivel_independente_da_consolidacao`; ADR-02 |
| **RNF-03** | Falha transitória no consumer não perde lançamento | Zero perda | Retry 500ms/2s/10s e depois error queue; o evento permanece no event store como fonte da verdade | ADR-02, ADR-04 |
| **RNF-04** | Reentrega de mensagem não corrompe o saldo | Aplicação idempotente | Inbox `lancamentos_consolidados` com PK no `LancamentoId`: reprocessar é no-op | `IdempotenciaConsolidadoTests` |
| **RNF-05** | Orquestrador só roteia tráfego para instância pronta | Probe responde em < 1s | `/health/live` e `/health/ready`; readiness valida o PostgreSQL | Healthcheck do compose e smoke test do CI |

### 2.2 Desempenho e escalabilidade

| ID | Requisito | Meta | Como é atingido | Verificado por |
|---|---|---|---|---|
| **RNF-02** | *(enunciado)* Consolidado suporta pico de 50 req/s com no máximo 5% de perda | ≥ 50 req/s, erro ≤ 5%, por 2 min | Read model materializado: `SELECT` por chave primária `(comerciante_id, data)`, sem agregação em tempo de consulta | `ConsolidadoLoadTests` (NBomber) |
| **RNF-06** | Consistência eventual do consolidado dentro de janela aceitável | p95 < 1s entre o `201` e o saldo atualizado | Consumer assíncrono dedicado | Polling nos testes de integração |
| **RNF-07** | API escala horizontalmente | Sem estado no processo | Token stateless validado por chave pública; estado só no PostgreSQL e no RabbitMQ; consumers competem pela mesma fila | ADR-07 |
| **RNF-08** | Extrato não degrada com volume | Resposta paginada sempre limitada | Paginação obrigatória em `GET /lancamentos` | `ConsultarLancamentosHandlerTests` |

### 2.3 Segurança

| ID | Requisito | Meta | Como é atingido |
|---|---|---|---|
| **RNF-09** | Endpoints de negócio exigem autenticação | 100% dos endpoints de dados | Política `comerciante` nos grupos de rota; token RS256 validado contra o JWKS |
| **RNF-10** | Segredo de assinatura nunca na aplicação | Zero segredos versionados | A chave privada vive no Keycloak; a API falha ao subir sem `Keycloak:Authority` |
| **RNF-11** | Senha nunca em claro nem na aplicação | Credencial só no IdP | Keycloak (hash e política no realm) |
| **RNF-12** | Superfície de abuso limitada | 429 acima do limite | Rate limiting: 60 req/s no consolidado, 10/min no auth |
| **RNF-13** | Origens de browser controladas | Lista explícita | CORS por configuração, sem `AllowAnyOrigin` |

Detalhamento e modelo de ameaças em [SEGURANCA.md](SEGURANCA.md).

### 2.4 Observabilidade e operação

| ID | Requisito | Meta | Como é atingido |
|---|---|---|---|
| **RNF-14** | Log estruturado e correlacionável | 100% das requisições com correlation id | Serilog em JSON + `CorrelationIdMiddleware` |
| **RNF-15** | Erro nunca retorna corpo vazio | Todo erro em ProblemDetails (RFC 7807) | Handler global com correlation id no corpo do 500 |
| **RNF-16** | Auditabilidade do saldo | Qualquer saldo reconstruível | Event store append-only é a fonte da verdade |

Plano completo de métricas, tracing e alertas em [OBSERVABILIDADE.md](OBSERVABILIDADE.md).

### 2.5 Manutenibilidade

| ID | Requisito | Meta | Como é atingido |
|---|---|---|---|
| **RNF-17** | Domínio isolado de infraestrutura | Zero dependência externa no projeto Domain | Clean Architecture; `FluxoDeCaixa.Domain` sem PackageReference |
| **RNF-18** | Build e testes automatizados | CI verde a cada push | GitHub Actions: build, unit, integração (Testcontainers) e smoke E2E |
| **RNF-19** | Subir o ambiente em um comando | `docker compose up` | 6 containers com healthcheck e ordenação por dependência |

---

## 3. Fora de escopo (decisão consciente)

| Item | Motivo |
|---|---|
| Estorno e fechamento de dia | Exige modelagem de domínio maior que o tempo disponível — ver [DOMINIOS-E-CAPACIDADES.md](DOMINIOS-E-CAPACIDADES.md#6-capacidades-não-implementadas-e-por-quê) |
| Outbox transacional | Trade-off assumido e documentado em ADR-04; mitigação proposta em [ARQUITETURA-ALVO.md](ARQUITETURA-ALVO.md) |
| Serviços em processos separados | Monólito modular com desacoplamento assíncrono cumpre o RNF-01 a um custo operacional menor |
| Migrations versionadas (DbUp/EF) | DDL idempotente no startup atende o escopo local; é pré-requisito para produção |
| Multi-moeda, previsão de fluxo | Roadmap |
| MFA (OTP) | Suportado pelo Keycloak; não ativado para não atrapalhar a avaliação |

---

## 4. Rastreabilidade: enunciado → requisito

| Trecho do enunciado | Requisitos |
|---|---|
| "Serviço que faça o controle de lançamentos" | RF-01, RF-02, RF-03, RF-04, RF-10 |
| "Serviço do consolidado diário" | RF-05, RF-06, RF-07 |
| "O serviço de controle de lançamento não deve ficar indisponível se o sistema de consolidado diário cair" | **RNF-01**, RNF-03, RNF-04 |
| "50 requisições por segundo, com no máximo 5% de perda" | **RNF-02**, RNF-07 |
| "Mapeamento de domínios funcionais e capacidades de negócio" | [DOMINIOS-E-CAPACIDADES.md](DOMINIOS-E-CAPACIDADES.md) |
| "Desenho da solução completo (Arquitetura Alvo)" | [ARQUITETURA-ALVO.md](ARQUITETURA-ALVO.md), [../ARCHITECTURE.md](../ARCHITECTURE.md) |
| "Justificativa na decisão/escolha de ferramentas" | ADRs em [../ARCHITECTURE.md](../ARCHITECTURE.md), [../FAQ.md](../FAQ.md) |
| "Testes" | Coluna "Verificado por" das tabelas acima |
| "Arquitetura de Transição" *(diferencial)* | Resumo em [../FAQ.md](../FAQ.md#19-arquitetura-de-transição-para-sistemas-legados) |
| "Estimativa de custos" *(diferencial)* | [CUSTOS.md](CUSTOS.md) |
| "Monitoramento e Observabilidade" *(diferencial)* | [OBSERVABILIDADE.md](OBSERVABILIDADE.md) |
| "Critérios de segurança para consumo de serviços" *(diferencial)* | [SEGURANCA.md](SEGURANCA.md) |
