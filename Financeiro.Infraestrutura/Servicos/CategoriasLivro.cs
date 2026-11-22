using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Categorias padrão (seed) e personalizadas do usuário.
/// </summary>
public sealed class CategoriasLivro : ICategoriasLivro
{
    private const int LimiteNome = 40;

    private readonly FinanceiroDbContext _db;
    private readonly IRelogioDominio _relogio;

    public CategoriasLivro(FinanceiroDbContext db, IRelogioDominio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    /// <summary>
    /// Lista categorias padrão e as personalizadas do dono.
    /// </summary>
    public async Task<IReadOnlyList<Categoria>> ListarVisiveisAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        return await _db.Categorias
            .AsNoTracking()
            .Where(c =>
                c.Escopo == EscopoCategoria.Padrao ||
                (c.Escopo == EscopoCategoria.Personalizada && c.UsuarioId == usuarioId))
            .OrderBy(c => c.Tipo)
            .ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Cria categoria personalizada. Nome único por tipo no conjunto visível ao usuário.
    /// </summary>
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
            CriadoEm = _relogio.Agora(),
        };

        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync(cancellationToken);
        return categoria;
    }
}
