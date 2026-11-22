namespace Financeiro.Dominio.Servicos;

/// <summary>
/// Sugestão estruturada para preencher o formulário. Ainda não é transação.
/// </summary>
public sealed record SugestaoInterpretacao(
    string? Tipo,
    decimal Valor,
    DateOnly Data,
    string Descricao,
    Guid? CategoriaId,
    string? CategoriaNaoEncontrada);

/// <summary>
/// Resultado da leitura de um texto. Nada é gravado no livro aqui.
/// </summary>
public sealed record ResultadoInterpretacao(
    bool Ok,
    SugestaoInterpretacao? Sugestao,
    string? Motivo);

/// <summary>
/// Interpreta linguagem natural em rascunho estruturado.
/// </summary>
public interface IInterpretadorTexto
{
    /// <summary>
    /// Lê o texto do usuário e devolve sugestão ou motivo de falha.
    /// </summary>
    Task<ResultadoInterpretacao> InterpretarAsync(
        Guid usuarioId,
        string texto,
        CancellationToken cancellationToken);
}
