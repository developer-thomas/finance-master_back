using System.Security.Claims;
using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Api.Autenticacao;

public static class UsuarioHttp
{
    public const string ItemUsuario = "UsuarioLivro";

    public static IdentidadeExterna? LerIdentidade(ClaimsPrincipal principal)
    {
        var uid = principal.FindFirstValue("user_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var nome = principal.FindFirstValue("name")
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? string.Empty;

        return new IdentidadeExterna(uid, nome, email);
    }

    public static Usuario Exigir(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemUsuario, out var valor) && valor is Usuario usuario)
        {
            return usuario;
        }

        throw new InvalidOperationException("Usuário do livro não resolvido.");
    }
}
