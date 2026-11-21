using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class ContaFinanceiraConfiguracao : IEntityTypeConfiguration<ContaFinanceira>
{
    public void Configure(EntityTypeBuilder<ContaFinanceira> builder)
    {
        builder.ToTable("conta_financeira", table =>
        {
            table.HasCheckConstraint(
                "ck_conta_padrao_ativa",
                "(padrao = false) OR (padrao = true AND ativa = true)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(32).IsRequired();
        builder.Property(x => x.SaldoInicial).HasColumnName("saldo_inicial").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.Ativa).HasColumnName("ativa").IsRequired();
        builder.Property(x => x.Padrao).HasColumnName("padrao").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        builder.HasIndex(x => new { x.UsuarioId, x.Nome })
            .IsUnique()
            .HasDatabaseName("ix_conta_usuario_nome");

        builder.HasIndex(x => x.UsuarioId)
            .IsUnique()
            .HasFilter("padrao = true")
            .HasDatabaseName("ix_conta_usuario_padrao");

        builder.HasIndex(x => new { x.Id, x.UsuarioId })
            .IsUnique()
            .HasDatabaseName("ix_conta_id_usuario");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Contas)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
