namespace Financeiro.Dominio.Entidades;

public sealed class OperacaoPendente
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public string Especie { get; set; } = string.Empty;

    public string? Tipo { get; set; }

    public decimal? Valor { get; set; }

    public DateOnly DataMovimentacao { get; set; }

    public string? Descricao { get; set; }

    public Guid? CategoriaId { get; set; }

    public Guid ContaId { get; set; }

    public string? CampoFaltante { get; set; }

    public string IdentificadorMensagem { get; set; } = string.Empty;

    public DateTimeOffset PerguntadaEm { get; set; }

    public DateTimeOffset ExpiraEm { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public ContaFinanceira Conta { get; set; } = null!;

    public Categoria? Categoria { get; set; }
}
