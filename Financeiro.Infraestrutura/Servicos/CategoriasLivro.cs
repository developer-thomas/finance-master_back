using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

public sealed class CategoriasLivro(FinanceiroDbContext db) : ICategoriasLivro
{
    public async Task<IReadOnlyList<Categoria>> ListarVisiveisAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        return await db.Categorias
            .AsNoTracking()
            .Where(c =>
                c.Escopo == EscopoCategoria.Padrao ||
                (c.Escopo == EscopoCategoria.Personalizada && c.UsuarioId == usuarioId))
            .OrderBy(c => c.Tipo)
            .ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);
    }
}
