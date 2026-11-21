namespace Financeiro.Dominio.Relogio;

public sealed class RelogioSaoPaulo : IRelogioDominio
{
    private static readonly TimeZoneInfo Fuso = ResolveFuso();

    public DateOnly Hoje()
    {
        var agoraLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Fuso);
        return DateOnly.FromDateTime(agoraLocal.DateTime);
    }

    /// <summary>
    /// Instantes persistidos no PostgreSQL (timestamptz) precisam ser UTC.
    /// </summary>
    public DateTimeOffset Agora() => DateTimeOffset.UtcNow;

    private static TimeZoneInfo ResolveFuso()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
