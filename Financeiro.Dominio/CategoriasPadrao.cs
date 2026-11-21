using Financeiro.Dominio.Enums;

namespace Financeiro.Dominio;

public static class CategoriasPadrao
{
    public static readonly IReadOnlyList<(string Tipo, string Nome)> Todas =
    [
        (TipoTransacao.Despesa, "Alimentação"),
        (TipoTransacao.Despesa, "Mercado"),
        (TipoTransacao.Despesa, "Transporte"),
        (TipoTransacao.Despesa, "Moradia"),
        (TipoTransacao.Despesa, "Saúde"),
        (TipoTransacao.Despesa, "Educação"),
        (TipoTransacao.Despesa, "Lazer"),
        (TipoTransacao.Despesa, "Compras"),
        (TipoTransacao.Despesa, "Assinaturas"),
        (TipoTransacao.Despesa, "Outros"),
        (TipoTransacao.Receita, "Salário"),
        (TipoTransacao.Receita, "Freelance"),
        (TipoTransacao.Receita, "Reembolso"),
        (TipoTransacao.Receita, "Transferência recebida"),
        (TipoTransacao.Receita, "Investimentos"),
        (TipoTransacao.Receita, "Outros"),
    ];
}
