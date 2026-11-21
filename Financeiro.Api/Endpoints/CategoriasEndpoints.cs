using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

public static class CategoriasEndpoints
{
    public static RouteGroupBuilder MapCategoriasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/categorias").RequireAuthorization();

        grupo.MapGet("/", async (HttpContext http, ICategoriasLivro livro, CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var categorias = await livro.ListarVisiveisAsync(usuario.Id, cancellationToken);
            return Results.Ok(categorias.Select(c => new
            {
                id = c.Id,
                tipo = c.Tipo,
                nome = c.Nome,
                escopo = c.Escopo,
            }));
        });

        return grupo;
    }
}
