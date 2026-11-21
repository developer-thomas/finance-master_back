using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Enums;
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
            return Results.Ok(contas.Select(c => new
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
            }));
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

                return Results.Created($"/api/contas/{conta.Id}", new
                {
                    id = conta.Id,
                    nome = conta.Nome,
                    tipo = conta.Tipo,
                    saldoInicial = conta.SaldoInicial,
                    ativa = conta.Ativa,
                    padrao = conta.Padrao,
                    criadoEm = conta.CriadoEm,
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { mensagem = ex.Message });
            }
        });

        return grupo;
    }

    public sealed record CriarContaRequest(string Nome, string Tipo, decimal SaldoInicial, bool Padrao);
}
