using Financeiro.Dominio.Relogio;
using FluentAssertions;

namespace Financeiro.Dominio.Testes;

public class RelogioSaoPauloTestes
{
    [Fact]
    public void Hoje_UsaFusoDeSaoPaulo_EAgora_EUtc()
    {
        var relogio = new RelogioSaoPaulo();

        var hoje = relogio.Hoje();
        var agora = relogio.Agora();

        agora.Offset.Should().Be(TimeSpan.Zero);

        var fuso = ResolveFuso();
        var esperado = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, fuso).DateTime);
        hoje.Should().Be(esperado);
    }

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
