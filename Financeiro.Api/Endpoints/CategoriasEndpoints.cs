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

        grupo.MapPost("/", async (
            HttpContext http,
            CriarCategoriaRequest corpo,
            ICategoriasLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                var categoria = await livro.CriarAsync(usuario.Id, corpo.Tipo, corpo.Nome, cancellationToken);
                return Results.Created($"/api/categorias/{categoria.Id}", new
                {
                    id = categoria.Id,
                    tipo = categoria.Tipo,
                    nome = categoria.Nome,
                    escopo = categoria.Escopo,
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        return grupo;
    }

    public sealed record CriarCategoriaRequest(string Tipo, string Nome);
}
