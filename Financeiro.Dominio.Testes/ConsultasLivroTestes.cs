using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Servicos;

namespace Financeiro.Dominio.Testes;

public sealed class ConsultasLivroTestes
{
    private readonly ConsultasLivro _livro = new(null!, null!, new RelogioSaoPaulo());

    [Theory]
    [InlineData("quanto gastei hoje", TipoConsulta.Despesas, PeriodoConsulta.Hoje)]
    [InlineData("Quanto gastei ontem?", TipoConsulta.Despesas, PeriodoConsulta.Ontem)]
    [InlineData("quanto gastei este mês", TipoConsulta.Despesas, PeriodoConsulta.MesAtual)]
    [InlineData("quanto recebi este mês", TipoConsulta.Receitas, PeriodoConsulta.MesAtual)]
    [InlineData("qual meu saldo", TipoConsulta.SaldoTotal, PeriodoConsulta.MesAtual)]
    [InlineData("quanto tenho na nubank", TipoConsulta.SaldoConta, PeriodoConsulta.MesAtual)]
    [InlineData("maior despesa este mês", TipoConsulta.MaiorDespesa, PeriodoConsulta.MesAtual)]
    [InlineData("quanto gastei com alimentação", TipoConsulta.DespesasCategoria, PeriodoConsulta.MesAtual)]
    public void Classificar_ListaFechada_RetornaPedido(string texto, string tipo, string periodo)
    {
        var pedido = _livro.Classificar(texto);
        Assert.NotNull(pedido);
        Assert.Equal(tipo, pedido!.Tipo);
        Assert.Equal(periodo, pedido.Periodo);
    }

    [Fact]
    public void Classificar_MesPassadoDireto_NaoSuportado()
    {
        var pedido = _livro.Classificar("quanto gastei mês passado");
        Assert.NotNull(pedido);
        Assert.Equal(PeriodoConsulta.NaoSuportado, pedido!.Periodo);
    }

    [Fact]
    public void Classificar_Lancamento_NaoEConsulta()
    {
        Assert.Null(_livro.Classificar("gastei 45 no almoço"));
        Assert.Null(_livro.Classificar("recebi 1000 do freela"));
    }

    [Theory]
    [InlineData("e mês passado?")]
    [InlineData("E mes passado")]
    [InlineData("e o mês passado?")]
    public void EhContinuacaoMesPassado_Reconhece(string texto)
    {
        Assert.True(_livro.EhContinuacaoMesPassado(texto));
        Assert.Null(_livro.Classificar(texto));
    }
}
