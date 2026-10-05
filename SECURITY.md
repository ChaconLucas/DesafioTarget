# Segurança

Este documento registra a validação de segurança feita no projeto: o que foi analisado, o que foi encontrado, o que foi corrigido e os riscos que continuam em aberto por decisão consciente.

## Escopo

| Componente | Exposição |
|------------|-----------|
| `Desafio.Core` | Regras de negócio, usadas pelo console e pela web |
| `Desafio.App` | Console local, sem rede |
| `Desafio.Web` | Servidor HTTP com interface e API REST — **principal superfície de ataque** |
| `Dockerfile` | Imagem de execução da interface web |

## Como foi validado

1. **Revisão manual do código** seguindo o OWASP Top 10 (2021) e o OWASP API Security Top 10 (2023).
2. **Testes de ataque contra o servidor rodando**, com `curl`: valores extremos, corpo gigante, cabeçalho `Host` forjado, JSON malformado e enum inválido.
3. **Varredura de dependências** com `dotnet list package --vulnerable --include-transitive`: **nenhum pacote vulnerável** (05/10/2026).
4. **Testes automatizados**: cada correção abaixo tem um teste em `tests/Desafio.Tests` que falharia se a proteção fosse removida.

## Problemas encontrados e corrigidos

| # | Severidade | Problema | Correção | Teste |
|---|-----------|----------|----------|-------|
| 1 | **Alta** | **Estouro de inteiro no estoque.** Uma entrada de `2147483647` unidades fazia o saldo virar `-2147483499`, furando a regra "o estoque nunca fica negativo". | Quantidade máxima de 1.000.000 por movimentação, e a soma é feita em contexto `checked`: se estourar, a operação é recusada e o saldo não muda. | `Entrada_que_estouraria_o_int_gera_erro_e_nao_altera_o_saldo`, `Entrada_que_estouraria_o_estoque_devolve_400` |
| 2 | Média | **Erro 500 nos juros com valores extremos.** `valor=79228162514264337593543950335` estourava o `decimal`. | Valor máximo de R$ 1 trilhão e taxa entre 0% e 100%. Com esses limites, o cálculo cabe no `decimal` mesmo com o atraso máximo possível. | `Juros_com_valor_extremo_devolve_400_e_nao_500`, `Valor_maximo_com_atraso_maximo_nao_estoura` |
| 3 | Média | **Descrição e corpo da requisição sem limite.** Uma descrição de 100 KB era aceita e gravada em disco; repetida, enchia o arquivo de histórico. | Descrição de no máximo 200 caracteres, corpo da requisição de no máximo 16 KB (`413` acima disso) e `maxlength` nos campos da página. | `Descricao_muito_longa_devolve_400`, `Descricao_longa_demais_gera_erro` |
| 4 | Média | **Sem limite de requisições.** Um script podia lançar movimentações sem parar. | *Rate limiting* de 60 escritas por minuto por IP no `POST` e no `DELETE` (configurável em `Seguranca:EscritasPorMinuto`), com resposta `429`. | `Excesso_de_lancamentos_devolve_429` |
| 5 | Média | **Qualquer cabeçalho `Host` era aceito**, abrindo espaço para *DNS rebinding*: um site malicioso aberto no navegador poderia falar com o servidor local. | `AllowedHosts` restrito a `localhost`, `127.0.0.1` e `[::1]`. | `Host_desconhecido_e_rejeitado` |
| 6 | Média | **Nenhum cabeçalho de segurança** e o servidor se identificava (`Server: Kestrel`). | CSP restritiva (`script-src 'self'`, `frame-ancestors 'none'`, `object-src 'none'`…), `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`. Cabeçalho `Server` removido. O JavaScript da página saiu do HTML para `app.js`, o que permite a CSP sem `'unsafe-inline'` para scripts. | `Respostas_tem_cabecalhos_de_seguranca` |
| 7 | Baixa | **JSON malformado ou tipo inválido (`"Roubo"`) devolvia 500** em desenvolvimento, com detalhes da exceção. | Requisições inválidas sempre devolvem `400`; erros inesperados viram `ProblemDetails` sem stack trace. | `Corpo_invalido_devolve_400_sem_detalhes_internos` |
| 8 | Baixa | **Swagger exposto em qualquer ambiente**, documentando a API para quem a acessasse. | Swagger só no ambiente `Development` (o padrão do `dotnet run`). Na imagem Docker, que roda em `Production`, ele fica desligado. | `Swagger_nao_fica_exposto_em_producao` |
| 9 | Baixa | **O trecho do endereço depois do `#`** montava um seletor CSS na página; um valor como `#"]x` quebrava o script. | Só nomes de aba conhecidos são aceitos. | Verificado manualmente no navegador |
| 10 | Baixa | **Container rodando como root.** | A imagem usa o usuário sem privilégios da imagem oficial (`$APP_UID`); só `App_Data/` tem permissão de escrita. | — |

