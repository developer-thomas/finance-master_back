using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

public sealed class CategoriasLivro(
    FinanceiroDbContext db,
    IRelogioDominio relogio) : ICategoriasLivro
{
    private const int LimiteNome = 40;

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

    public async Task<Categoria> CriarAsync(
        Guid usuarioId,
        string tipo,
        string nome,
        CancellationToken cancellationToken)
    {
        if (!TipoTransacao.Todos.Contains(tipo))
        {
            throw new InvalidOperationException("Tipo de categoria inválido.");
        }

        var limpo = nome.Trim();
        if (string.IsNullOrWhiteSpace(limpo))
        {
            throw new InvalidOperationException("Dê um nome para a categoria.");
        }

        if (limpo.Length > LimiteNome)
        {
            throw new InvalidOperationException("Use até 40 caracteres.");
        }

        var visiveis = await ListarVisiveisAsync(usuarioId, cancellationToken);
        if (visiveis.Any(c =>
                c.Tipo == tipo &&
                string.Equals(c.Nome, limpo, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Já existe a categoria \"{limpo}\" em {(tipo == TipoTransacao.Despesa ? "despesas" : "receitas")}.");
        }

        var categoria = new Categoria
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Nome = limpo,
            Escopo = EscopoCategoria.Personalizada,
            UsuarioId = usuarioId,
            CriadoEm = relogio.Agora(),
        };

        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(cancellationToken);
        return categoria;
    }
}
