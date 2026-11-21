using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class TransacaoConfiguracao : IEntityTypeConfiguration<Transacao>
{
    public void Configure(EntityTypeBuilder<Transacao> builder)
    {
        builder.ToTable("transacao", table =>
        {
            table.HasCheckConstraint("ck_transacao_valor_positivo", "valor > 0");
            table.HasCheckConstraint("ck_transacao_moeda_brl", "moeda = 'BRL'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.ContaId).HasColumnName("conta_id").IsRequired();
        builder.Property(x => x.CategoriaId).HasColumnName("categoria_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(16).IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.Moeda).HasColumnName("moeda").HasMaxLength(3).IsRequired();
        builder.Property(x => x.DataMovimentacao).HasColumnName("data_movimentacao").IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(32).IsRequired();
        builder.Property(x => x.IdentificadorMensagem).HasColumnName("identificador_mensagem").HasMaxLength(255);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.Property(x => x.ExcluidaEm).HasColumnName("excluida_em");

        builder.HasIndex(x => x.IdentificadorMensagem)
            .IsUnique()
            .HasFilter("identificador_mensagem IS NOT NULL")
            .HasDatabaseName("ix_transacao_identificador_mensagem");

        builder.HasIndex(x => new { x.UsuarioId, x.DataMovimentacao, x.CriadoEm })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_transacao_usuario_data");

        builder.HasIndex(x => new { x.UsuarioId, x.ContaId })
            .HasDatabaseName("ix_transacao_usuario_conta");

        builder.HasIndex(x => new { x.UsuarioId, x.CategoriaId })
            .HasDatabaseName("ix_transacao_usuario_categoria");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Transacoes)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Conta)
            .WithMany(x => x.Transacoes)
            .HasForeignKey(x => new { x.ContaId, x.UsuarioId })
            .HasPrincipalKey(x => new { x.Id, x.UsuarioId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Categoria)
            .WithMany(x => x.Transacoes)
            .HasForeignKey(x => x.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => x.ExcluidaEm == null);
    }
}
