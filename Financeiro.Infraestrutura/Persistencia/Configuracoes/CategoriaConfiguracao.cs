using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class CategoriaConfiguracao : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categoria", table =>
        {
            table.HasCheckConstraint(
                "ck_categoria_escopo",
                "(escopo = 'PADRAO' AND usuario_id IS NULL) OR (escopo = 'PERSONALIZADA' AND usuario_id IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(16).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Escopo).HasColumnName("escopo").HasMaxLength(16).IsRequired();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(x => new { x.Tipo, x.Nome })
            .IsUnique()
            .HasFilter("usuario_id IS NULL")
            .HasDatabaseName("ix_categoria_padrao_tipo_nome");

        builder.HasIndex(x => new { x.UsuarioId, x.Tipo, x.Nome })
            .IsUnique()
            .HasFilter("usuario_id IS NOT NULL")
            .HasDatabaseName("ix_categoria_usuario_tipo_nome");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Categorias)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
