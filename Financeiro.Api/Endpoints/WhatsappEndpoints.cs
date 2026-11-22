using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

public static class WhatsappEndpoints
{
    public static RouteGroupBuilder MapWhatsappEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/whatsapp").RequireAuthorization();

        grupo.MapGet("/", async (HttpContext http, IWhatsappLivro livro, CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var status = await livro.ObterStatusAsync(usuario.Id, cancellationToken);
            return Results.Ok(Mapear(status));
        });

        grupo.MapPost("/codigo", async (
            HttpContext http,
            GerarCodigoRequest corpo,
            IWhatsappLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                var codigo = await livro.GerarCodigoAsync(usuario.Id, corpo.Numero, cancellationToken);
                return Results.Ok(new
                {
                    numero = codigo.NumeroE164,
                    codigo = codigo.Codigo,
                    expiraEm = codigo.ExpiraEm,
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        grupo.MapDelete("/codigo", async (HttpContext http, IWhatsappLivro livro, CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            await livro.CancelarCodigoAsync(usuario.Id, cancellationToken);
            return Results.NoContent();
        });

        grupo.MapPost("/simular-confirmacao", async (
            HttpContext http,
            IWhatsappLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                await livro.SimularConfirmacaoAsync(usuario.Id, cancellationToken);
                var status = await livro.ObterStatusAsync(usuario.Id, cancellationToken);
                return Results.Ok(Mapear(status));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        grupo.MapDelete("/", async (HttpContext http, IWhatsappLivro livro, CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            await livro.DesvincularAsync(usuario.Id, cancellationToken);
            return Results.NoContent();
        });

        return grupo;
    }

    private static object Mapear(StatusWhatsapp status) => new
    {
        vinculo = status.Vinculo is null
            ? null
            : new
            {
                numero = status.Vinculo.NumeroE164,
                vinculadoEm = status.Vinculo.VinculadoEm,
            },
        codigoPendente = status.CodigoPendente is null
            ? null
            : new
            {
                numero = status.CodigoPendente.NumeroE164,
                codigo = status.CodigoPendente.Codigo,
                expiraEm = status.CodigoPendente.ExpiraEm,
            },
    };

    public sealed record GerarCodigoRequest(string Numero);
}
