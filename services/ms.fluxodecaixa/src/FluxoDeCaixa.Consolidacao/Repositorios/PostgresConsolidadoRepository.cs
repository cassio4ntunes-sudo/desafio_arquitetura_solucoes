using FluxoDeCaixa.Consolidacao.Modelos;
using FluxoDeCaixa.Domain.Eventos;
using Npgsql;

namespace FluxoDeCaixa.Consolidacao.Repositorios;

public class PostgresConsolidadoRepository : IConsolidadoRepository
{
    private readonly string _connectionString;

    public PostgresConsolidadoRepository(string connectionString)
        => _connectionString = connectionString;

    /// <summary>
    /// Aplica o lançamento ao consolidado do comerciante de forma idempotente.
    /// A entrega do RabbitMQ é at-least-once (com retry 500ms/2s/10s), então o mesmo
    /// evento pode chegar mais de uma vez. O INSERT em lancamentos_consolidados atua
    /// como inbox: se o LancamentoId já foi aplicado, nada é somado.
    /// </summary>
    public async Task UpsertAsync(LancamentoRegistrado evento, CancellationToken ct)
    {
        var creditoValor = evento.Tipo == "Credito" ? evento.Valor : 0m;
        var debitoValor  = evento.Tipo == "Debito"  ? evento.Valor : 0m;

        const string sql = """
            WITH novo AS (
                INSERT INTO lancamentos_consolidados (lancamento_id, aplicado_em)
                VALUES ($1, $5)
                ON CONFLICT (lancamento_id) DO NOTHING
                RETURNING lancamento_id
            )
            INSERT INTO consolidado_diario (comerciante_id, data, total_creditos, total_debitos, saldo_liquido, quantidade, atualizado_em)
            SELECT $2, $3, $4, $6, $4 - $6, 1, $5
            FROM novo
            ON CONFLICT (comerciante_id, data) DO UPDATE SET
                total_creditos = consolidado_diario.total_creditos + EXCLUDED.total_creditos,
                total_debitos  = consolidado_diario.total_debitos  + EXCLUDED.total_debitos,
                saldo_liquido  = (consolidado_diario.total_creditos + EXCLUDED.total_creditos)
                               - (consolidado_diario.total_debitos  + EXCLUDED.total_debitos),
                quantidade     = consolidado_diario.quantidade + 1,
                atualizado_em  = EXCLUDED.atualizado_em;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(evento.LancamentoId);   // $1 — inbox key
        cmd.Parameters.AddWithValue(evento.ComercianteId);  // $2 — uuid
        cmd.Parameters.AddWithValue(evento.Data);           // $3 — DateOnly → date
        cmd.Parameters.AddWithValue(creditoValor);          // $4 — decimal → numeric
        cmd.Parameters.AddWithValue(DateTimeOffset.UtcNow); // $5 — DateTimeOffset → timestamptz
        cmd.Parameters.AddWithValue(debitoValor);           // $6 — decimal → numeric
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ConsolidadoDiario?> ObterPorDataAsync(
        Guid comercianteId, DateOnly data, CancellationToken ct)
    {
        const string sql = """
            SELECT comerciante_id, data, total_creditos, total_debitos, saldo_liquido, quantidade, atualizado_em
            FROM consolidado_diario
            WHERE comerciante_id = $1 AND data = $2;
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(comercianteId);
        cmd.Parameters.AddWithValue(data);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return new ConsolidadoDiario(
            ComercianteId: reader.GetGuid(0),
            Data: reader.GetFieldValue<DateOnly>(1),
            TotalCreditos: reader.GetDecimal(2),
            TotalDebitos: reader.GetDecimal(3),
            SaldoLiquido: reader.GetDecimal(4),
            Quantidade: reader.GetInt32(5),
            AtualizadoEm: reader.GetFieldValue<DateTimeOffset>(6)
        );
    }

    public async Task CriarTabelaSeNaoExisteAsync(CancellationToken ct)
    {
        const string sql = """
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

            CREATE TABLE IF NOT EXISTS lancamentos_consolidados (
                lancamento_id UUID        PRIMARY KEY,
                aplicado_em   TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
