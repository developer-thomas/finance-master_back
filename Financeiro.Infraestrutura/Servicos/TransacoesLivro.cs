using System.Globalization;
using System.Text;
using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Livro de transações: extrato filtrado, criação, edição e exclusão lógica.
/// </summary>
public sealed class TransacoesLivro : ITransacoesLivro
{
    private const int LimiteDescricao = 120;

    private readonly FinanceiroDbContext _db;
    private readonly IRelogioDominio _relogio;

    public TransacoesLivro(FinanceiroDbContext db, IRelogioDominio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    /// <summary>
    /// Extrato do usuário com filtros combinados (período, tipo, categoria, conta e busca).
    /// </summary>
    public async Task<IReadOnlyList<TransacaoDetalhe>> ListarAsync(
        Guid usuarioId,
        FiltroTransacoes filtro,
        CancellationToken cancellationToken)
    {
        var query = _db.Transacoes
            .AsNoTracking()
            .Include(t => t.Conta)
            .Include(t => t.Categoria)
            .Where(t => t.UsuarioId == usuarioId);

        if (filtro.De is not null)
        {
            query = query.Where(t => t.DataMovimentacao >= filtro.De);
        }

        if (filtro.Ate is not null)
        {
            query = query.Where(t => t.DataMovimentacao <= filtro.Ate);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            query = query.Where(t => t.Tipo == filtro.Tipo);
        }

        if (filtro.CategoriaId is not null)
        {
            query = query.Where(t => t.CategoriaId == filtro.CategoriaId);
        }

        if (filtro.ContaId is not null)
        {
            query = query.Where(t => t.ContaId == filtro.ContaId);
        }

        var lista = await query
            .OrderByDescending(t => t.DataMovimentacao)
            .ThenByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);

        var termo = NormalizarBusca(filtro.Busca);
        if (termo.Length > 0)
        {
            lista = lista
                .Where(t =>
                    NormalizarBusca(t.Descricao).Contains(termo, StringComparison.Ordinal) ||
                    NormalizarBusca(t.Categoria.Nome).Contains(termo, StringComparison.Ordinal))
                .ToList();
        }

        return lista.Select(MapearDetalhe).ToList();
    }

