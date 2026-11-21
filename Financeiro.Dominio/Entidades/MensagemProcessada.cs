namespace Financeiro.Dominio.Entidades;

public sealed class MensagemProcessada
{
    public string IdentificadorExterno { get; set; } = string.Empty;

    public Guid? UsuarioId { get; set; }

    public string Status { get; set; } = "recebida";

    public string? Efeito { get; set; }

    public Guid? TransacaoId { get; set; }

    public string? Resposta { get; set; }

    public DateTimeOffset RecebidaEm { get; set; }

    public DateTimeOffset? ProcessamentoIniciadoEm { get; set; }

    public DateTimeOffset? ConcluidaEm { get; set; }

    public Usuario? Usuario { get; set; }

    public Transacao? Transacao { get; set; }
}
