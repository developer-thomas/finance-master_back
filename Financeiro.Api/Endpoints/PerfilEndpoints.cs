using Financeiro.Api.Autenticacao;

namespace Financeiro.Api.Endpoints;

public static class PerfilEndpoints
{
    public static RouteGroupBuilder MapPerfilEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api").RequireAuthorization();

        grupo.MapGet("/eu", (HttpContext http) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            return Results.Ok(new
            {
                id = usuario.Id,
                firebaseUid = usuario.FirebaseUid,
                nome = usuario.Nome,
                email = usuario.Email,
            });
        });

        return grupo;
    }
}
