namespace Financeiro.Dominio.Servicos;

public sealed record CodigoVinculoAtivo(
    string NumeroE164,
    string Codigo,
    DateTimeOffset ExpiraEm);

public sealed record VinculoWhatsappAtivo(
    string NumeroE164,
    DateTimeOffset VinculadoEm);

public sealed record StatusWhatsapp(
    VinculoWhatsappAtivo? Vinculo,
    CodigoVinculoAtivo? CodigoPendente);

/// <summary>
/// Contrato do vínculo WhatsApp e códigos de posse do número.
/// </summary>
public interface IWhatsappLivro
{
    /// <summary>Status do vínculo e código pendente.</summary>
    Task<StatusWhatsapp> ObterStatusAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Emite código de 15 minutos para o número informado.</summary>
    Task<CodigoVinculoAtivo> GerarCodigoAsync(Guid usuarioId, string numeroDigitado, CancellationToken cancellationToken);

    /// <summary>Cancela o código pendente.</summary>
    Task CancelarCodigoAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Confirma vínculo a partir do número + código (webhook).</summary>
    Task ConfirmarPorCodigoAsync(string numeroE164, string codigo, CancellationToken cancellationToken);

    /// <summary>Confirma o código pendente sem mensagem real (dev).</summary>
    Task SimularConfirmacaoAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Remove o vínculo do usuário.</summary>
    Task DesvincularAsync(Guid usuarioId, CancellationToken cancellationToken);
}
