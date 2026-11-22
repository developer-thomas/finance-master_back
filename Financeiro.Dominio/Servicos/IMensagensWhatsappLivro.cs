namespace Financeiro.Dominio.Servicos;

/// <summary>
/// Resultado do processamento de uma mensagem inbound do WhatsApp.
/// </summary>
public sealed record ResultadoMensagemWhatsapp(
    string Status,
    string Resposta,
    Guid? TransacaoId,
    string? Efeito);

/// <summary>
/// Processa texto recebido de um número (vínculo, lançamento ou recusa).
/// </summary>
public interface IMensagensWhatsappLivro
{
    /// <summary>
    /// Trata uma mensagem de texto. Idempotente pelo identificador externo.
    /// Na etapa atual, texto incompleto só responde o que faltou — sem operação pendente.
    /// </summary>
    Task<ResultadoMensagemWhatsapp> ProcessarTextoAsync(
        string numeroDigitado,
        string texto,
        string identificadorExterno,
        CancellationToken cancellationToken);
}
