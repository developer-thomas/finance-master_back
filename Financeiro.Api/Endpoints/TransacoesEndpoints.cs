using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

public static class TransacoesEndpoints
{
    public static RouteGroupBuilder MapTransacoesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/transacoes").RequireAuthorization();

        grupo.MapGet("/", async (
            HttpContext http,
            ITransacoesLivro livro,
            DateOnly? de,
            DateOnly? ate,
            string? tipo,
            Guid? categoriaId,
            Guid? contaId,
            string? busca,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var lista = await livro.ListarAsync(
                usuario.Id,
                new FiltroTransacoes(de, ate, tipo, categoriaId, contaId, busca),
                cancellationToken);

            return Results.Ok(lista.Select(Mapear));
        });

        grupo.MapGet("/{id:guid}", async (
            HttpContext http,
            Guid id,
            ITransacoesLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var item = await livro.ObterAsync(usuario.Id, id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(Mapear(item));
        });

        grupo.MapPost("/", async (
            HttpContext http,
            CriarTransacaoRequest corpo,
            ITransacoesLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                var criada = await livro.CriarAsync(
                    usuario.Id,
                    new DadosTransacao(
                        corpo.Tipo,
                        corpo.Valor,
                        corpo.Data,
                        corpo.Descricao,
                        corpo.CategoriaId,
                        corpo.ContaId,
                        corpo.Origem),
                    cancellationToken);

                var detalhe = await livro.ObterAsync(usuario.Id, criada.Id, cancellationToken);
                return Results.Created($"/api/transacoes/{criada.Id}", Mapear(detalhe!));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        grupo.MapPut("/{id:guid}", async (
            HttpContext http,
            Guid id,
            EditarTransacaoRequest corpo,
            ITransacoesLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                await livro.EditarAsync(
                    usuario.Id,
                    id,
                    new DadosTransacao(
                        corpo.Tipo,
                        corpo.Valor,
                        corpo.Data,
                        corpo.Descricao,
                        corpo.CategoriaId,
                        corpo.ContaId,
                        Origem: null),
                    cancellationToken);

                var detalhe = await livro.ObterAsync(usuario.Id, id, cancellationToken);
                return Results.Ok(Mapear(detalhe!));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        grupo.MapDelete("/{id:guid}", async (
            HttpContext http,
            Guid id,
            ITransacoesLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                await livro.ExcluirAsync(usuario.Id, id, cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        return grupo;
    }

    private static object Mapear(TransacaoDetalhe item) => new
    {
        id = item.Transacao.Id,
        contaId = item.Transacao.ContaId,
        contaNome = item.ContaNome,
        categoriaId = item.Transacao.CategoriaId,
        categoriaNome = item.CategoriaNome,
        tipo = item.Transacao.Tipo,
        valor = item.Transacao.Valor,
        data = item.Transacao.DataMovimentacao.ToString("yyyy-MM-dd"),
        descricao = item.Transacao.Descricao,
        origem = item.Transacao.Origem,
        criadoEm = item.Transacao.CriadoEm,
        atualizadoEm = item.Transacao.AtualizadoEm,
    };

    public sealed record CriarTransacaoRequest(
        string Tipo,
        decimal Valor,
        DateOnly? Data,
        string? Descricao,
        Guid CategoriaId,
        Guid ContaId,
        string? Origem);

    public sealed record EditarTransacaoRequest(
        string Tipo,
        decimal Valor,
        DateOnly? Data,
        string? Descricao,
        Guid CategoriaId,
        Guid ContaId);
}
