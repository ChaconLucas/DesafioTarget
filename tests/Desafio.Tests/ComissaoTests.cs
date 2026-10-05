using Desafio.Core.Comissoes;

namespace Desafio.Tests;

public class ComissaoTests
{
    private readonly CalculadoraComissao _calc = new();

    [Theory]
    [InlineData("0", "0")]
    [InlineData("99.99", "0")]       // abaixo de 100: sem comissão
    [InlineData("100.00", "1.00")]   // 100 já entra no 1%
    [InlineData("499.99", "5.00")]   // 1% de 499,99 = 4,9999 → 5,00
    [InlineData("500.00", "25.00")]  // "a partir de" 500: 5%
    [InlineData("1200.50", "60.03")] // 60,025 → arredonda para 60,03
    public void Aplica_a_faixa_correta(string valor, string esperado)
    {
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture),
                     _calc.CalcularComissao(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Valor_negativo_gera_erro()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calc.CalcularComissao(-1m));
    }

    [Fact]
    public void Agrupa_e_soma_por_vendedor()
    {
        var vendas = new List<Venda>
        {
            new("Ana", 50m),    // 0
            new("Ana", 200m),   // 2,00
            new("Ana", 1000m),  // 50,00
            new("Bia", 500m),   // 25,00
        };

        var resumo = _calc.CalcularPorVendedor(vendas);

        var ana = Assert.Single(resumo, r => r.Vendedor == "Ana");
        Assert.Equal(3, ana.QuantidadeVendas);
        Assert.Equal(1250m, ana.TotalVendido);
        Assert.Equal(52m, ana.TotalComissao);
        Assert.Equal("Ana", resumo[0].Vendedor); // ordenado pela maior comissão
    }

    [Fact]
    public void Aceita_faixas_personalizadas()
    {
        var calc = new CalculadoraComissao([new(0m, 0.10m)]);
        Assert.Equal(5m, calc.CalcularComissao(50m));
    }

    [Fact]
    public void Detalha_cada_venda_com_percentual_e_comissao()
    {
        var vendas = new List<Venda> { new("Ana", 99.99m), new("Ana", 100m), new("Bia", 500m) };

        var detalhe = new CalculadoraComissao().DetalharVendas(vendas);

        Assert.Equal(3, detalhe.Count);
        Assert.Equal((0m, 0m), (detalhe[0].Percentual, detalhe[0].Comissao));
        Assert.Equal((0.01m, 1m), (detalhe[1].Percentual, detalhe[1].Comissao));
        Assert.Equal((0.05m, 25m), (detalhe[2].Percentual, detalhe[2].Comissao));
    }
}
