# ❓ FAQ — Decisões Arquiteturais do Fluxo de Caixa

> Perguntas frequentes sobre as escolhas técnicas do sistema de controle de fluxo de caixa para comerciantes.

---

## 📦 Event Sourcing e Marten

### 1. Por que Event Sourcing ao invés de CRUD?

Event Sourcing garante **imutabilidade**: cada lançamento é um evento append-only, impossível de alterar ou apagar silenciosamente. Isso proporciona auditoria completa e natural — o histórico *é* o dado, não um log secundário. Além disso, o estado atual pode ser reconstruído a qualquer momento fazendo replay dos eventos, o que permite corrigir bugs de projeção sem perda de dados.

### 2. Por que Marten ao invés de EventStoreDB?

Marten roda sobre **PostgreSQL**, eliminando a necessidade de um banco de dados adicional na infraestrutura. Com ele, temos document store e event store no mesmo banco, simplificando backup, monitoramento e operação. Para o escopo deste desafio, um único PostgreSQL atende perfeitamente sem sacrificar funcionalidades de event sourcing.

### 3. E se precisar reconstruir o consolidado?

Basta fazer **replay dos eventos** armazenados no Marten, reprojetando as projeções de consolidação do zero. O consumer de consolidação é idempotente — processar o mesmo evento mais de uma vez não gera dados duplicados. Isso torna a reconstrução segura e determinística.

---

## 🏗️ Clean Architecture

### 4. Por que Clean Architecture ao invés de N-Camadas?

Clean Architecture direciona todas as dependências **para o domínio**, mantendo regras de negócio isoladas de frameworks e infraestrutura. Isso maximiza a testabilidade (testes unitários sem banco, sem broker) e permite trocar componentes de infra (ex.: mudar de RabbitMQ para Kafka) sem impactar a camada de negócio.

### 5. Por que separar Consolidação como projeto próprio?

O serviço de consolidação tem **responsabilidade e ciclo de vida distintos** do serviço de lançamentos. Separando-os, cada um pode escalar independentemente — se a consolidação estiver sobrecarregada, escalamos apenas ela. Esse isolamento também impede que uma falha na consolidação derrube o registro de lançamentos.

---

## 🐇 RabbitMQ e Mensageria

### 6. Por que RabbitMQ ao invés de comunicação síncrona?

É um **requisito do desafio**: o serviço de lançamentos não pode ficar indisponível se o serviço de consolidação cair. Com mensageria assíncrona via RabbitMQ, os lançamentos são publicados em uma fila e processados quando o consolidado estiver disponível, garantindo desacoplamento total.

### 7. O que acontece se o RabbitMQ cair?

Adotamos o padrão **Persist-then-Publish**: o evento é primeiro persistido no Marten (PostgreSQL) e só depois publicado no RabbitMQ. Se o broker estiver fora, o evento já está salvo e o consumer pode reprocessá-lo quando o RabbitMQ voltar. Nenhum lançamento é perdido.

### 8. Por que MassTransit ao invés de usar RabbitMQ direto?

MassTransit oferece **abstrações poderosas** sobre o RabbitMQ: retry policies configuráveis, error queues automáticas, gerenciamento do ciclo de vida dos consumers e serialização padronizada. Isso reduz código boilerplate e permite trocar o transport (ex.: para Amazon SQS) sem reescrever a lógica de mensageria.

---

## 🔐 Segurança

### 9. Por que Keycloak e não autenticação própria?

Porque **identidade é capacidade genérica** — não é onde este negócio se diferencia. Autenticar na própria API (tabela `usuarios`, BCrypt, JWT HS256) é barato de escrever, mas carrega quatro dívidas estruturais: sem revogação, HS256 impedindo que um parceiro valide o token sem receber a chave que assina, sem MFA e sem política de senha. Nenhuma delas é corrigível sem trocar a decisão de base — todas decorrem de a aplicação ser a autoridade de identidade.

