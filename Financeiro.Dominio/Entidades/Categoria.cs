namespace Financeiro.Dominio.Entidades;

public sealed class Categoria
{
    public Guid Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Escopo { get; set; } = string.Empty;

    public Guid? UsuarioId { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public Usuario? Usuario { get; set; }

    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();
}
