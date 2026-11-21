using Financeiro.Dominio.Entidades;

namespace Financeiro.Dominio.Servicos;

public sealed record DadosTransacao(
    string Tipo,
    decimal Valor,
    DateOnly? DataMovimentacao,
    string? Descricao,
    Guid CategoriaId,
    Guid ContaId,
    string? Origem);

public sealed record FiltroTransacoes(
    DateOnly? De,
    DateOnly? Ate,
    string? Tipo,
    Guid? CategoriaId,
    Guid? ContaId,
    string? Busca);

public sealed record TransacaoDetalhe(
    Transacao Transacao,
    string ContaNome,
    string CategoriaNome);

public interface ITransacoesLivro
{
    Task<IReadOnlyList<TransacaoDetalhe>> ListarAsync(
        Guid usuarioId,
        FiltroTransacoes filtro,
        CancellationToken cancellationToken);

    Task<TransacaoDetalhe?> ObterAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken);

    Task<Transacao> CriarAsync(Guid usuarioId, DadosTransacao dados, CancellationToken cancellationToken);

    Task<Transacao> EditarAsync(Guid usuarioId, Guid id, DadosTransacao dados, CancellationToken cancellationToken);

    Task ExcluirAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken);
}
