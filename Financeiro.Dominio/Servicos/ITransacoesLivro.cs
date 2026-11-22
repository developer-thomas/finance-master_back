using Financeiro.Dominio.Entidades;

namespace Financeiro.Dominio.Servicos;

public sealed record DadosTransacao(
    string Tipo,
    decimal Valor,
    DateOnly? DataMovimentacao,
    string? Descricao,
    Guid CategoriaId,
    Guid ContaId,
    string? Origem,
    string? IdentificadorMensagem = null);

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

/// <summary>
/// Contrato do livro de transações e extrato.
/// </summary>
public interface ITransacoesLivro
{
    /// <summary>Extrato filtrado, mais recente primeiro.</summary>
    Task<IReadOnlyList<TransacaoDetalhe>> ListarAsync(
        Guid usuarioId,
        FiltroTransacoes filtro,
        CancellationToken cancellationToken);

    /// <summary>Detalhe de uma transação válida.</summary>
    Task<TransacaoDetalhe?> ObterAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken);

    /// <summary>Cria lançamento após validar regras do domínio.</summary>
    Task<Transacao> CriarAsync(Guid usuarioId, DadosTransacao dados, CancellationToken cancellationToken);

    /// <summary>Edita lançamento existente.</summary>
    Task<Transacao> EditarAsync(Guid usuarioId, Guid id, DadosTransacao dados, CancellationToken cancellationToken);

    /// <summary>Exclusão lógica da transação.</summary>
    Task ExcluirAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken);
}
