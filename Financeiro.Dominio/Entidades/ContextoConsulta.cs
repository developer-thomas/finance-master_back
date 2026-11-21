namespace Financeiro.Dominio.Entidades;

public sealed class ContextoConsulta
{
    public Guid UsuarioId { get; set; }

    public string Consulta { get; set; } = string.Empty;

    public string Periodo { get; set; } = string.Empty;

    public Guid? CategoriaId { get; set; }

    public Guid? ContaId { get; set; }

    public DateTimeOffset ExpiraEm { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
