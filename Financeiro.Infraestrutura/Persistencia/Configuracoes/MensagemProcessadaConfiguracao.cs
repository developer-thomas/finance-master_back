using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class MensagemProcessadaConfiguracao : IEntityTypeConfiguration<MensagemProcessada>
{
    public void Configure(EntityTypeBuilder<MensagemProcessada> builder)
    {
        builder.ToTable("mensagem_processada");
        builder.HasKey(x => x.IdentificadorExterno);

        builder.Property(x => x.IdentificadorExterno).HasColumnName("identificador_externo").HasMaxLength(255);
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Efeito).HasColumnName("efeito").HasMaxLength(40);
        builder.Property(x => x.TransacaoId).HasColumnName("transacao_id");
        builder.Property(x => x.Resposta).HasColumnName("resposta");
        builder.Property(x => x.RecebidaEm).HasColumnName("recebida_em").IsRequired();
        builder.Property(x => x.ProcessamentoIniciadoEm).HasColumnName("processamento_iniciado_em");
        builder.Property(x => x.ConcluidaEm).HasColumnName("concluida_em");

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transacao)
            .WithMany()
            .HasForeignKey(x => x.TransacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
