using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class CodigoVinculoConfiguracao : IEntityTypeConfiguration<CodigoVinculo>
{
    public void Configure(EntityTypeBuilder<CodigoVinculo> builder)
    {
        builder.ToTable("codigo_vinculo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.NumeroE164).HasColumnName("numero_e164").HasMaxLength(20).IsRequired();
        builder.Property(x => x.CodigoHash).HasColumnName("codigo_hash").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(x => x.UsadoEm).HasColumnName("usado_em");

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
