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

public interface IContasLivro
{
    Task<IReadOnlyList<ContaComSaldo>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken);

    Task<ContaFinanceira> CriarAsync(Guid usuarioId, DadosConta dados, CancellationToken cancellationToken);
}
