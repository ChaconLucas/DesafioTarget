using System.Text.Json;
using Desafio.Core.Estoques;

namespace Desafio.Web;

/// <summary>
/// Guarda o <see cref="EstoqueService"/> da aplicação e grava o histórico de movimentações num arquivo JSON,
/// para que os lançamentos não se percam quando o servidor reinicia.
/// O saldo inicial sempre vem de Dados/estoque.json; o arquivo guarda só as movimentações, que são reaplicadas.
/// </summary>
public class EstoqueRepositorio
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _arquivoEstoque;
    private readonly string _arquivoHistorico;
    private readonly ILogger<EstoqueRepositorio> _log;
    private readonly object _trava = new();
    private EstoqueService _servico;

    public EstoqueRepositorio(string arquivoEstoque, string arquivoHistorico, ILogger<EstoqueRepositorio> log)
    {
        _arquivoEstoque = arquivoEstoque;
        _arquivoHistorico = arquivoHistorico;
        _log = log;
        _servico = Carregar();
    }

    public List<Produto> Produtos()
    {
        lock (_trava)
            return _servico.Produtos.OrderBy(p => p.CodigoProduto).ToList();
    }

    public List<Movimentacao> Historico()
    {
        lock (_trava)
            return _servico.Historico.Reverse().ToList();
    }

    public Movimentacao Movimentar(int codigoProduto, TipoMovimentacao tipo, int quantidade, string descricao)
    {
        lock (_trava)
        {
            var mov = _servico.Movimentar(codigoProduto, tipo, quantidade, descricao);
            Salvar();
            return mov;
        }
    }

    /// <summary>Apaga o histórico e volta ao saldo inicial do arquivo de estoque.</summary>
    public void Reiniciar()
    {
        lock (_trava)
        {
            File.Delete(_arquivoHistorico);
            _servico = NovoServico();
        }
    }

    private EstoqueService NovoServico()
    {
        var arquivo = JsonSerializer.Deserialize<ArquivoEstoque>(File.ReadAllText(_arquivoEstoque))
                      ?? throw new InvalidDataException("Arquivo de estoque vazio ou inválido.");
        return new EstoqueService(arquivo.Estoque);
    }

    private EstoqueService Carregar()
    {
        var servico = NovoServico();
        if (!File.Exists(_arquivoHistorico))
            return servico;

        try
        {
            var historico = JsonSerializer.Deserialize<List<Movimentacao>>(File.ReadAllText(_arquivoHistorico)) ?? [];
            servico.Restaurar(historico);
            _log.LogInformation("{Quantidade} movimentações restauradas de {Arquivo}.", historico.Count, _arquivoHistorico);
            return servico;
        }
        catch (Exception ex) when (ex is JsonException or EstoqueException)
        {
            // Não derruba a aplicação: guarda o arquivo problemático e começa do saldo inicial.
            var backup = _arquivoHistorico + ".invalido";
            File.Move(_arquivoHistorico, backup, overwrite: true);
            _log.LogWarning(ex, "Histórico de estoque inválido, movido para {Backup}. Começando do saldo inicial.", backup);
            return NovoServico();
        }
    }

    private void Salvar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivoHistorico)!);
        // Grava num arquivo temporário e troca, para não deixar um JSON pela metade se algo falhar.
        var temporario = _arquivoHistorico + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(_servico.Historico, Json));
        File.Move(temporario, _arquivoHistorico, overwrite: true);
    }
}
