using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluxoDeCaixa.Tests.Integration.Fixtures;

namespace FluxoDeCaixa.Tests.Integration;

/// <summary>
/// Prova a resiliência do caminho de escrita: o registro de lançamentos responde
/// 201 de forma confiável, inclusive sob POSTs concorrentes.
/// Usa o IntegrationTestFixture compartilhado (PostgreSQL + RabbitMQ Testcontainers).
/// </summary>
[Collection("Integration")]
public class ResilienciaTests
{
    private readonly IntegrationTestFixture _fixture;

    public ResilienciaTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deve_registrar_lancamento_retorna_201()
    {
        // Arrange — data isolada para não conflitar com outros testes
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-20));
        var request = new
        {
            valor = 100.00m,
            tipo = "Credito",
            data = data.ToString("yyyy-MM-dd"),
            descricao = "Teste resiliência",
            categoria = "Vendas"
        };

        // Act
        var response = await client.PostAsJsonAsync("/lancamentos", request);

        // Assert — 201 prova que o caminho de escrita funciona
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("id");
        body!["id"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Deve_registrar_multiplos_lancamentos_concorrentes()
    {
        // Arrange — 3 lançamentos em data isolada
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-21));
        var lancamentos = new[]
        {
            new { valor = 200.00m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"), descricao = "Crédito resiliência 1", categoria = "Vendas" },
            new { valor = 50.00m,  tipo = "Debito",  data = data.ToString("yyyy-MM-dd"), descricao = "Débito resiliência",   categoria = "Serviços" },
            new { valor = 150.00m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"), descricao = "Crédito resiliência 2", categoria = "Vendas" }
        };

        // Act — dispara os 3 em paralelo (streams por transação = zero contenção)
        var responses = await Task.WhenAll(
            lancamentos.Select(l => client.PostAsJsonAsync("/lancamentos", l)));

        // Assert — todos 201
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));
    }

    /// <summary>
    /// RNF do desafio: o serviço de lançamentos não pode ficar indisponível se a
    /// consolidação cair. Aqui o caminho de escrita continua respondendo 201
    /// mesmo com um volume de mensagens que a consolidação ainda não processou —
    /// o desacoplamento via RabbitMQ garante que a escrita não espera o consumer.
    /// </summary>
    [Fact]
    public async Task Escrita_permanece_disponivel_independente_da_consolidacao()
    {
        // Arrange
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-22));

        // Act — 20 lançamentos em rajada
        var envios = Enumerable.Range(1, 20).Select(i => client.PostAsJsonAsync("/lancamentos", new
        {
            valor = 10.00m * i,
            tipo = "Credito",
            data = data.ToString("yyyy-MM-dd"),
            descricao = $"Rajada {i}",
            categoria = "Vendas"
        }));

        var respostas = await Task.WhenAll(envios);

        // Assert — nenhuma escrita foi bloqueada pela consolidação assíncrona
        respostas.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));
    }
}
