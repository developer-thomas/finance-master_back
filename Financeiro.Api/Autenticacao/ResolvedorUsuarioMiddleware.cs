using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Autenticacao;

/// <summary>
/// Após autenticar o token, resolve/cria o usuário interno e coloca em <see cref="HttpContext.Items"/>.
/// </summary>
public sealed class ResolvedorUsuarioMiddleware
{
    private readonly RequestDelegate _next;

    public ResolvedorUsuarioMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Preenche o usuário autenticado para os endpoints que usam <see cref="UsuarioHttp"/>.
    /// </summary>
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

        await _next(context);
    }
}
