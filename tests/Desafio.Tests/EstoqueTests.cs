using Desafio.Core.Estoques;

namespace Desafio.Tests;

public class EstoqueTests
{
    private static EstoqueService CriarServico() => new(
    [
        new Produto { CodigoProduto = 101, DescricaoProduto = "Caneta Azul", Quantidade = 150 },
        new Produto { CodigoProduto = 102, DescricaoProduto = "Caderno Universitário", Quantidade = 75 },
    ]);

    [Fact]
    public void Entrada_soma_ao_estoque()
    {
        var mov = CriarServico().Movimentar(101, TipoMovimentacao.Entrada, 50, "Compra fornecedor");

        Assert.Equal(150, mov.EstoqueAnterior);
        Assert.Equal(200, mov.EstoqueFinal);
    }

    [Fact]
    public void Saida_subtrai_do_estoque()
    {
        var mov = CriarServico().Movimentar(102, TipoMovimentacao.Saida, 75, "Venda");
        Assert.Equal(0, mov.EstoqueFinal);
    }

    [Fact]
    public void Ids_sao_unicos_e_sequenciais_e_ficam_no_historico()
    {
        var servico = CriarServico();
        var m1 = servico.Movimentar(101, TipoMovimentacao.Entrada, 1, "A");
        var m2 = servico.Movimentar(102, TipoMovimentacao.Saida, 1, "B");

        Assert.Equal(1, m1.Id);
        Assert.Equal(2, m2.Id);
        Assert.Equal(2, servico.Historico.Count);
    }

    [Fact]
    public void Nao_permite_estoque_negativo()
    {
        var servico = CriarServico();

        var ex = Assert.Throws<EstoqueException>(() =>
            servico.Movimentar(102, TipoMovimentacao.Saida, 80, "Venda"));

        Assert.Contains("Saldo insuficiente", ex.Message);
        Assert.Equal(75, servico.ObterProduto(102).Quantidade); // nada foi alterado
        Assert.Empty(servico.Historico);
    }

    [Theory]
    [InlineData(999, 10, "Venda")] // produto inexistente
    [InlineData(101, 0, "Venda")]  // quantidade zero
    [InlineData(101, -5, "Venda")] // quantidade negativa
    [InlineData(101, 10, "  ")]    // sem descrição
    public void Rejeita_movimentacoes_invalidas(int codigo, int quantidade, string descricao)
    {
        Assert.Throws<EstoqueException>(() =>
            CriarServico().Movimentar(codigo, TipoMovimentacao.Entrada, quantidade, descricao));
    }

    [Fact]
    public void Rejeita_tipo_invalido()
    {
        Assert.Throws<EstoqueException>(() =>
            CriarServico().Movimentar(101, (TipoMovimentacao)9, 1, "X"));
    }

    [Fact]
    public void Restaurar_reaplica_o_historico_e_continua_a_numeracao()
    {
        var data = new DateTime(2026, 1, 10, 9, 30, 0);
        var servico = CriarServico();

        servico.Restaurar(
        [
            new Movimentacao(1, 101, TipoMovimentacao.Saida, 50, "Venda", data, 0, 0),
            new Movimentacao(2, 102, TipoMovimentacao.Entrada, 5, "Compra", data, 0, 0),
        ]);
        var nova = servico.Movimentar(101, TipoMovimentacao.Entrada, 10, "Compra");

        Assert.Equal(100, servico.Historico[0].EstoqueFinal); // saldos recalculados, não copiados
        Assert.Equal(data, servico.Historico[0].DataHora);
        Assert.Equal(80, servico.ObterProduto(102).Quantidade);
        Assert.Equal(3, nova.Id);
        Assert.Equal(110, nova.EstoqueFinal);
    }

    [Fact]
    public void Restaurar_rejeita_historico_inconsistente()
    {
        var servico = CriarServico();
        var invalido = new Movimentacao(1, 102, TipoMovimentacao.Saida, 999, "Venda", DateTime.Now, 0, 0);

        Assert.Throws<EstoqueException>(() => servico.Restaurar([invalido]));
    }
}
