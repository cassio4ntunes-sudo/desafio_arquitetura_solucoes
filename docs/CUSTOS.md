# Estimativa de Custos — Infraestrutura e Licenças

Estimativa para a [arquitetura alvo](ARQUITETURA-ALVO.md) em AWS, região **sa-east-1 (São Paulo)**, preços de tabela sob demanda vigentes em janeiro/2025, em USD/mês.

> **Como ler:** os números servem para comparar alternativas e dimensionar ordem de grandeza, não para fechar orçamento. Preços de nuvem mudam e variam com uso real; as premissas estão explícitas para que a conta possa ser refeita.

---

## Premissas de carga

| Parâmetro | Valor | Origem |
|---|---|---|
| Comerciantes ativos | 500 | Premissa de negócio |
| Lançamentos por comerciante/dia | 40 | Premissa de negócio |
| Escrita | ~20 mil/dia ≈ 0,25 req/s médio, pico 5 req/s | Derivado |
| Leitura do consolidado | **50 req/s no pico** (~4 h/dia), 5 req/s fora do pico | **Enunciado do desafio** |
| Volume de dados | ~7,3 M eventos/ano ≈ 12 GB/ano com índices | Derivado |
| Retenção | 5 anos em base quente (exigência fiscal brasileira) | Premissa regulatória |
| Disponibilidade alvo | 99,9% (≈ 43 min/mês) | Premissa de SLA |

---

## Cenário 1 — Produção com HA (arquitetura alvo)

| Componente | Configuração | USD/mês |
|---|---|---|
| **ECS Fargate — Lançamentos** | 2 tasks 0,5 vCPU / 1 GB, 24×7 | 36 |
| **ECS Fargate — Consolidação** | 2 tasks 0,5 vCPU / 1 GB + autoscale (~20% do tempo em 4 tasks) | 43 |
| **RDS PostgreSQL** | `db.t4g.medium` Multi-AZ, 100 GB gp3 | 175 |
| **RDS — read replica** | `db.t4g.small` (leitura do consolidado) | 62 |
| **Amazon MQ (RabbitMQ)** | `mq.t3.micro` cluster 2 nós | 58 |
| **ElastiCache Redis** | `cache.t4g.micro` | 24 |
| **Application Load Balancer** | 1 ALB + ~25 LCU | 36 |
| **API Gateway** | ~6 M chamadas/mês | 21 |
| **CloudFront + S3** | SPA estática, ~50 GB egresso | 14 |
| **Keycloak (ECS Fargate)** | 1 task 0,5 vCPU / 1 GB + RDS `db.t4g.micro` dedicado | 44 |
| **Secrets Manager** | 6 segredos + chamadas | 3 |
| **CloudWatch** | Logs (~30 GB), métricas, alarmes | 42 |
| **X-Ray** | Tracing com 10% de amostragem | 8 |
| **Backup / snapshots** | 200 GB | 19 |
| **Transferência de dados** | Inter-AZ + saída | 25 |
| | **Subtotal** | **610** |
| | *Contingência 15%* | *92* |
| | **Total** | **≈ 702 USD/mês** |

**Custo unitário:** ≈ US$ 1,40 por comerciante/mês (≈ R$ 7,70 a R$ 5,50/USD).

### Otimizações aplicáveis

| Ação | Economia | Trade-off |
|---|---|---|
| Savings Plan de 1 ano (Fargate + RDS) | −30% ≈ −US$ 160 | Compromisso de 12 meses |
| Graviton (t4g) em toda a stack | Já aplicado | Nenhum |
| Fargate Spot na Consolidação | −US$ 20 | Interrupção tolerável: o consumer é idempotente e a fila retém |
| Logs para S3 + Athena após 7 dias | −US$ 25 | Consulta ao histórico fica mais lenta |
| **Total otimizado** | | **≈ 485 USD/mês** |

---

## Cenário 2 — Homologação

Instância única, sem Multi-AZ, desligada fora do horário comercial (12 h/dia, 5 dias/semana ≈ 36% do tempo).

