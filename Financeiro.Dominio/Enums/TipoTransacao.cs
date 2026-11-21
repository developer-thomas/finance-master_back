namespace Financeiro.Dominio.Enums;

public static class TipoTransacao
{
    public const string Receita = "RECEITA";
    public const string Despesa = "DESPESA";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>
    {
        Receita,
        Despesa,
    };
}
