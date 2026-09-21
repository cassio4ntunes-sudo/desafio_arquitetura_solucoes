using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace WKR.Consolidacao.Saude;

/// <summary>
/// Readiness do banco: abre conexao e executa SELECT 1. Usado pela probe
/// /health/ready para que orquestradores (ECS/Kubernetes) so enviem trafego
/// para a instancia quando ela conseguir atender de fato.
/// </summary>
public class PostgresHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public PostgresHealthCheck(string connectionString)
        => _connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy("PostgreSQL acessivel.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL inacessivel.", ex);
        }
    }
}