Delegando a um IdP, as quatro desaparecem: a API não guarda senha, não emite token e não tem tabela de usuários. Ver [ADR-07](ARCHITECTURE.md#adr-07-keycloak-como-identity-provider-oidc).

### 10. Por que Keycloak e não Cognito/Auth0?

Para o desafio, três razões: **roda em `docker compose`** (o avaliador sobe tudo com um comando, sem conta em nuvem), é **open source Apache 2.0** (custo de licença zero) e **não gera vendor lock-in** — o protocolo é OIDC padrão, então trocar por Cognito ou Entra ID é mudar configuração, não código.

O trade-off é operacional: um IdP gerenciado não precisa ser mantido por você. Em produção, essa escolha seria reavaliada conforme o tamanho da equipe — está registrada em [docs/ARQUITETURA-ALVO.md](docs/ARQUITETURA-ALVO.md).

### 11. Por que Authorization Code + PKCE e não senha direto na API?

Porque num SPA não existe lugar seguro para guardar client secret, e o *Resource Owner Password Credentials* (ROPC) é desencorajado no OAuth 2.1 — ele exige que a aplicação veja a senha do usuário. Com PKCE, a senha é digitada **na página do Keycloak** e o código nunca a toca; o `code_verifier` impede que um código interceptado seja trocado por token.

O realm tem um cliente separado (`fluxocaixa-testes`) com ROPC habilitado **apenas** para automação de testes — mantendo o cliente do browser limpo.

### 12. O `sub` do token vira o `ComercianteId`?

Sim. O `sub` é o identificador estável do usuário no Keycloak, e é ele que escopa todos os dados — a PK do consolidado é `(comerciante_id, data)`. O `ComercianteId` **nunca** vem do corpo ou da query string, que o cliente controla.

Detalhe prático: no realm, o usuário `demo@loja.com` tem `id` fixo `1111...1111`, que é exatamente o `comerciante_id` do `seed-data.sql`. É isso que faz o consolidado semeado aparecer ao logar com ele.

---

## 🖥️ Frontend

### 13. Por que Blazor WASM?

Blazor WASM permite desenvolvimento **C# end-to-end**, eliminando a necessidade de JavaScript e permitindo reutilizar DTOs e validações entre frontend e backend. A equipe trabalha com uma única linguagem e stack, reduzindo o custo cognitivo e facilitando manutenção.

### 14. Por que MudBlazor?

MudBlazor oferece uma biblioteca completa de componentes **Material Design** prontos para uso — DataGrid, Dialogs, Forms, Charts — acelerando o desenvolvimento do frontend. Os componentes são bem documentados, responsivos e customizáveis, cobrindo as necessidades de dashboards e formulários do fluxo de caixa.

### 15. Por que nginx no Docker para o frontend?

Blazor WASM compila para **arquivos estáticos** (HTML, CSS, JS, DLLs .NET). O nginx serve esses arquivos com compressão gzip, cache headers e rewrite rules para SPA routing (fallback para `index.html`). É leve, performático e é o padrão de mercado para servir SPAs em containers.

---

## 🐳 Infraestrutura

### 16. Por que Docker Compose?

Docker Compose garante um **ambiente reproduzível**: Keycloak, API, consolidação, PostgreSQL e RabbitMQ sobem com um único `docker compose up --wait` — inclusive o realm do IdP, importado automaticamente. Elimina o problema de "funciona na minha máquina" e permite que qualquer avaliador rode o projeto completo em minutos.

### 17. E se quiser ir para produção?

A arquitetura já está preparada para evoluir: **Kubernetes** para orquestração, managed PostgreSQL (RDS/Cloud SQL) para o banco, CloudAMQP ou Amazon MQ para o broker, e um pipeline de CI/CD para deploys automatizados. A transição é incremental graças à containerização e ao desacoplamento dos serviços.

### 18. Como escalar horizontalmente?

A API é **stateless** (valida o token por chave pública, sem sessão), permitindo múltiplas instâncias atrás de um load balancer. O RabbitMQ suporta competing consumers — basta adicionar mais instâncias do serviço de consolidação. Para leitura, PostgreSQL read replicas distribuem a carga de queries do consolidado.

---

## 🏆 Diferenciais do Desafio

### 19. Arquitetura de transição para sistemas legados?

Adotamos **Strangler Fig** em três fases: (1) coexistência, com CDC replicando o legado e uma **Anti-Corruption Layer** traduzindo para o evento `LancamentoRegistrado` — o consolidado novo roda em shadow e é comparado diariamente; (2) inversão da escrita por canário, com sincronismo reverso enquanto houver periférico lendo do legado; (3) desativação. Cada fase tem critério de avanço e de rollback definidos.

📄 Diagramas por fase, critérios e prazos em **[docs/ARQUITETURA-ALVO.md](docs/ARQUITETURA-ALVO.md#2-arquitetura-de-transição)**.

### 20. Estimativa de custos em cloud?

Em AWS sa-east-1, com 500 comerciantes e o pico de 50 req/s do enunciado: **≈ US$ 651/mês** a preço de tabela, ou **≈ US$ 440/mês** com Savings Plan, Graviton e Fargate Spot no consumer. Homologação custa ~US$ 93/mês e um MVP enxuto ~US$ 95/mês. Licenças de software: **US$ 0** — toda a stack é MIT/Apache 2.0.

📄 Premissas, memória de cálculo por componente e comparativo de alternativas (SQS vs. Amazon MQ, Aurora vs. RDS, Lambda vs. Fargate) em **[docs/CUSTOS.md](docs/CUSTOS.md)**.

### 21. Monitoramento e Observabilidade?

**Implementado:** Serilog em JSON, correlation id propagado de ponta a ponta (HTTP → evento → consumer), health probes `/health/live` e `/health/ready`, e erros sempre em ProblemDetails com o correlation id no corpo.

**Planejado:** OpenTelemetry (traces, métricas e logs via OTLP), com a métrica central sendo o `consolidacao.lag` — o tempo entre o `201` e o saldo atualizado, que transforma a promessa "< 1s" em algo verificável.

📄 SLIs/SLOs com orçamento de erro, alertas e painéis em **[docs/OBSERVABILIDADE.md](docs/OBSERVABILIDADE.md)**.

### 22. Critérios de segurança para consumo dos serviços?

Autenticação por tipo de consumidor (**PKCE para browser — já implementado**, Client Credentials para server-to-server, mTLS interno), escopos mínimos por operação, TLS 1.2+, rate limiting com `429`/`Retry-After`, versionamento na rota e contrato de resiliência exigido do consumidor (timeout, circuit breaker, backoff com jitter).

📄 Modelo de ameaças STRIDE e lacunas conhecidas declaradas em **[docs/SEGURANCA.md](docs/SEGURANCA.md)**.

### 23. Por que o consolidado é por comerciante e não global?

Porque é um requisito de domínio, não de implementação: o saldo de um comerciante não pode se misturar ao de outro. Por isso a chave primária de `consolidado_diario` é `(comerciante_id, data)` e o `ComercianteId` vem sempre da claim `sub` do token — nunca do body ou da query string, que o cliente controla. Coberto pelo teste `Consolidado_de_um_comerciante_nao_vaza_para_outro`.

### 24. O que acontece se a mesma mensagem for entregue duas vezes?

Nada — o saldo não muda. O RabbitMQ entrega *at-least-once* e o MassTransit ainda faz retry, então a reentrega é certeza, não hipótese. O consumer deduplica por `LancamentoId` usando a tabela `lancamentos_consolidados` como inbox: o UPSERT do consolidado só executa se o INSERT no inbox inserir de fato. Sem isso, uma falha transitória do banco somaria o mesmo lançamento duas vezes, de forma permanente e silenciosa — inaceitável em um razão financeiro. Ver ADR-10 e `IdempotenciaConsolidadoTests`.

---

> 📌 *Este documento reflete as decisões para o escopo do desafio técnico. Em produção, cada escolha seria reavaliada conforme requisitos de escala, equipe e orçamento.*
