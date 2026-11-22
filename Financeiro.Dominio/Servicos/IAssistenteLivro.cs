namespace Financeiro.Dominio.Servicos;

/// <summary>
/// Papéis de uma mensagem do chat do assistente.
/// </summary>
public static class PapelAssistente
{
    public const string Usuario = "usuario";
    public const string Assistente = "assistente";
}

/// <summary>
/// Turno já ocorrido na conversa (enviado pelo cliente; o servidor não persiste ainda).
/// </summary>
public sealed record MensagemAssistenteHistorico(string Papel, string Texto);

/// <summary>
/// Resposta do assistente. Números citados devem vir do livro, não da IA inventada.
/// </summary>
public sealed record RespostaAssistente(string Texto, bool UsouIa);

/// <summary>
/// Conversa sobre a saúde financeira do usuário, com fatos calculados no backend.
/// </summary>
public interface IAssistenteLivro
{
    /// <summary>
    /// Responde à mensagem com base no snapshot oficial do livro (+ Gemini quando configurado).
    /// </summary>
    Task<RespostaAssistente> ResponderAsync(
        Guid usuarioId,
        string mensagem,
        IReadOnlyList<MensagemAssistenteHistorico>? historico,
        CancellationToken cancellationToken);
}
