using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Persistencia;

public sealed class FinanceiroDbContext(DbContextOptions<FinanceiroDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ContaFinanceira> ContasFinanceiras => Set<ContaFinanceira>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<VinculoWhatsapp> VinculosWhatsapp => Set<VinculoWhatsapp>();
    public DbSet<CodigoVinculo> CodigosVinculo => Set<CodigoVinculo>();
    public DbSet<OperacaoPendente> OperacoesPendentes => Set<OperacaoPendente>();
    public DbSet<MensagemProcessada> MensagensProcessadas => Set<MensagemProcessada>();
    public DbSet<ContextoConsulta> ContextosConsulta => Set<ContextoConsulta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceiroDbContext).Assembly);
        SeedCategoriasPadrao(modelBuilder);
    }

    private static void SeedCategoriasPadrao(ModelBuilder modelBuilder)
    {
        var criadoEm = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var seed = CategoriasPadrao.Todas
            .Select((item, indice) => new Categoria
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{indice + 1:D12}"),
                Tipo = item.Tipo,
                Nome = item.Nome,
                Escopo = EscopoCategoria.Padrao,
                UsuarioId = null,
                CriadoEm = criadoEm,
            })
            .ToArray();

        modelBuilder.Entity<Categoria>().HasData(seed);
    }
}
