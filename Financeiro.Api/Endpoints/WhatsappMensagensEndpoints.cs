using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

/// <summary>
/// Entrada de mensagens WhatsApp. O endpoint /dev simula o webhook da Meta.
/// </summary>
public static class WhatsappMensagensEndpoints
{
    public static IEndpointRouteBuilder MapWhatsappMensagensEndpoints(this IEndpointRouteBuilder app)
    {
        // Simula inbound da Meta enquanto o webhook real não está configurado.
        app.MapPost("/api/whatsapp/dev/mensagem", async (
            MensagemDevRequest corpo,
            IMensagensWhatsappLivro livro,
            IHostEnvironment ambiente,
            CancellationToken cancellationToken) =>
        {
            if (!ambiente.IsDevelopment())
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(corpo.Numero) || string.IsNullOrWhiteSpace(corpo.Texto))
            {
                return Results.BadRequest(new { mensagem = "Informe numero e texto." });
            }

            var identificador = string.IsNullOrWhiteSpace(corpo.Identificador)
                ? $"dev-{Guid.NewGuid():N}"
                : corpo.Identificador.Trim();

            var resultado = await livro.ProcessarTextoAsync(
                corpo.Numero,
                corpo.Texto,
                identificador,
                cancellationToken);

            return Results.Ok(new
            {
                status = resultado.Status,
                efeito = resultado.Efeito,
                resposta = resultado.Resposta,
                transacaoId = resultado.TransacaoId,
                identificador,
            });
        });

        return app;
    }

    public sealed record MensagemDevRequest(string Numero, string Texto, string? Identificador);
}
