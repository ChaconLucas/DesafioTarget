namespace Desafio.Core.Juros;

public record ResultadoJuros(decimal ValorOriginal, int DiasAtraso, decimal ValorJuros, decimal ValorAtualizado);

public static class CalculadoraJuros
{
    /// <summary>2,5% ao dia, conforme o enunciado.</summary>
    public const decimal TaxaDiariaPadrao = 0.025m;

    /// <summary>Maior valor aceito (R$ 1 trilhão). Também impede estouro de <c>decimal</c> no cálculo.</summary>
    public const decimal ValorMaximo = 1_000_000_000_000m;

    /// <summary>
    /// Juros simples por dia corrido de atraso: valor × taxa × dias.
    /// A data de "hoje" é recebida por parâmetro para facilitar os testes.
    /// </summary>
    public static ResultadoJuros Calcular(
        decimal valor,
        DateOnly vencimento,
        DateOnly hoje,
        decimal taxaDiaria = TaxaDiariaPadrao)
    {
        if (valor <= 0)
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor deve ser maior que zero.");
        if (valor > ValorMaximo)
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor máximo aceito é R$ 1 trilhão.");
        if (taxaDiaria is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(taxaDiaria), "A taxa diária deve estar entre 0% e 100%.");

        // Vencendo hoje ou no futuro não há atraso.
        var diasAtraso = Math.Max(0, hoje.DayNumber - vencimento.DayNumber);
        var juros = Math.Round(valor * taxaDiaria * diasAtraso, 2, MidpointRounding.AwayFromZero);

        return new ResultadoJuros(valor, diasAtraso, juros, valor + juros);
    }
}
