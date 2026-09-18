-- Seed de demonstração para o avaliador.
-- Roda via docker-entrypoint-initdb.d na primeira subida do container Postgres.
-- O DDL abaixo espelha PostgresConsolidadoRepository.CriarTabelaSeNaoExisteAsync.
--
-- Não há tabela de usuários: identidade é responsabilidade do Keycloak
-- (ver keycloak/realm-fluxocaixa.json). O comerciante_id abaixo é exatamente
-- o `sub` do usuário demo@loja.com no realm — é o que liga o token a estes dados.

-- O consolidado é por comerciante: a chave primária composta garante que o
-- saldo de um comerciante nunca se misture ao de outro.
CREATE TABLE IF NOT EXISTS consolidado_diario (
    comerciante_id UUID            NOT NULL,
    data           DATE            NOT NULL,
    total_creditos NUMERIC(19, 2)  NOT NULL DEFAULT 0,
    total_debitos  NUMERIC(19, 2)  NOT NULL DEFAULT 0,
    saldo_liquido  NUMERIC(19, 2)  NOT NULL DEFAULT 0,
    quantidade     INTEGER         NOT NULL DEFAULT 0,
    atualizado_em  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    PRIMARY KEY (comerciante_id, data)
);

-- Inbox de idempotência: garante que a reentrega da mesma mensagem pelo
-- RabbitMQ (at-least-once) não some o lançamento duas vezes no consolidado.
CREATE TABLE IF NOT EXISTS lancamentos_consolidados (
    lancamento_id UUID        PRIMARY KEY,
    aplicado_em   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 7 dias de fluxo de caixa realista para demo@loja.com
-- (sub = 11111111-1111-1111-1111-111111111111 no realm do Keycloak)
INSERT INTO consolidado_diario (comerciante_id, data, total_creditos, total_debitos, saldo_liquido, quantidade, atualizado_em)
VALUES
    ('11111111-1111-1111-1111-111111111111', CURRENT_DATE,     3250.75, 1480.30, 1770.45, 15, NOW()),
    ('11111111-1111-1111-1111-111111111111', CURRENT_DATE - 1, 4120.00, 2350.50, 1769.50, 22, NOW()),
    ('11111111-1111-1111-1111-111111111111', CURRENT_DATE - 2, 1875.25, 2100.00, -224.75,  9, NOW()),
    ('11111111-1111-1111-1111-111111111111', CURRENT_DATE - 3, 5600.00, 3200.75, 2399.25, 28, NOW()),
    ('11111111-1111-1111-1111-111111111111', CURRENT_DATE - 4, 2980.50, 1650.00, 1330.50, 12, NOW()),
    ('11111111-1111-1111-1111-111111111111', CURRENT_DATE - 5, 3450.00, 4100.25, -650.25, 18, NOW()),
    ('11111111-1111-1111-1111-111111111111', CURRENT_DATE - 6, 4200.80, 2875.60, 1325.20, 25, NOW())
ON CONFLICT (comerciante_id, data) DO NOTHING;
