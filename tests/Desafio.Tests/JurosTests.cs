using Desafio.Core.Juros;

namespace Desafio.Tests;

public class JurosTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 5);

    [Fact]
    public void Calcula_juros_simples_por_dia_de_atraso()
    {
        var r = CalculadoraJuros.Calcular(1000m, new DateOnly(2026, 9, 25), Hoje);

        Assert.Equal(10, r.DiasAtraso);
        Assert.Equal(250m, r.ValorJuros);      // 1000 × 2,5% × 10
        Assert.Equal(1250m, r.ValorAtualizado);
    }

    [Fact]
    public void Um_dia_de_atraso()
    {
        var r = CalculadoraJuros.Calcular(100m, new DateOnly(2026, 10, 4), Hoje);
        Assert.Equal(2.5m, r.ValorJuros);
    }

    [Theory]
    [InlineData(2026, 10, 5)]  // vence hoje
    [InlineData(2026, 12, 31)] // vence no futuro
    public void Sem_atraso_nao_ha_juros(int ano, int mes, int dia)
    {
        var r = CalculadoraJuros.Calcular(500m, new DateOnly(ano, mes, dia), Hoje);

        Assert.Equal(0, r.DiasAtraso);
        Assert.Equal(0m, r.ValorJuros);
        Assert.Equal(500m, r.ValorAtualizado);
    }

    [Fact]
    public void Arredonda_em_duas_casas()
    {
        var r = CalculadoraJuros.Calcular(10.01m, new DateOnly(2026, 10, 4), Hoje);
        Assert.Equal(0.25m, r.ValorJuros); // 0,25025 → 0,25
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Valor_invalido_gera_erro(int valor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalculadoraJuros.Calcular(valor, new DateOnly(2026, 10, 1), Hoje));
    }

    [Fact]
    public void Valor_acima_do_maximo_gera_erro_em_vez_de_estourar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalculadoraJuros.Calcular(decimal.MaxValue, DateOnly.MinValue, Hoje));
    }

    [Fact]
    public void Valor_maximo_com_atraso_maximo_nao_estoura()
    {
        var r = CalculadoraJuros.Calcular(CalculadoraJuros.ValorMaximo, DateOnly.MinValue, DateOnly.MaxValue);
        Assert.True(r.ValorAtualizado > CalculadoraJuros.ValorMaximo);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Taxa_fora_do_intervalo_gera_erro(double taxa)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalculadoraJuros.Calcular(100m, new DateOnly(2026, 1, 1), Hoje, (decimal)taxa));
    }
}
