namespace Financeiro.Dominio.Entidades;

public sealed class Transacao
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public Guid ContaId { get; set; }

    public Guid CategoriaId { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public string Moeda { get; set; } = "BRL";

    public DateOnly DataMovimentacao { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public string Origem { get; set; } = string.Empty;

    public string? IdentificadorMensagem { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }

    public DateTimeOffset? ExcluidaEm { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public ContaFinanceira Conta { get; set; } = null!;

    public Categoria Categoria { get; set; } = null!;
}
