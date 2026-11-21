namespace Financeiro.Dominio.Entidades;

public sealed class VinculoWhatsapp
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public string NumeroE164 { get; set; } = string.Empty;

    public DateTimeOffset VinculadoEm { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
