using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Desafio.Tests;

/// <summary>
/// Testes de integração da API web: sobem a aplicação em memória e chamam os endpoints por HTTP.
/// Cada teste usa um arquivo de histórico próprio, numa pasta temporária.
/// </summary>
public class ApiTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "desafio-testes-" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _fabrica;

    public ApiTests()
    {
        _fabrica = CriarFabrica();
    }

    private string ArquivoHistorico => Path.Combine(_pasta, "movimentacoes.json");

    private WebApplicationFactory<Program> CriarFabrica(Action<IWebHostBuilder>? configurar = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Estoque:ArquivoHistorico", ArquivoHistorico);
            configurar?.Invoke(b);
        });

    public void Dispose()
    {
        _fabrica.Dispose();
        if (Directory.Exists(_pasta))
            Directory.Delete(_pasta, recursive: true);
    }

    private static object Movimento(int codigo, string tipo, int quantidade, string descricao = "Teste") =>
        new { codigoProduto = codigo, tipo, quantidade, descricao };

    [Fact]
    public async Task Comissoes_retorna_totais_e_detalhe_das_vendas()
    {
        var json = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>("/api/comissoes");

        Assert.Equal(1746.02m, json.GetProperty("total").GetProperty("totalComissao").GetDecimal());
        var joao = json.GetProperty("vendedores")[0];
        Assert.Equal("João Silva", joao.GetProperty("vendedor").GetString());
        Assert.Equal(10, joao.GetProperty("vendas").GetArrayLength());
    }

    [Fact]
    public async Task Saida_valida_devolve_estoque_final()
    {
        var resposta = await _fabrica.CreateClient().PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(102, "Saida", 10));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var mov = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, mov.GetProperty("id").GetInt32());
        Assert.Equal(65, mov.GetProperty("estoqueFinal").GetInt32());
    }

    [Fact]
    public async Task Saida_maior_que_o_saldo_devolve_400_com_mensagem()
    {
        var resposta = await _fabrica.CreateClient().PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(102, "Saida", 80));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("Saldo insuficiente", erro.GetProperty("erro").GetString());
    }

    [Theory]
    [InlineData(999, "Entrada", 1, "Teste", "não encontrado")]
    [InlineData(101, "Entrada", 0, "Teste", "maior que zero")]
    [InlineData(101, "Entrada", 1, "  ", "descrição")]
    public async Task Dados_invalidos_devolvem_400(int codigo, string tipo, int quantidade, string descricao, string trecho)
    {
        var resposta = await _fabrica.CreateClient().PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(codigo, tipo, quantidade, descricao));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(trecho, await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Movimentacoes_sobrevivem_a_reinicializacao()
    {
        await _fabrica.CreateClient().PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(101, "Entrada", 50));
        _fabrica.Dispose();

        // Uma nova instância da aplicação lendo o mesmo arquivo.
        using var outra = CriarFabrica();
        var cliente = outra.CreateClient();
        var produtos = await cliente.GetFromJsonAsync<JsonElement>("/api/estoque/produtos");
        var historico = await cliente.GetFromJsonAsync<JsonElement>("/api/estoque/movimentacoes");

        Assert.Equal(200, produtos.EnumerateArray().Single(p => p.GetProperty("codigoProduto").GetInt32() == 101).GetProperty("estoque").GetInt32());
        Assert.Equal(1, historico.GetArrayLength());
    }

    [Fact]
    public async Task Reiniciar_volta_ao_saldo_inicial()
    {
        var cliente = _fabrica.CreateClient();
        await cliente.PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(101, "Saida", 100));

        var resposta = await cliente.DeleteAsync("/api/estoque/movimentacoes");
        var produtos = await cliente.GetFromJsonAsync<JsonElement>("/api/estoque/produtos");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(150, produtos[0].GetProperty("estoque").GetInt32());
        Assert.False(File.Exists(ArquivoHistorico));
    }

    [Fact]
    public async Task Juros_calcula_ate_hoje()
    {
        var vencimento = DateOnly.FromDateTime(DateTime.Today).AddDays(-10).ToString("yyyy-MM-dd");

        var json = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/juros?valor=1000&vencimento={vencimento}");

        var r = json.GetProperty("resultado");
        Assert.Equal(10, r.GetProperty("diasAtraso").GetInt32());
        Assert.Equal(250m, r.GetProperty("valorJuros").GetDecimal());
    }

    [Fact]
    public async Task Juros_com_valor_invalido_devolve_400()
    {
        var resposta = await _fabrica.CreateClient().GetAsync("/api/juros?valor=0&vencimento=2026-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("maior que zero", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Interface_e_swagger_estao_disponiveis()
    {
        var cliente = _fabrica.CreateClient();

        Assert.Contains("Desafio Target Sistemas", await cliente.GetStringAsync("/"));
        Assert.Contains("/api/estoque/movimentacoes", await cliente.GetStringAsync("/swagger/v1/swagger.json"));
    }

    // ---------- segurança ----------

    [Fact]
    public async Task Respostas_tem_cabecalhos_de_seguranca()
    {
        var resposta = await _fabrica.CreateClient().GetAsync("/");

        Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resposta.Headers.GetValues("X-Frame-Options").Single());
        var csp = resposta.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [Fact]
    public async Task Host_desconhecido_e_rejeitado()
    {
        var cliente = _fabrica.CreateClient();
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/estoque/produtos");
        requisicao.Headers.Host = "site-malicioso.example";

        var resposta = await cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Excesso_de_lancamentos_devolve_429()
    {
        using var fabrica = CriarFabrica(b => b.UseSetting("Seguranca:EscritasPorMinuto", "2"));
        var cliente = fabrica.CreateClient();

        var status = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            status.Add((await cliente.PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(101, "Entrada", 1))).StatusCode);

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], status);
    }

    [Fact]
    public async Task Entrada_que_estouraria_o_estoque_devolve_400()
    {
        var cliente = _fabrica.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(101, "Entrada", int.MaxValue));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var produtos = await cliente.GetFromJsonAsync<JsonElement>("/api/estoque/produtos");
        Assert.Equal(150, produtos[0].GetProperty("estoque").GetInt32());
    }

    [Fact]
    public async Task Descricao_muito_longa_devolve_400()
    {
        var resposta = await _fabrica.CreateClient().PostAsJsonAsync("/api/estoque/movimentacoes", Movimento(101, "Entrada", 1, new string('A', 5000)));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.False(File.Exists(ArquivoHistorico));
    }

    [Fact]
    public async Task Juros_com_valor_extremo_devolve_400_e_nao_500()
    {
        var resposta = await _fabrica.CreateClient().GetAsync("/api/juros?valor=79228162514264337593543950335&vencimento=0001-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData("{\"codigoProduto\":101,\"tipo\":\"Roubo\",\"quantidade\":1,\"descricao\":\"x\"}")]
    [InlineData("{json quebrado")]
    public async Task Corpo_invalido_devolve_400_sem_detalhes_internos(string corpo)
    {
        var resposta = await _fabrica.CreateClient().PostAsync("/api/estoque/movimentacoes",
            new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.DoesNotContain("   at ", await resposta.Content.ReadAsStringAsync()); // sem stack trace
    }

    [Fact]
    public async Task Swagger_nao_fica_exposto_em_producao()
    {
        using var producao = CriarFabrica(b => b.UseEnvironment("Production"));

        var resposta = await producao.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
