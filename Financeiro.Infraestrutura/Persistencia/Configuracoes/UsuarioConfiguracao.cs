using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FirebaseUid).HasColumnName("firebase_uid").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        builder.HasIndex(x => x.FirebaseUid).IsUnique().HasDatabaseName("ix_usuario_firebase_uid");
        // E-mail é gravado em minúsculas pela aplicação; o índice único reforça isso.
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ix_usuario_email");
    }
}
