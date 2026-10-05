using System.Text.Json.Serialization;

namespace Desafio.Core.Comissoes;

public record Venda(
    [property: JsonPropertyName("vendedor")] string Vendedor,
    [property: JsonPropertyName("valor")] decimal Valor);

public record ArquivoVendas([property: JsonPropertyName("vendas")] List<Venda> Vendas);

/// <summary>Faixa de comissão: vendas com valor &gt;= ValorMinimo recebem o Percentual.</summary>
public record FaixaComissao(decimal ValorMinimo, decimal Percentual);

public record ResumoComissao(string Vendedor, int QuantidadeVendas, decimal TotalVendido, decimal TotalComissao);

/// <summary>Uma venda com a faixa aplicada e a comissão que ela gerou.</summary>
public record VendaComissionada(string Vendedor, decimal Valor, decimal Percentual, decimal Comissao);

public class CalculadoraComissao
{
    /// <summary>
    /// Regra do desafio:
    ///  - abaixo de R$ 100,00 → sem comissão
    ///  - abaixo de R$ 500,00 → 1%
    ///  - a partir de R$ 500,00 → 5%
    /// </summary>
    public static readonly IReadOnlyList<FaixaComissao> FaixasPadrao =
    [
        new(500m, 0.05m),
        new(100m, 0.01m),
        new(0m, 0m),
    ];

    private readonly IReadOnlyList<FaixaComissao> _faixas;

    public CalculadoraComissao(IEnumerable<FaixaComissao>? faixas = null)
    {
        // Ordena da maior faixa para a menor: a primeira que o valor atingir é a aplicada.
        _faixas = (faixas ?? FaixasPadrao).OrderByDescending(f => f.ValorMinimo).ToList();
    }

    /// <summary>Percentual de comissão da faixa em que o valor se encaixa.</summary>
    public decimal ObterPercentual(decimal valorVenda)
    {
        if (valorVenda < 0)
            throw new ArgumentOutOfRangeException(nameof(valorVenda), "O valor da venda não pode ser negativo.");

        return _faixas.FirstOrDefault(f => valorVenda >= f.ValorMinimo)?.Percentual ?? 0m;
    }

    /// <summary>Comissão de uma única venda, arredondada em 2 casas.</summary>
    public decimal CalcularComissao(decimal valorVenda) =>
        Math.Round(valorVenda * ObterPercentual(valorVenda), 2, MidpointRounding.AwayFromZero);

    /// <summary>Cada venda com o percentual aplicado e a comissão gerada, na ordem original.</summary>
    public IReadOnlyList<VendaComissionada> DetalharVendas(IEnumerable<Venda> vendas) =>
        vendas
            .Select(v => new VendaComissionada(v.Vendedor, v.Valor, ObterPercentual(v.Valor), CalcularComissao(v.Valor)))
            .ToList();

    /// <summary>Agrupa as vendas por vendedor e soma vendas e comissões (maior comissão primeiro).</summary>
    public IReadOnlyList<ResumoComissao> CalcularPorVendedor(IEnumerable<Venda> vendas) =>
        vendas
            .GroupBy(v => v.Vendedor)
            .Select(g => new ResumoComissao(
                g.Key,
                g.Count(),
                g.Sum(v => v.Valor),
                g.Sum(v => CalcularComissao(v.Valor))))
            .OrderByDescending(r => r.TotalComissao)
            .ToList();
}
