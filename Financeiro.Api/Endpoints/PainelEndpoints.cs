using Financeiro.Api.Autenticacao;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Endpoints;

public static class PainelEndpoints
{
    public static RouteGroupBuilder MapPainelEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/painel").RequireAuthorization();

        grupo.MapGet("/", async (HttpContext http, IPainelLivro livro, CancellationToken cancellationToken) =>
        {
            var usuario = UsuarioHttp.Exigir(http);
            var painel = await livro.ObterAsync(usuario.Id, cancellationToken);

            return Results.Ok(new
            {
                saldoTotal = painel.SaldoTotal,
                semMovimentacao = painel.SemMovimentacao,
                resumoMes = new
                {
                    mes = painel.ResumoMes.Mes,
                    receitas = painel.ResumoMes.Receitas,
                    despesas = painel.ResumoMes.Despesas,
                    resultado = painel.ResumoMes.Resultado,
                    gastosPorCategoria = painel.ResumoMes.GastosPorCategoria.Select(g => new
                    {
                        categoriaId = g.CategoriaId,
                        nome = g.Nome,
                        total = g.Total,
                        percentual = g.Percentual,
                    }),
                },
                recentes = painel.Recentes.Select(t => new
                {
                    id = t.Transacao.Id,
                    contaId = t.Transacao.ContaId,
                    contaNome = t.ContaNome,
                    categoriaId = t.Transacao.CategoriaId,
                    categoriaNome = t.CategoriaNome,
                    tipo = t.Transacao.Tipo,
                    valor = t.Transacao.Valor,
                    data = t.Transacao.DataMovimentacao.ToString("yyyy-MM-dd"),
                    descricao = t.Transacao.Descricao,
                    origem = t.Transacao.Origem,
                    criadoEm = t.Transacao.CriadoEm,
                    atualizadoEm = t.Transacao.AtualizadoEm,
                }),
            });
        });

        return grupo;
    }
}
