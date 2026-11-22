namespace Financeiro.Dominio.Servicos;

/// <summary>
/// Período aceito nas consultas fechadas do WhatsApp.
/// </summary>
public static class PeriodoConsulta
{
    public const string Hoje = "HOJE";
    public const string Ontem = "ONTEM";
    public const string MesAtual = "MES_ATUAL";
    public const string MesPassado = "MES_PASSADO";
    public const string NaoSuportado = "NAO_SUPORTADO";
}

/// <summary>
/// Tipo de pergunta da lista fechada.
/// </summary>
public static class TipoConsulta
{
    public const string Despesas = "DESPESAS";
    public const string Receitas = "RECEITAS";
    public const string SaldoTotal = "SALDO_TOTAL";
    public const string SaldoConta = "SALDO_CONTA";
    public const string DespesasCategoria = "DESPESAS_CATEGORIA";
    public const string MaiorDespesa = "MAIOR_DESPESA";
}

/// <summary>
/// Pedido de consulta já classificado (ainda sem valores calculados).
/// </summary>
public sealed record PedidoConsulta(
    string Tipo,
    string Periodo,
    string? NomeCategoria,
    string? NomeConta);

/// <summary>
/// Contrato das consultas financeiras do canal WhatsApp.
/// </summary>
public interface IConsultasLivro
{
    /// <summary>
    /// True quando o texto é só a continuação "e mês passado?".
    /// </summary>
    bool EhContinuacaoMesPassado(string texto);

    /// <summary>
    /// Tenta classificar o texto como consulta da lista fechada. Null se não for consulta.
    /// </summary>
    PedidoConsulta? Classificar(string texto);

    /// <summary>
    /// Calcula a resposta oficial no backend (a IA não calcula).
    /// </summary>
    Task<string> ResponderAsync(Guid usuarioId, PedidoConsulta pedido, CancellationToken cancellationToken);
}
