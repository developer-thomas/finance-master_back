using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Garante um <see cref="Usuario"/> interno a partir da identidade do token (Firebase ou local).
/// </summary>
public sealed class ResolvedorUsuario : IResolvedorUsuario
{
    private readonly FinanceiroDbContext _db;
    private readonly IRelogioDominio _relogio;

    public ResolvedorUsuario(FinanceiroDbContext db, IRelogioDominio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    /// <summary>
    /// Busca o usuário pelo FirebaseUid; se não existir, cria. Atualiza nome/e-mail quando mudam.
    /// </summary>
    public async Task<Usuario> ObterOuCriarAsync(IdentidadeExterna identidade, CancellationToken cancellationToken)
    {
        var uid = identidade.FirebaseUid.Trim();
        var email = identidade.Email.Trim().ToLowerInvariant();
        var nome = string.IsNullOrWhiteSpace(identidade.Nome)
            ? email.Split('@')[0]
            : identidade.Nome.Trim();

        var existente = await _db.Usuarios
            .SingleOrDefaultAsync(u => u.FirebaseUid == uid, cancellationToken);

        if (existente is not null)
        {
            var mudou = false;
            if (!string.Equals(existente.Email, email, StringComparison.Ordinal))
            {
                existente.Email = email;
                mudou = true;
            }

            if (!string.IsNullOrWhiteSpace(nome) && !string.Equals(existente.Nome, nome, StringComparison.Ordinal))
            {
                existente.Nome = nome;
                mudou = true;
            }

            if (mudou)
            {
                existente.AtualizadoEm = _relogio.Agora();
                await _db.SaveChangesAsync(cancellationToken);
            }

            return existente;
        }

        var agora = _relogio.Agora();
        var novo = new Usuario
        {
            Id = Guid.NewGuid(),
            FirebaseUid = uid,
            Nome = nome,
            Email = email,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        _db.Usuarios.Add(novo);
        await _db.SaveChangesAsync(cancellationToken);
        return novo;
    }
}
