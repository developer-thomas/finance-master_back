using Financeiro.Dominio.Entidades;

namespace Financeiro.Dominio.Servicos;

public sealed record GastoPorCategoria(
    Guid CategoriaId,
    string Nome,
    decimal Total,
    decimal Percentual);

public sealed record ResumoMes(
    string Mes,
    decimal Receitas,
    decimal Despesas,
    decimal Resultado,
    IReadOnlyList<GastoPorCategoria> GastosPorCategoria);

public sealed record PainelResumo(
    decimal SaldoTotal,
    ResumoMes ResumoMes,
    IReadOnlyList<TransacaoDetalhe> Recentes,
    bool SemMovimentacao);

/// <summary>
/// Contrato das leituras do dashboard.
/// </summary>
public interface IPainelLivro
{
    /// <summary>Calcula saldo total, resumo do mês e recentes.</summary>
    Task<PainelResumo> ObterAsync(Guid usuarioId, CancellationToken cancellationToken);
}
