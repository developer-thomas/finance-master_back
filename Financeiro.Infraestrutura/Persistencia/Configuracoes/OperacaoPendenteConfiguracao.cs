using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class OperacaoPendenteConfiguracao : IEntityTypeConfiguration<OperacaoPendente>
{
    public void Configure(EntityTypeBuilder<OperacaoPendente> builder)
    {
        builder.ToTable("operacao_pendente");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.Especie).HasColumnName("especie").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(16);
        builder.Property(x => x.Valor).HasColumnName("valor").HasPrecision(14, 2);
        builder.Property(x => x.DataMovimentacao).HasColumnName("data_movimentacao").IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(500);
        builder.Property(x => x.CategoriaId).HasColumnName("categoria_id");
        builder.Property(x => x.ContaId).HasColumnName("conta_id").IsRequired();
        builder.Property(x => x.CampoFaltante).HasColumnName("campo_faltante").HasMaxLength(16);
        builder.Property(x => x.IdentificadorMensagem).HasColumnName("identificador_mensagem").HasMaxLength(255).IsRequired();
        builder.Property(x => x.PerguntadaEm).HasColumnName("perguntada_em").IsRequired();
        builder.Property(x => x.ExpiraEm).HasColumnName("expira_em").IsRequired();

        builder.HasIndex(x => x.UsuarioId).IsUnique().HasDatabaseName("ix_operacao_pendente_usuario");

        builder.HasOne(x => x.Usuario)
            .WithOne(x => x.OperacaoPendente)
            .HasForeignKey<OperacaoPendente>(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Conta)
            .WithMany()
            .HasForeignKey(x => x.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Categoria)
            .WithMany()
            .HasForeignKey(x => x.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
