namespace Financeiro.Dominio.Enums;

public static class TipoConta
{
    public const string ContaCorrente = "CONTA_CORRENTE";
    public const string ContaDigital = "CONTA_DIGITAL";
    public const string Dinheiro = "DINHEIRO";
    public const string Outros = "OUTROS";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>
    {
        ContaCorrente,
        ContaDigital,
        Dinheiro,
        Outros,
    };
}
