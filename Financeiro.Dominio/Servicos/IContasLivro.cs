using Financeiro.Dominio.Entidades;

namespace Financeiro.Dominio.Servicos;

public sealed record ContaComSaldo(
    ContaFinanceira Conta,
    decimal Saldo,
    bool TemLancamentos);

public sealed record DadosConta(
    string Nome,
    string Tipo,
    decimal SaldoInicial,
    bool Padrao);

/// <summary>
/// Contrato do livro de contas financeiras.
/// </summary>
public interface IContasLivro
{
    /// <summary>Lista contas com saldo e indicador de lançamentos.</summary>
    Task<IReadOnlyList<ContaComSaldo>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Cria conta ativa e define padrão quando necessário.</summary>
    Task<ContaFinanceira> CriarAsync(Guid usuarioId, DadosConta dados, CancellationToken cancellationToken);

    /// <summary>Edita conta respeitando bloqueio do saldo inicial.</summary>
    Task<ContaFinanceira> EditarAsync(Guid usuarioId, Guid id, DadosConta dados, CancellationToken cancellationToken);

    /// <summary>Desativa conta e promove outra padrão se preciso.</summary>
    Task DesativarAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken);

    /// <summary>Reativa conta sem roubar padrão existente.</summary>
    Task ReativarAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken);
}
