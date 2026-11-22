using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

/// <summary>
/// Endpoints de interpretação de texto (rascunho para o formulário).
/// </summary>
public static class InterpretacaoEndpoints
{
    public static RouteGroupBuilder MapInterpretacaoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/interpretacao").RequireAuthorization();

        grupo.MapPost("/texto", async (
            HttpContext http,
            InterpretarTextoRequest corpo,
            IInterpretadorTexto interpretador,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var resultado = await interpretador.InterpretarAsync(usuario.Id, corpo.Texto ?? string.Empty, cancellationToken);

            if (!resultado.Ok || resultado.Sugestao is null)
            {
                return Results.Ok(new
                {
                    ok = false,
                    motivo = resultado.Motivo ?? "Não foi possível interpretar o texto.",
                });
            }

            var s = resultado.Sugestao;
            return Results.Ok(new
            {
                ok = true,
                sugestao = new
                {
                    tipo = s.Tipo,
                    valor = s.Valor,
                    data = s.Data.ToString("yyyy-MM-dd"),
                    descricao = s.Descricao,
                    categoriaId = s.CategoriaId,
                    categoriaNaoEncontrada = s.CategoriaNaoEncontrada,
                },
            });
        });

        return grupo;
    }

    public sealed record InterpretarTextoRequest(string? Texto);
}
