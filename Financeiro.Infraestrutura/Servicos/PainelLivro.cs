using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Leituras do dashboard: saldo total, resumo do mês e transações recentes.
/// </summary>
public sealed class PainelLivro : IPainelLivro
{
    private const int LimiteRecentes = 6;

    private readonly FinanceiroDbContext _db;
    private readonly IContasLivro _contas;
    private readonly IRelogioDominio _relogio;

    public PainelLivro(FinanceiroDbContext db, IContasLivro contas, IRelogioDominio relogio)
    {
        _db = db;
        _contas = contas;
        _relogio = relogio;
    }

    /// <summary>
    /// Monta o painel do usuário com dados até o dia civil em America/Sao_Paulo.
    /// </summary>
    public async Task<PainelResumo> ObterAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var hoje = _relogio.Hoje();
        var mes = $"{hoje.Year:D4}-{hoje.Month:D2}";
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);

        var contasComSaldo = await _contas.ListarAsync(usuarioId, cancellationToken);
        var saldoTotal = contasComSaldo
            .Where(c => c.Conta.Ativa)
            .Sum(c => c.Saldo);

        var doMes = await _db.Transacoes
            .AsNoTracking()
            .Include(t => t.Conta)
            .Include(t => t.Categoria)
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.DataMovimentacao >= inicioMes &&
                t.DataMovimentacao <= hoje)
            .ToListAsync(cancellationToken);

        var receitas = doMes.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => t.Valor);
        var despesasLista = doMes.Where(t => t.Tipo == TipoTransacao.Despesa).ToList();
        var despesas = despesasLista.Sum(t => t.Valor);

        var gastosPorCategoria = despesasLista
            .GroupBy(t => new { t.CategoriaId, t.Categoria.Nome })
            .Select(g =>
            {
                var total = g.Sum(t => t.Valor);
                return new GastoPorCategoria(
                    g.Key.CategoriaId,
                    g.Key.Nome,
                    total,
                    despesas == 0 ? 0 : decimal.Round(total / despesas, 4, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(g => g.Total)
            .ToList();

        var recentes = await _db.Transacoes
            .AsNoTracking()
            .Include(t => t.Conta)
            .Include(t => t.Categoria)
            .Where(t => t.UsuarioId == usuarioId && t.DataMovimentacao <= hoje)
            .OrderByDescending(t => t.DataMovimentacao)
            .ThenByDescending(t => t.CriadoEm)
            .Take(LimiteRecentes)
            .ToListAsync(cancellationToken);

        var temMovimentacao = await _db.Transacoes
            .AsNoTracking()
            .AnyAsync(t => t.UsuarioId == usuarioId, cancellationToken);

        return new PainelResumo(
            saldoTotal,
            new ResumoMes(mes, receitas, despesas, receitas - despesas, gastosPorCategoria),
            recentes.Select(t => new TransacaoDetalhe(t, t.Conta.Nome, t.Categoria.Nome)).ToList(),
            !temMovimentacao);
    }
}
