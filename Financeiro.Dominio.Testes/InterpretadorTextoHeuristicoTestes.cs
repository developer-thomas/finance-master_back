using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Servicos;

namespace Financeiro.Dominio.Testes;

public sealed class InterpretadorTextoHeuristicoTestes
{
    [Fact]
    public async Task Interpretar_GasteiAlmoco_ExtraiDespesaValorECategoria()
    {
        var interpretador = Criar();
        var resultado = await interpretador.InterpretarAsync(
            Guid.NewGuid(),
            "gastei 49,90 no almoço hoje",
            CancellationToken.None);

        Assert.True(resultado.Ok);
        Assert.NotNull(resultado.Sugestao);
        Assert.Equal(TipoTransacao.Despesa, resultado.Sugestao!.Tipo);
        Assert.Equal(49.90m, resultado.Sugestao.Valor);
        Assert.Equal("Alimentação", await NomeCategoria(resultado.Sugestao.CategoriaId));
    }

    [Fact]
    public async Task Interpretar_SemValor_FalhaComMotivo()
    {
        var interpretador = Criar();
        var resultado = await interpretador.InterpretarAsync(
            Guid.NewGuid(),
            "gastei no almoço",
            CancellationToken.None);

        Assert.False(resultado.Ok);
        Assert.Contains("valor", resultado.Motivo!, StringComparison.OrdinalIgnoreCase);
    }

    private static InterpretadorTextoHeuristico Criar()
    {
        return new InterpretadorTextoHeuristico(new CategoriasFake(), new RelogioSaoPaulo());
    }

    private static Task<string> NomeCategoria(Guid? id)
    {
        var nome = CategoriasFake.Todas.First(c => c.Id == id).Nome;
        return Task.FromResult(nome);
    }

    private sealed class CategoriasFake : ICategoriasLivro
    {
        public static readonly IReadOnlyList<Categoria> Todas =
        [
            new Categoria
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Tipo = TipoTransacao.Despesa,
                Nome = "Alimentação",
                Escopo = EscopoCategoria.Padrao,
            },
            new Categoria
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000000A"),
                Tipo = TipoTransacao.Despesa,
                Nome = "Outros",
                Escopo = EscopoCategoria.Padrao,
            },
            new Categoria
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000000B"),
                Tipo = TipoTransacao.Receita,
                Nome = "Outros",
                Escopo = EscopoCategoria.Padrao,
            },
        ];

        public Task<IReadOnlyList<Categoria>> ListarVisiveisAsync(Guid usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(Todas);

        public Task<Categoria> CriarAsync(Guid usuarioId, string tipo, string nome, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
