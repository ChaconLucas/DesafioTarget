using System.Globalization;
using System.Text;
using System.Text.Json;
using Desafio.Core.Comissoes;
using Desafio.Core.Estoques;
using Desafio.Core.Juros;

Console.OutputEncoding = Encoding.UTF8;
var ptBR = CultureInfo.GetCultureInfo("pt-BR");
var pastaDados = Path.Combine(AppContext.BaseDirectory, "Dados");

// O estoque fica em memória durante a execução, então as movimentações se acumulam.
var estoque = new EstoqueService(LerJson<ArquivoEstoque>("estoque.json").Estoque);

while (true)
{
    Console.WriteLine();
    Console.WriteLine("===== Desafio Target Sistemas =====");
    Console.WriteLine("1 - Calcular comissões dos vendedores");
    Console.WriteLine("2 - Movimentar estoque");
    Console.WriteLine("3 - Calcular juros por atraso");
    Console.WriteLine("0 - Sair");

    switch (Ler("Opção: "))
    {
        case "1": ExecutarComissoes(); break;
        case "2": ExecutarEstoque(); break;
        case "3": ExecutarJuros(); break;
        case "0" or null: return;
        default: Console.WriteLine("Opção inválida."); break;
    }
}

void ExecutarComissoes()
{
    var vendas = LerJson<ArquivoVendas>("vendas.json").Vendas;
    var resumo = new CalculadoraComissao().CalcularPorVendedor(vendas);

    Console.WriteLine();
    Console.WriteLine($"{"Vendedor",-18}{"Vendas",8}{"Total vendido",18}{"Comissão",15}");
    foreach (var r in resumo)
        Console.WriteLine($"{r.Vendedor,-18}{r.QuantidadeVendas,8}{Moeda(r.TotalVendido),18}{Moeda(r.TotalComissao),15}");
    Console.WriteLine($"{"TOTAL",-18}{resumo.Sum(r => r.QuantidadeVendas),8}" +
                      $"{Moeda(resumo.Sum(r => r.TotalVendido)),18}{Moeda(resumo.Sum(r => r.TotalComissao)),15}");
}

void ExecutarEstoque()
{
    Console.WriteLine();
    foreach (var p in estoque.Produtos)
        Console.WriteLine($"{p.CodigoProduto} - {p.DescricaoProduto,-28} estoque: {p.Quantidade}");

    try
    {
        var codigo = LerInteiro("Código do produto: ");
        var produto = estoque.ObterProduto(codigo); // valida antes de pedir o resto
        var tipo = LerInteiro("Tipo (1 = Entrada, 2 = Saída): ");
        var quantidade = LerInteiro("Quantidade: ");
        var descricao = Ler("Descrição (ex.: Compra fornecedor, Venda balcão, Ajuste de inventário): ") ?? "";

        var mov = estoque.Movimentar(produto.CodigoProduto, (TipoMovimentacao)tipo, quantidade, descricao);

        Console.WriteLine();
        Console.WriteLine($"Movimentação #{mov.Id} registrada ({(mov.Tipo == TipoMovimentacao.Entrada ? "Entrada" : "Saída")} - {mov.Descricao}).");
        Console.WriteLine($"{produto.DescricaoProduto}: {mov.EstoqueAnterior} → {mov.EstoqueFinal}");
        Console.WriteLine($"Estoque final do produto: {mov.EstoqueFinal}");
    }
    catch (EstoqueException ex)
    {
        Console.WriteLine($"Não foi possível movimentar: {ex.Message}");
    }
    catch (FormatException ex)
    {
        Console.WriteLine(ex.Message);
    }
}

void ExecutarJuros()
{
    try
    {
        var valor = LerDecimal("Valor (ex.: 1.500,00): ");
        var vencimento = LerData("Data de vencimento (dd/mm/aaaa): ");
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var r = CalculadoraJuros.Calcular(valor, vencimento, hoje);

        Console.WriteLine();
        Console.WriteLine($"Hoje: {hoje.ToString("dd/MM/yyyy", ptBR)} | Dias de atraso: {r.DiasAtraso}");
        Console.WriteLine($"Juros (2,5% ao dia): {Moeda(r.ValorJuros)}");
        Console.WriteLine($"Valor atualizado:    {Moeda(r.ValorAtualizado)}");
    }
    catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
    {
        Console.WriteLine(ex.Message);
    }
}

// ---------- utilitários ----------

T LerJson<T>(string arquivo)
{
    var caminho = Path.Combine(pastaDados, arquivo);
    return JsonSerializer.Deserialize<T>(File.ReadAllText(caminho))
           ?? throw new InvalidDataException($"Arquivo {arquivo} vazio ou inválido.");
}

string? Ler(string rotulo)
{
    Console.Write(rotulo);
    return Console.ReadLine()?.Trim();
}

int LerInteiro(string rotulo) =>
    int.TryParse(Ler(rotulo), out var n) ? n : throw new FormatException("Informe um número inteiro.");

decimal LerDecimal(string rotulo) =>
    decimal.TryParse(Ler(rotulo), NumberStyles.Number, ptBR, out var v)
        ? v
        : throw new FormatException("Valor inválido. Use o formato 1.500,00.");

DateOnly LerData(string rotulo) =>
    DateOnly.TryParseExact(Ler(rotulo), "dd/MM/yyyy", ptBR, DateTimeStyles.None, out var d)
        ? d
        : throw new FormatException("Data inválida. Use o formato dd/mm/aaaa.");

string Moeda(decimal valor) => valor.ToString("C", ptBR);