## O que já estava protegido

- **Injeção de HTML / XSS:** todo texto vindo da API passa por `esc()` antes de entrar na página. Com a CSP, mesmo uma falha de escape não executaria script de outra origem nem script inline.
- **CSRF:** a API só aceita `application/json`. Um formulário de outro site não consegue mandar esse tipo de conteúdo, e um `fetch` de outra origem dispara *preflight* CORS, que é recusado porque não há política CORS liberando outras origens.
- **Validação no servidor:** todas as regras (produto existente, quantidade, descrição, saldo) ficam no `Desafio.Core` e valem para qualquer cliente, não só para a página.
- **Integridade do histórico:** o arquivo é gravado em um temporário e depois trocado (escrita atômica). Ao iniciar, o histórico é reaplicado passando pelas mesmas validações; um arquivo corrompido ou adulterado é separado (`.invalido`) e a aplicação volta ao saldo inicial em vez de carregar dados inconsistentes.
- **Concorrência:** as operações de estoque são serializadas com `lock`, então duas saídas simultâneas não conseguem passar do saldo.
- **Caminhos de arquivo:** nenhum caminho vem do usuário; todos vêm da configuração do servidor.
- **Dinheiro:** valores em `decimal`, sem erros de arredondamento de ponto flutuante.

## Riscos aceitos

Estes pontos ficaram em aberto **de propósito**, porque o projeto é um desafio técnico pensado para rodar na máquina do avaliador:

| Risco | Por que foi aceito | O que fazer em produção |
|-------|--------------------|-------------------------|
| **Sem autenticação.** Quem acessa o servidor pode lançar movimentações e apagar o histórico (`DELETE`). | O servidor só aceita conexões com `Host` local, e o enunciado não pede controle de usuários. | Autenticação (ex.: OpenID Connect) e autorização por perfil, com o `DELETE` restrito a administradores. |
| **HTTP sem TLS.** | Em `localhost` o tráfego não sai da máquina. | Publicar atrás de um proxy reverso com HTTPS e ativar HSTS. |
| **`'unsafe-inline'` em `style-src`.** | Necessário para as larguras das barras (atributo `style`). Estilo inline não executa código. | Trocar as larguras por classes ou variáveis CSS e remover a exceção. |
| **Fonte carregada do Google Fonts.** | Só afeta a aparência; a página funciona sem ela. | Servir a fonte pela própria aplicação e tirar os domínios do Google da CSP. |
| **Histórico em arquivo JSON.** | Suficiente para uma aplicação de uma só instância. | Usar o modelo de `sql/estoque.sql`, com transação e constraints no banco. |

## Configuração

| Chave | Padrão | Para que serve |
|-------|--------|----------------|
| `AllowedHosts` | `localhost;127.0.0.1;[::1]` | Hosts aceitos. Para acessar por outro nome ou IP, inclua-o aqui (ex.: variável de ambiente `AllowedHosts`). |
| `Seguranca:EscritasPorMinuto` | `60` | Limite de `POST`/`DELETE` por IP por minuto. |
| `Estoque:ArquivoHistorico` | `App_Data/movimentacoes.json` | Onde o histórico é gravado. |

## Como reproduzir a validação

```bash
dotnet test                                                # 55 testes, incluindo os de segurança
dotnet list package --vulnerable --include-transitive      # dependências vulneráveis
```

## Reportando um problema

Encontrou uma vulnerabilidade? Abra uma *issue* no repositório descrevendo como reproduzir.
