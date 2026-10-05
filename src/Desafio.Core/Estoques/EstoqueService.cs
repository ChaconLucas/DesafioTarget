using System.Text.Json.Serialization;

namespace Desafio.Core.Estoques;

public class Produto
{
    [JsonPropertyName("codigoProduto")] public int CodigoProduto { get; init; }
    [JsonPropertyName("descricaoProduto")] public string DescricaoProduto { get; init; } = "";
    [JsonPropertyName("estoque")] public int Quantidade { get; set; }
}

public record ArquivoEstoque([property: JsonPropertyName("estoque")] List<Produto> Estoque);

public enum TipoMovimentacao
{
    Entrada = 1,
    Saida = 2,
}

public record Movimentacao(
    int Id,
    int CodigoProduto,
    TipoMovimentacao Tipo,
    int Quantidade,
    string Descricao,
    DateTime DataHora,
    int EstoqueAnterior,
    int EstoqueFinal);

/// <summary>Erro de regra de negócio do estoque, com mensagem pronta para mostrar ao usuário.</summary>
public class EstoqueException(string mensagem) : Exception(mensagem);

public class EstoqueService
{
    private readonly Dictionary<int, Produto> _produtos;
    private readonly List<Movimentacao> _historico = [];
    private readonly Func<DateTime> _agora;
    private int _ultimoId;

    public EstoqueService(IEnumerable<Produto> produtos, Func<DateTime>? relogio = null)
    {
        _produtos = produtos.ToDictionary(p => p.CodigoProduto);
        _agora = relogio ?? (() => DateTime.Now);
    }

    public IReadOnlyCollection<Produto> Produtos => _produtos.Values;
    public IReadOnlyList<Movimentacao> Historico => _historico;

    public Produto ObterProduto(int codigoProduto) =>
        _produtos.TryGetValue(codigoProduto, out var produto)
            ? produto
            : throw new EstoqueException($"Produto {codigoProduto} não encontrado.");

    /// <summary>
    /// Lança uma movimentação de entrada ou saída e devolve o registro com o estoque final do produto.
    /// </summary>
    public Movimentacao Movimentar(int codigoProduto, TipoMovimentacao tipo, int quantidade, string descricao) =>
        Aplicar(_ultimoId + 1, codigoProduto, tipo, quantidade, descricao, _agora());

    /// <summary>
    /// Reaplica movimentações já registradas (por exemplo, lidas de um arquivo), mantendo Id e data originais.
    /// Passa pelas mesmas validações, então um histórico inconsistente gera <see cref="EstoqueException"/>.
    /// </summary>
    public void Restaurar(IEnumerable<Movimentacao> historico)
    {
        foreach (var m in historico.OrderBy(m => m.Id))
        {
            if (m.Id <= _ultimoId)
                throw new EstoqueException($"Histórico inválido: a movimentação {m.Id} está fora de ordem ou repetida.");

            Aplicar(m.Id, m.CodigoProduto, m.Tipo, m.Quantidade, m.Descricao, m.DataHora);
        }
    }

    private Movimentacao Aplicar(int id, int codigoProduto, TipoMovimentacao tipo, int quantidade, string descricao, DateTime dataHora)
    {
        if (!Enum.IsDefined(tipo))
            throw new EstoqueException("Tipo de movimentação inválido.");
        if (quantidade <= 0)
            throw new EstoqueException("A quantidade deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(descricao))
            throw new EstoqueException("Informe uma descrição para a movimentação.");

        var produto = ObterProduto(codigoProduto);
        var anterior = produto.Quantidade;

        if (tipo == TipoMovimentacao.Saida && quantidade > anterior)
            throw new EstoqueException(
                $"Saldo insuficiente: o produto {produto.CodigoProduto} ({produto.DescricaoProduto}) " +
                $"tem {anterior} em estoque e foi solicitada a saída de {quantidade}.");

        produto.Quantidade = tipo == TipoMovimentacao.Entrada ? anterior + quantidade : anterior - quantidade;

        var movimentacao = new Movimentacao(
            Id: id,
            CodigoProduto: produto.CodigoProduto,
            Tipo: tipo,
            Quantidade: quantidade,
            Descricao: descricao.Trim(),
            DataHora: dataHora,
            EstoqueAnterior: anterior,
            EstoqueFinal: produto.Quantidade);

        _historico.Add(movimentacao);
        _ultimoId = id;
        return movimentacao;
    }
}
