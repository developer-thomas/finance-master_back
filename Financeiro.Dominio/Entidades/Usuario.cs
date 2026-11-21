namespace Financeiro.Dominio.Entidades;

public sealed class Usuario
{
    public Guid Id { get; set; }

    public string FirebaseUid { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }

    public ICollection<ContaFinanceira> Contas { get; set; } = new List<ContaFinanceira>();

    public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();

    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();

    public VinculoWhatsapp? VinculoWhatsapp { get; set; }

    public OperacaoPendente? OperacaoPendente { get; set; }

    public ContextoConsulta? ContextoConsulta { get; set; }
}
