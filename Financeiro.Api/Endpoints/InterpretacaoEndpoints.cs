using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

/// <summary>
/// Endpoints de interpretação (rascunho para o formulário — não grava no livro).
/// </summary>
public static class InterpretacaoEndpoints
{
    private const long TamanhoMaximoBytes = 10 * 1024 * 1024;

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
            var resultado = await interpretador.InterpretarAsync(
                usuario.Id,
                corpo.Texto ?? string.Empty,
                cancellationToken);
            return Resposta(resultado);
        });

        grupo.MapPost("/imagem", async (
            HttpContext http,
            IInterpretadorTexto interpretador,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            if (!http.Request.HasFormContentType)
            {
                return Results.BadRequest(new { ok = false, motivo = "Envie a imagem como multipart/form-data no campo arquivo." });
            }

            var form = await http.Request.ReadFormAsync(cancellationToken);
            var arquivo = form.Files.GetFile("arquivo");
            if (arquivo is null || arquivo.Length == 0)
            {
                return Results.BadRequest(new { ok = false, motivo = "Selecione uma imagem." });
            }

            if (arquivo.Length > TamanhoMaximoBytes)
            {
                return Results.BadRequest(new
                {
                    ok = false,
                    motivo = "A imagem passa de 10 MB. Tente uma foto menor.",
                });
            }

            await using var memoria = new MemoryStream();
            await arquivo.CopyToAsync(memoria, cancellationToken);
            var resultado = await interpretador.InterpretarImagemAsync(
                usuario.Id,
                memoria.ToArray(),
                arquivo.ContentType ?? "image/jpeg",
                cancellationToken);
            return Resposta(resultado);
        }).DisableAntiforgery();

        return grupo;
    }

    private static IResult Resposta(ResultadoInterpretacao resultado)
    {
        if (!resultado.Ok || resultado.Sugestao is null)
        {
            return Results.Ok(new
            {
                ok = false,
                motivo = resultado.Motivo ?? "Não foi possível interpretar.",
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
    }

    public sealed record InterpretarTextoRequest(string? Texto);
}
