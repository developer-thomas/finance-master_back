using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

/// <summary>
/// Chat do assistente financeiro (fatos do livro + redação via IA).
/// </summary>
public static class AssistenteEndpoints
{
    public static RouteGroupBuilder MapAssistenteEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/assistente").RequireAuthorization();

        grupo.MapPost("/mensagem", async (
            HttpContext http,
            PedidoAssistenteRequest corpo,
            IAssistenteLivro assistente,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var historico = corpo.Historico?
                .Where(h => !string.IsNullOrWhiteSpace(h.Texto))
                .Select(h => new MensagemAssistenteHistorico(
                    string.IsNullOrWhiteSpace(h.Papel) ? PapelAssistente.Usuario : h.Papel.Trim(),
                    h.Texto!.Trim()))
                .ToList();

            var resposta = await assistente.ResponderAsync(
                usuario.Id,
                corpo.Mensagem ?? string.Empty,
                historico,
                cancellationToken);

            return Results.Ok(new
            {
                texto = resposta.Texto,
                usouIa = resposta.UsouIa,
            });
        });

        return grupo;
    }

    public sealed record PedidoAssistenteRequest(
        string? Mensagem,
        IReadOnlyList<MensagemHistoricoRequest>? Historico);

    public sealed record MensagemHistoricoRequest(string? Papel, string? Texto);
}
