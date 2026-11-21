using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

public sealed class ResolvedorUsuario(
    FinanceiroDbContext db,
    IRelogioDominio relogio) : IResolvedorUsuario
{
    public async Task<Usuario> ObterOuCriarAsync(IdentidadeExterna identidade, CancellationToken cancellationToken)
    {
        var uid = identidade.FirebaseUid.Trim();
        var email = identidade.Email.Trim().ToLowerInvariant();
        var nome = string.IsNullOrWhiteSpace(identidade.Nome)
            ? email.Split('@')[0]
            : identidade.Nome.Trim();

        var existente = await db.Usuarios
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
                existente.AtualizadoEm = relogio.Agora();
                await db.SaveChangesAsync(cancellationToken);
            }

            return existente;
        }

        var agora = relogio.Agora();
        var novo = new Usuario
        {
            Id = Guid.NewGuid(),
            FirebaseUid = uid,
            Nome = nome,
            Email = email,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        db.Usuarios.Add(novo);
        await db.SaveChangesAsync(cancellationToken);
        return novo;
    }
}
