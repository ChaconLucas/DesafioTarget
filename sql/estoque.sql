/*
  Exercício 2 - modelagem de referência em SQL Server.
  Mostra como as movimentações de estoque ficariam persistidas no banco.
  (O programa em C# trabalha em memória; este script é complementar.)
*/

CREATE TABLE Produto (
    CodigoProduto    INT           NOT NULL PRIMARY KEY,
    DescricaoProduto VARCHAR(100)  NOT NULL,
    Estoque          INT           NOT NULL DEFAULT 0,
    CONSTRAINT CK_Produto_EstoqueNaoNegativo CHECK (Estoque >= 0)
);

CREATE TABLE MovimentacaoEstoque (
    IdMovimentacao  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,   -- identificador único
    CodigoProduto   INT          NOT NULL REFERENCES Produto (CodigoProduto),
    Tipo            CHAR(1)      NOT NULL,                     -- E = entrada, S = saída
    Quantidade      INT          NOT NULL,
    Descricao       VARCHAR(200) NOT NULL,                     -- ex.: 'Compra fornecedor'
    DataHora        DATETIME2    NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Mov_Tipo       CHECK (Tipo IN ('E', 'S')),
    CONSTRAINT CK_Mov_Quantidade CHECK (Quantidade > 0)
);

CREATE INDEX IX_Mov_Produto ON MovimentacaoEstoque (CodigoProduto, DataHora);
GO

INSERT INTO Produto (CodigoProduto, DescricaoProduto, Estoque) VALUES
    (101, 'Caneta Azul',               150),
    (102, 'Caderno Universitário',      75),
    (103, 'Borracha Branca',           200),
    (104, 'Lápis Preto HB',            320),
    (105, 'Marcador de Texto Amarelo',  90);
GO

/*
  Lança a movimentação e atualiza o saldo na mesma transação.
  O UPDATE com a condição de saldo evita estoque negativo mesmo com acessos simultâneos.
*/
CREATE OR ALTER PROCEDURE sp_MovimentarEstoque
    @CodigoProduto INT,
    @Tipo          CHAR(1),
    @Quantidade    INT,
    @Descricao     VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Tipo NOT IN ('E', 'S') THROW 50001, 'Tipo de movimentação inválido.', 1;
    IF @Quantidade <= 0        THROW 50002, 'A quantidade deve ser maior que zero.', 1;

    BEGIN TRANSACTION;

    UPDATE Produto
       SET Estoque = Estoque + CASE @Tipo WHEN 'E' THEN @Quantidade ELSE -@Quantidade END
     WHERE CodigoProduto = @CodigoProduto
       AND (@Tipo = 'E' OR Estoque >= @Quantidade);

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK;
        THROW 50003, 'Produto não encontrado ou saldo insuficiente.', 1;
    END

    INSERT INTO MovimentacaoEstoque (CodigoProduto, Tipo, Quantidade, Descricao)
    VALUES (@CodigoProduto, @Tipo, @Quantidade, @Descricao);

    COMMIT;

    -- Retorna o id da movimentação e a quantidade final do produto
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS IdMovimentacao,
           p.CodigoProduto, p.DescricaoProduto, p.Estoque AS EstoqueFinal
      FROM Produto p
     WHERE p.CodigoProduto = @CodigoProduto;
END
GO

-- Exemplos
EXEC sp_MovimentarEstoque 101, 'E', 50, 'Compra fornecedor';
EXEC sp_MovimentarEstoque 102, 'S', 30, 'Venda balcão';

-- Histórico de um produto
SELECT * FROM MovimentacaoEstoque WHERE CodigoProduto = 102 ORDER BY DataHora;
