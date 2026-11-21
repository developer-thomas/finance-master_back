using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class VinculoWhatsappConfiguracao : IEntityTypeConfiguration<VinculoWhatsapp>
{
    public void Configure(EntityTypeBuilder<VinculoWhatsapp> builder)
    {
        builder.ToTable("vinculo_whatsapp");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.NumeroE164).HasColumnName("numero_e164").HasMaxLength(20).IsRequired();
        builder.Property(x => x.VinculadoEm).HasColumnName("vinculado_em").IsRequired();

        builder.HasIndex(x => x.UsuarioId).IsUnique().HasDatabaseName("ix_vinculo_usuario");
        builder.HasIndex(x => x.NumeroE164).IsUnique().HasDatabaseName("ix_vinculo_numero");

        builder.HasOne(x => x.Usuario)
            .WithOne(x => x.VinculoWhatsapp)
            .HasForeignKey<VinculoWhatsapp>(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
