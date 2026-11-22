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

public interface IWhatsappLivro
{
    Task<StatusWhatsapp> ObterStatusAsync(Guid usuarioId, CancellationToken cancellationToken);

    Task<CodigoVinculoAtivo> GerarCodigoAsync(Guid usuarioId, string numeroDigitado, CancellationToken cancellationToken);

    Task CancelarCodigoAsync(Guid usuarioId, CancellationToken cancellationToken);

    Task ConfirmarPorCodigoAsync(string numeroE164, string codigo, CancellationToken cancellationToken);

    Task SimularConfirmacaoAsync(Guid usuarioId, CancellationToken cancellationToken);

    Task DesvincularAsync(Guid usuarioId, CancellationToken cancellationToken);
}
