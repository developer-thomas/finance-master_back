using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

public static class ContasEndpoints
{
    public static RouteGroupBuilder MapContasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/contas").RequireAuthorization();

        grupo.MapGet("/", async (HttpContext http, IContasLivro livro, CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var contas = await livro.ListarAsync(usuario.Id, cancellationToken);
            return Results.Ok(contas.Select(Mapear));
        });

        grupo.MapPost("/", async (HttpContext http, CriarContaRequest corpo, IContasLivro livro, CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                var conta = await livro.CriarAsync(
                    usuario.Id,
                    new DadosConta(corpo.Nome, corpo.Tipo, corpo.SaldoInicial, corpo.Padrao),
                    cancellationToken);
                var listagem = await livro.ListarAsync(usuario.Id, cancellationToken);
                var criada = listagem.First(c => c.Conta.Id == conta.Id);
                return Results.Created($"/api/contas/{conta.Id}", Mapear(criada));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        grupo.MapPut("/{id:guid}", async (
            HttpContext http,
            Guid id,
            CriarContaRequest corpo,
            IContasLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                await livro.EditarAsync(
                    usuario.Id,
                    id,
                    new DadosConta(corpo.Nome, corpo.Tipo, corpo.SaldoInicial, corpo.Padrao),
                    cancellationToken);
                var listagem = await livro.ListarAsync(usuario.Id, cancellationToken);
                var editada = listagem.First(c => c.Conta.Id == id);
                return Results.Ok(Mapear(editada));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        grupo.MapPost("/{id:guid}/desativar", async (
            HttpContext http,
            Guid id,
            IContasLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                await livro.DesativarAsync(usuario.Id, id, cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        grupo.MapPost("/{id:guid}/reativar", async (
            HttpContext http,
            Guid id,
            IContasLivro livro,
            CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            try
            {
                await livro.ReativarAsync(usuario.Id, id, cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        return grupo;
    }

    private static object Mapear(ContaComSaldo c) => new
    {
        id = c.Conta.Id,
        nome = c.Conta.Nome,
        tipo = c.Conta.Tipo,
        saldoInicial = c.Conta.SaldoInicial,
        saldo = c.Saldo,
        ativa = c.Conta.Ativa,
        padrao = c.Conta.Padrao,
        temLancamentos = c.TemLancamentos,
        criadoEm = c.Conta.CriadoEm,
    };

    public sealed record CriarContaRequest(string Nome, string Tipo, decimal SaldoInicial, bool Padrao);
}
