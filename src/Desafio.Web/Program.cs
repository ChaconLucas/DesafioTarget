using System.Text.Json;
using System.Text.Json.Serialization;
using Desafio.Core.Comissoes;
using Desafio.Core.Estoques;
using Desafio.Core.Juros;
using Desafio.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "Desafio Target Sistemas", Version = "v1" }));

var pastaDados = Path.Combine(AppContext.BaseDirectory, "Dados");

// O histórico de movimentações é gravado em App_Data/movimentacoes.json (configurável em Estoque:ArquivoHistorico).
builder.Services.AddSingleton(sp => new EstoqueRepositorio(
    Path.Combine(pastaDados, "estoque.json"),
    builder.Configuration["Estoque:ArquivoHistorico"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "movimentacoes.json"),
    sp.GetRequiredService<ILogger<EstoqueRepositorio>>()));

var app = builder.Build();
app.UseDefaultFiles();
// no-cache: o navegador sempre confere se a página mudou, em vez de mostrar uma versão antiga.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});
app.UseSwagger();
app.UseSwaggerUI();

// ---------- Exercício 1: comissões ----------

var comissoes = app.MapGroup("/api/comissoes").WithTags("Comissões");

comissoes.MapGet("/", () =>
{
    var calculadora = new CalculadoraComissao();
    var vendas = LerJson<ArquivoVendas>("vendas.json").Vendas;
    var resumo = calculadora.CalcularPorVendedor(vendas);
    var detalhe = calculadora.DetalharVendas(vendas).ToLookup(v => v.Vendedor);

    return new
    {
        faixas = CalculadoraComissao.FaixasPadrao.OrderBy(f => f.ValorMinimo),
        vendedores = resumo.Select(r => new
        {
            r.Vendedor,
            r.QuantidadeVendas,
            r.TotalVendido,
            r.TotalComissao,
            vendas = detalhe[r.Vendedor].OrderByDescending(v => v.Valor),
        }),
        total = new
        {
            quantidadeVendas = resumo.Sum(r => r.QuantidadeVendas),
            totalVendido = resumo.Sum(r => r.TotalVendido),
            totalComissao = resumo.Sum(r => r.TotalComissao),
        },
    };
}).WithSummary("Comissão por vendedor, com o detalhe de cada venda");

// ---------- Exercício 2: estoque ----------

var estoque = app.MapGroup("/api/estoque").WithTags("Estoque");

estoque.MapGet("/produtos", (EstoqueRepositorio repo) => repo.Produtos())
    .WithSummary("Produtos com o saldo atual");

estoque.MapGet("/movimentacoes", (EstoqueRepositorio repo) => repo.Historico())
    .WithSummary("Histórico de movimentações, da mais recente para a mais antiga");

estoque.MapPost("/movimentacoes", (NovaMovimentacao req, EstoqueRepositorio repo) =>
{
    try
    {
        return Results.Ok(repo.Movimentar(req.CodigoProduto, req.Tipo, req.Quantidade, req.Descricao ?? ""));
    }
    catch (EstoqueException ex)
    {
        return Results.BadRequest(new RespostaErro(ex.Message));
    }
})
.WithSummary("Lança uma entrada ou saída e devolve o estoque final")
.Produces<Movimentacao>()
.Produces<RespostaErro>(StatusCodes.Status400BadRequest);

estoque.MapDelete("/movimentacoes", (EstoqueRepositorio repo) =>
{
    repo.Reiniciar();
    return Results.NoContent();
}).WithSummary("Apaga o histórico e volta ao saldo inicial");

// ---------- Exercício 3: juros ----------

app.MapGet("/api/juros", (decimal valor, DateOnly vencimento) =>
{
    try
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        return Results.Ok(new { hoje, resultado = CalculadoraJuros.Calcular(valor, vencimento, hoje) });
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new RespostaErro(ex.Message.Split(" (Parameter")[0]));
    }
})
.WithTags("Juros")
.WithSummary("Juros de 2,5% ao dia sobre o valor, do vencimento até hoje (vencimento no formato aaaa-mm-dd)")
.Produces<RespostaErro>(StatusCodes.Status400BadRequest);

app.Run();

T LerJson<T>(string arquivo)
{
    var caminho = Path.Combine(pastaDados, arquivo);
    return JsonSerializer.Deserialize<T>(File.ReadAllText(caminho))
           ?? throw new InvalidDataException($"Arquivo {arquivo} vazio ou inválido.");
}

record NovaMovimentacao(int CodigoProduto, TipoMovimentacao Tipo, int Quantidade, string? Descricao);

record RespostaErro(string Erro);

// Permite que os testes de integração subam a aplicação com WebApplicationFactory<Program>.
public partial class Program;