    /// <summary>
    /// Obtém uma transação válida (não excluída) do dono.
    /// </summary>
    public async Task<TransacaoDetalhe?> ObterAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken)
    {
        var transacao = await _db.Transacoes
            .AsNoTracking()
            .Include(t => t.Conta)
            .Include(t => t.Categoria)
            .FirstOrDefaultAsync(t => t.UsuarioId == usuarioId && t.Id == id, cancellationToken);

        return transacao is null ? null : MapearDetalhe(transacao);
    }

    /// <summary>
    /// Grava uma nova transação após validar conta, categoria, valor e descrição.
    /// </summary>
    public async Task<Transacao> CriarAsync(Guid usuarioId, DadosTransacao dados, CancellationToken cancellationToken)
    {
        var validados = await ValidarAsync(usuarioId, dados, contaAtualId: null, cancellationToken);
        var agora = _relogio.Agora();

        var transacao = new Transacao
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            ContaId = validados.ContaId,
            CategoriaId = validados.CategoriaId,
            Tipo = validados.Tipo,
            Valor = validados.Valor,
            Moeda = "BRL",
            DataMovimentacao = validados.DataMovimentacao!.Value,
            Descricao = validados.Descricao!,
            Origem = validados.Origem!,
            IdentificadorMensagem = validados.IdentificadorMensagem,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        _db.Transacoes.Add(transacao);
        await _db.SaveChangesAsync(cancellationToken);
        return transacao;
    }

    /// <summary>
    /// Altera campos da transação. A conta atual pode permanecer mesmo se já estiver inativa.
    /// </summary>
    public async Task<Transacao> EditarAsync(
        Guid usuarioId,
        Guid id,
        DadosTransacao dados,
        CancellationToken cancellationToken)
    {
        var transacao = await _db.Transacoes
            .FirstOrDefaultAsync(t => t.UsuarioId == usuarioId && t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Esta transação não existe mais.");

        var validados = await ValidarAsync(usuarioId, dados, transacao.ContaId, cancellationToken);
        transacao.ContaId = validados.ContaId;
        transacao.CategoriaId = validados.CategoriaId;
        transacao.Tipo = validados.Tipo;
        transacao.Valor = validados.Valor;
        transacao.DataMovimentacao = validados.DataMovimentacao!.Value;
        transacao.Descricao = validados.Descricao!;
        transacao.AtualizadoEm = _relogio.Agora();

        await _db.SaveChangesAsync(cancellationToken);
        return transacao;
    }

    /// <summary>
    /// Exclusão lógica: marca <c>ExcluidaEm</c> e remove a transação dos cálculos.
    /// </summary>
    public async Task ExcluirAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken)
    {
        var transacao = await _db.Transacoes
            .FirstOrDefaultAsync(t => t.UsuarioId == usuarioId && t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Esta transação não existe mais.");

        transacao.ExcluidaEm = _relogio.Agora();
        transacao.AtualizadoEm = transacao.ExcluidaEm.Value;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Aplica as invariantes do domínio antes de gravar.
    /// </summary>
    private async Task<DadosTransacao> ValidarAsync(
        Guid usuarioId,
        DadosTransacao dados,
        Guid? contaAtualId,
        CancellationToken cancellationToken)
    {
        if (!TipoTransacao.Todos.Contains(dados.Tipo))
        {
            throw new InvalidOperationException("Tipo de transação inválido.");
        }

        if (dados.Valor <= 0)
        {
            throw new InvalidOperationException("Informe um valor maior que zero.");
        }

        var valor = decimal.Round(dados.Valor, 2, MidpointRounding.AwayFromZero);
        if (valor <= 0)
        {
            throw new InvalidOperationException("Informe um valor maior que zero.");
        }

        var conta = await _db.ContasFinanceiras
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.Id == dados.ContaId, cancellationToken)
            ?? throw new InvalidOperationException("Escolha uma conta.");

        if (!conta.Ativa && conta.Id != contaAtualId)
        {
            throw new InvalidOperationException("Esta conta está inativa. Escolha uma conta ativa.");
        }

        var categoria = await _db.Categorias
            .FirstOrDefaultAsync(
                c => c.Id == dados.CategoriaId &&
                     (c.Escopo == EscopoCategoria.Padrao ||
                      (c.Escopo == EscopoCategoria.Personalizada && c.UsuarioId == usuarioId)),
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Escolha uma categoria de {(dados.Tipo == TipoTransacao.Despesa ? "despesa" : "receita")}.");

        if (categoria.Tipo != dados.Tipo)
        {
            throw new InvalidOperationException(
                $"Escolha uma categoria de {(dados.Tipo == TipoTransacao.Despesa ? "despesa" : "receita")}.");
        }

        var data = dados.DataMovimentacao ?? _relogio.Hoje();
        var descricao = (dados.Descricao ?? string.Empty).Trim();
        if (descricao.Length > LimiteDescricao)
        {
            descricao = descricao[..LimiteDescricao];
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            descricao = categoria.Nome;
        }

        var origem = string.IsNullOrWhiteSpace(dados.Origem) ? OrigemTransacao.AppManual : dados.Origem.Trim();
        if (!OrigemTransacao.Todos.Contains(origem))
        {
            throw new InvalidOperationException("Origem da transação inválida.");
        }

        return new DadosTransacao(
            dados.Tipo,
            valor,
            data,
            descricao,
            categoria.Id,
            conta.Id,
            origem,
            string.IsNullOrWhiteSpace(dados.IdentificadorMensagem) ? null : dados.IdentificadorMensagem.Trim());
    }

    private static TransacaoDetalhe MapearDetalhe(Transacao t) =>
        new(t, t.Conta.Nome, t.Categoria.Nome);

    private static string NormalizarBusca(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var form = texto.Trim().ToLower(CultureInfo.GetCultureInfo("pt-BR")).Normalize(NormalizationForm.FormD);
        return string.Concat(form.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    }
}
