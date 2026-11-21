namespace Financeiro.Dominio.Entidades;

public sealed class CodigoVinculo
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public string NumeroE164 { get; set; } = string.Empty;

    public string CodigoHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiraEm { get; set; }

    public DateTimeOffset? UsadoEm { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
