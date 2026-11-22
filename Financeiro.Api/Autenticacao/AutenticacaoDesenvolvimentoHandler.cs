using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Financeiro.Api.Autenticacao;

/// <summary>
/// Aceita Bearer local.&lt;base64(json)&gt; enquanto o Firebase não está configurado.
/// Só é registrado em Development.
/// </summary>
public sealed class AutenticacaoDesenvolvimentoHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AutenticacaoDesenvolvimentoHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// Valida o token local e monta as claims de uid, e-mail e nome.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var valor = header.ToString();
        if (!valor.StartsWith("Bearer local.", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            var payload = valor["Bearer local.".Length..];
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var dados = JsonSerializer.Deserialize<TokenLocal>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dados is null || string.IsNullOrWhiteSpace(dados.Uid) || string.IsNullOrWhiteSpace(dados.Email))
            {
                return Task.FromResult(AuthenticateResult.Fail("Token local inválido."));
            }

            var identidade = new ClaimsIdentity(
                [
                    new Claim("user_id", dados.Uid),
                    new Claim(ClaimTypes.NameIdentifier, dados.Uid),
                    new Claim(ClaimTypes.Email, dados.Email),
                    new Claim("email", dados.Email),
                    new Claim("name", dados.Nome ?? string.Empty),
                ],
                EsquemasAutenticacao.Desenvolvimento);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidade), EsquemasAutenticacao.Desenvolvimento);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Token local inválido."));
        }
    }

    private sealed record TokenLocal(string Uid, string Email, string? Nome);
}