| Componente | USD/mês |
|---|---|
| ECS Fargate (2 tasks, horário reduzido) | 14 |
| RDS `db.t4g.small` single-AZ | 30 |
| Amazon MQ `mq.t3.micro` nó único | 19 |
| Keycloak (1 task Fargate, horário reduzido) | 12 |
| ALB + CloudWatch + outros | 30 |
| **Total** | **≈ 105 USD/mês** |

---

## Cenário 3 — MVP enxuto (validação de mercado)

Para os primeiros meses, sem SLA formal. Mostra que a arquitetura não exige o alvo completo desde o dia um.

| Componente | Configuração | USD/mês |
|---|---|---|
| ECS Fargate | 2 tasks mínimas (MS + WKR), sem autoscaling | 30 |
| RDS `db.t4g.micro` single-AZ | 20 GB | 26 |
| RabbitMQ em container no mesmo ECS | — | 9 |
| Keycloak em container no mesmo ECS | — | 9 |
| ALB + CloudWatch + S3/CloudFront | | 42 |
| **Total** | | **≈ 116 USD/mês** |

O código não muda entre o Cenário 3 e o Cenário 1 — muda a topologia de deploy. É o retorno concreto de ter separado os domínios em projetos com contrato assíncrono desde o início.

---

## Comparativo de alternativas

| Decisão | Alternativa | Custo alternativo | Por que a escolha atual |
|---|---|---|---|
| Amazon MQ (RabbitMQ) | Amazon SQS | ~US$ 3/mês (−95%) | SQS seria mais barato, mas o RabbitMQ entrega roteamento por tópico, dead-letter nativo e portabilidade entre nuvens. Em escala maior, a migração para SQS é uma troca de transporte no MassTransit — o código de domínio não muda |
| RDS PostgreSQL | Aurora Serverless v2 | ~US$ 290/mês | Aurora escala melhor em carga irregular; nesta carga previsível o RDS custa menos |
| PostgreSQL + Marten | EventStoreDB gerenciado | ~US$ 400/mês | Marten entrega event store e document store sobre um único PostgreSQL — um motor a menos para operar e pagar |
| ECS Fargate | EKS | +US$ 73/mês (control plane) + operação | Kubernetes não se paga com 2 serviços |
| ECS Fargate | Lambda | ~US$ 35/mês | Lambda seria mais barato na escrita, mas cold start prejudica o p95 do pico de leitura e o consumer de fila fica menos natural |

---

## Licenças de software

| Item | Licença | Custo |
|---|---|---|
| .NET 10, ASP.NET Core, Blazor | MIT | **0** |
| **Keycloak** | Apache 2.0 | **0** |
| PostgreSQL 16 | PostgreSQL License | **0** |
| RabbitMQ | MPL 2.0 | **0** |
| Marten, MassTransit, Serilog, FluentValidation, NBomber | Apache 2.0 / MIT | **0** |
| MediatR | Apache 2.0 | **0** (ver nota) |
| MudBlazor | MIT | **0** |
| xUnit, FluentAssertions, NSubstitute, Testcontainers | Apache 2.0 / MIT | **0** |
| **Total de licenças** | | **US$ 0/mês** |

> **Nota sobre sustentabilidade de dependências:** MediatR e MassTransit anunciaram modelos comerciais para versões futuras, e o FluentAssertions alterou seu licenciamento na v8. Nenhum impacto nas versões usadas aqui, mas é risco a monitorar. Mitigação: MediatR é uma abstração fina e substituível (os handlers são classes comuns); o MassTransit isola o transporte, que é justamente o que se quer trocar sem custo.

**Ferramental de time (fora da infraestrutura):** GitHub Team ~US$ 4/usuário/mês; GitHub Actions com 2.000 min/mês gratuitos — suficiente para este pipeline.

---

## Resumo

| Cenário | USD/mês | BRL/mês (≈ 5,50) |
|---|---|---|
| MVP enxuto | 116 | R$ 638 |
| Homologação | 105 | R$ 578 |
| Produção HA (tabela) | 702 | R$ 3.861 |
| Produção HA (otimizada) | 485 | R$ 2.668 |
| **Produção + homologação, otimizadas** | **590** | **R$ 3.245** |
