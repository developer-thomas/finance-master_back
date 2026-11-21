namespace Financeiro.Dominio.Entidades;

public sealed class ContaFinanceira
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    public decimal SaldoInicial { get; set; }

    public bool Ativa { get; set; } = true;

    public bool Padrao { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();
}
