using System.Text.Json;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

/// <summary>
/// Webhook da Meta Cloud API: verificação e recebimento de mensagens de texto.
/// </summary>
public static class WhatsappWebhookEndpoints
{
    public static IEndpointRouteBuilder MapWhatsappWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/whatsapp/webhook");

        // Challenge de verificação do webhook (hub.challenge).
        grupo.MapGet("/", (
            string? hub_mode,
            string? hub_verify_token,
            string? hub_challenge,
            IConfiguration config) =>
        {
            var esperado = config["WhatsApp:VerifyToken"];
            if (string.IsNullOrWhiteSpace(esperado))
            {
                return Results.NotFound();
            }

            if (hub_mode == "subscribe" &&
                string.Equals(hub_verify_token, esperado, StringComparison.Ordinal))
            {
                return Results.Text(hub_challenge ?? string.Empty);
            }

            return Results.Unauthorized();
        });

        // Recebe notificações; processa apenas mensagens de texto.
        grupo.MapPost("/", async (
            HttpRequest request,
            IMensagensWhatsappLivro livro,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("WhatsappWebhook");
            using var documento = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            var raiz = documento.RootElement;

            if (!raiz.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return Results.Ok();
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                    {
                        continue;
                    }

                    if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var message in messages.EnumerateArray())
                    {
                        var tipo = message.TryGetProperty("type", out var tipoEl) ? tipoEl.GetString() : null;
                        if (!string.Equals(tipo, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogInformation("Mensagem WhatsApp ignorada: tipo {Tipo}", tipo);
                            continue;
                        }

                        var de = message.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;
                        var id = message.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        var texto = message.TryGetProperty("text", out var textEl) &&
                                    textEl.TryGetProperty("body", out var bodyEl)
                            ? bodyEl.GetString()
                            : null;

                        if (string.IsNullOrWhiteSpace(de) ||
                            string.IsNullOrWhiteSpace(id) ||
                            string.IsNullOrWhiteSpace(texto))
                        {
                            continue;
                        }

                        // Meta envia "from" como 5511... sem '+'.
                        var numero = de.StartsWith("+", StringComparison.Ordinal) ? de : $"+{de}";
                        var resultado = await livro.ProcessarTextoAsync(numero, texto, id, cancellationToken);
                        logger.LogInformation(
                            "WhatsApp {Id} efeito={Efeito}: {Resposta}",
                            id,
                            resultado.Efeito,
                            resultado.Resposta);
                    }
                }
            }

            // A Meta exige 200 rápido; respostas outbound ficam para etapa com WHATSAPP_TOKEN.
            return Results.Ok();
        });

        return app;
    }
}
