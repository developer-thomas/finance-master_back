using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Autenticacao;

public sealed class ResolvedorUsuarioMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IResolvedorUsuario resolvedor)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var identidade = UsuarioHttp.LerIdentidade(context.User);
            if (identidade is not null)
            {
                var usuario = await resolvedor.ObterOuCriarAsync(identidade, context.RequestAborted);
                context.Items[UsuarioHttp.ItemUsuario] = usuario;
            }
        }

        await next(context);
    }
}
