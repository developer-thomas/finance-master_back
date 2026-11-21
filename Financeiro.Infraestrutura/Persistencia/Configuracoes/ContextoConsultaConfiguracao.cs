using Financeiro.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financeiro.Infraestrutura.Persistencia.Configuracoes;

public sealed class ContextoConsultaConfiguracao : IEntityTypeConfiguration<ContextoConsulta>
{
    public void Configure(EntityTypeBuilder<ContextoConsulta> builder)
    {
        builder.ToTable("contexto_consulta");
        builder.HasKey(x => x.UsuarioId);

        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.Consulta).HasColumnName("consulta").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Periodo).HasColumnName("periodo").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CategoriaId).HasColumnName("categoria_id");
        builder.Property(x => x.ContaId).HasColumnName("conta_id");
        builder.Property(x => x.ExpiraEm).HasColumnName("expira_em").IsRequired();

        builder.HasOne(x => x.Usuario)
            .WithOne(x => x.ContextoConsulta)
            .HasForeignKey<ContextoConsulta>(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
