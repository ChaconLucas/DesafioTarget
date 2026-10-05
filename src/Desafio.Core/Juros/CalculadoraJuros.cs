namespace Desafio.Core.Juros;

public record ResultadoJuros(decimal ValorOriginal, int DiasAtraso, decimal ValorJuros, decimal ValorAtualizado);

public static class CalculadoraJuros
{
    /// <summary>2,5% ao dia, conforme o enunciado.</summary>
    public const decimal TaxaDiariaPadrao = 0.025m;

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
        if (taxaDiaria < 0)
            throw new ArgumentOutOfRangeException(nameof(taxaDiaria), "A taxa não pode ser negativa.");

        // Vencendo hoje ou no futuro não há atraso.
        var diasAtraso = Math.Max(0, hoje.DayNumber - vencimento.DayNumber);
        var juros = Math.Round(valor * taxaDiaria * diasAtraso, 2, MidpointRounding.AwayFromZero);

        return new ResultadoJuros(valor, diasAtraso, juros, valor + juros);
    }
}
