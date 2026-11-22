using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Lista fechada de consultas do WhatsApp. O cálculo é sempre do livro.
/// </summary>
public sealed class ConsultasLivro : IConsultasLivro
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Regex ContinuacaoMesPassado = new(
        @"^\s*e\s+(o\s+|no\s+)?mes\s+passado\s*\??\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly FinanceiroDbContext _db;
    private readonly IContasLivro _contas;
    private readonly IRelogioDominio _relogio;

    public ConsultasLivro(FinanceiroDbContext db, IContasLivro contas, IRelogioDominio relogio)
    {
        _db = db;
        _contas = contas;
        _relogio = relogio;
    }

    /// <summary>
    /// Detecta a continuação "e mês passado?" (só período; a consulta vem do contexto).
    /// </summary>
    public bool EhContinuacaoMesPassado(string texto) =>
        ContinuacaoMesPassado.IsMatch(Normalizar(texto ?? string.Empty));

    /// <summary>
    /// Reconhece perguntas simples (gastei/recebi/saldo/maior despesa).
    /// </summary>
    public PedidoConsulta? Classificar(string texto)
    {
        var n = Normalizar(texto);
        if (n.Length == 0 || EhContinuacaoMesPassado(n))
        {
            return null;
        }

        var periodo = ResolverPeriodo(n);
        if (periodo == PeriodoConsulta.NaoSuportado)
        {
            if (PareceConsulta(n))
            {
                return new PedidoConsulta(TipoConsulta.Despesas, PeriodoConsulta.NaoSuportado, null, null);
            }

            return null;
        }

        if (Regex.IsMatch(n, @"\b(saldo|quanto tenho|quanto eu tenho)\b"))
        {
            var conta = ExtrairNomeConta(n);
            return string.IsNullOrWhiteSpace(conta)
                ? new PedidoConsulta(TipoConsulta.SaldoTotal, PeriodoConsulta.MesAtual, null, null)
                : new PedidoConsulta(TipoConsulta.SaldoConta, PeriodoConsulta.MesAtual, null, conta);
        }

        if (Regex.IsMatch(n, @"\bmaior despesa\b"))
        {
            var periodoMaior = periodo is PeriodoConsulta.Hoje or PeriodoConsulta.Ontem
                ? PeriodoConsulta.MesAtual
                : periodo;
            return new PedidoConsulta(TipoConsulta.MaiorDespesa, periodoMaior, null, null);
        }

        // "recebi 100" é lançamento; "quanto recebi" / "receitas este mês" é consulta.
        if ((Contem(n, "quanto") && (Contem(n, "recebi") || Contem(n, "receita")))
            || Regex.IsMatch(n, @"\breceitas\b"))
        {
            return new PedidoConsulta(TipoConsulta.Receitas, periodo, null, null);
        }

        if ((Contem(n, "quanto") && (Contem(n, "gastei") || Contem(n, "gasto") || Contem(n, "gastos")))
            || Regex.IsMatch(n, @"\b(despesas?|gastos?)\b"))
        {
            var categoria = ExtrairNomeCategoria(n);
            return string.IsNullOrWhiteSpace(categoria)
                ? new PedidoConsulta(TipoConsulta.Despesas, periodo, null, null)
                : new PedidoConsulta(TipoConsulta.DespesasCategoria, periodo, categoria, null);
        }

        return null;
    }

    /// <summary>
    /// Responde a consulta com totais do livro até o dia civil atual.
    /// </summary>
    public async Task<string> ResponderAsync(
        Guid usuarioId,
        PedidoConsulta pedido,
        CancellationToken cancellationToken)
    {
        if (pedido.Periodo == PeriodoConsulta.NaoSuportado)
        {
            return "Essa consulta ainda não é atendida. Posso responder saldo, gastos/receitas de hoje, ontem ou deste mês, gastos por categoria e a maior despesa do mês.";
        }

        var (inicio, fim, rotuloPeriodo) = Intervalo(pedido.Periodo);

        return pedido.Tipo switch
        {
            TipoConsulta.SaldoTotal => await ResponderSaldoTotalAsync(usuarioId, cancellationToken),
            TipoConsulta.SaldoConta => await ResponderSaldoContaAsync(usuarioId, pedido.NomeConta!, cancellationToken),
            TipoConsulta.Receitas => await ResponderSomaAsync(
                usuarioId, TipoTransacao.Receita, inicio, fim, $"Receitas {rotuloPeriodo}", cancellationToken),
            TipoConsulta.Despesas => await ResponderSomaAsync(
                usuarioId, TipoTransacao.Despesa, inicio, fim, $"Despesas {rotuloPeriodo}", cancellationToken),
            TipoConsulta.DespesasCategoria => await ResponderCategoriaAsync(
                usuarioId, pedido.NomeCategoria!, inicio, fim, rotuloPeriodo, cancellationToken),
            TipoConsulta.MaiorDespesa => await ResponderMaiorDespesaAsync(
                usuarioId, inicio, fim, rotuloPeriodo, cancellationToken),
            _ => "Não entendi a consulta.",
        };
    }

    private async Task<string> ResponderSaldoTotalAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var contas = await _contas.ListarAsync(usuarioId, cancellationToken);
        var total = contas.Where(c => c.Conta.Ativa).Sum(c => c.Saldo);
        return $"Seu saldo total (contas ativas) é R$ {Formatar(total)}.";
    }

    private async Task<string> ResponderSaldoContaAsync(
        Guid usuarioId,
        string nomeConta,
        CancellationToken cancellationToken)
    {
        var contas = await _contas.ListarAsync(usuarioId, cancellationToken);
        var chave = Normalizar(nomeConta);
        var conta = contas.FirstOrDefault(c => Normalizar(c.Conta.Nome) == chave);
        if (conta is null)
        {
            return $"Não encontrei uma conta chamada \"{nomeConta}\".";
        }

        var estado = conta.Conta.Ativa ? "" : " (inativa, fora do saldo total)";
        return $"Saldo de {conta.Conta.Nome}{estado}: R$ {Formatar(conta.Saldo)}.";
    }

    private async Task<string> ResponderSomaAsync(
        Guid usuarioId,
        string tipo,
        DateOnly inicio,
        DateOnly fim,
        string rotulo,
        CancellationToken cancellationToken)
    {
        var total = await _db.Transacoes
            .AsNoTracking()
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.Tipo == tipo &&
                t.DataMovimentacao >= inicio &&
                t.DataMovimentacao <= fim)
            .SumAsync(t => (decimal?)t.Valor, cancellationToken) ?? 0m;

        return $"{rotulo}: R$ {Formatar(total)}.";
    }

    private async Task<string> ResponderCategoriaAsync(
        Guid usuarioId,
        string nomeCategoria,
        DateOnly inicio,
        DateOnly fim,
        string rotuloPeriodo,
        CancellationToken cancellationToken)
    {
        var chave = Normalizar(nomeCategoria);
        var categorias = await _db.Categorias
            .AsNoTracking()
            .Where(c =>
                c.Escopo == EscopoCategoria.Padrao ||
                (c.Escopo == EscopoCategoria.Personalizada && c.UsuarioId == usuarioId))
            .ToListAsync(cancellationToken);

        var categoria = categorias.FirstOrDefault(c =>
            c.Tipo == TipoTransacao.Despesa && Normalizar(c.Nome) == chave);
        if (categoria is null)
        {
            // Aceita prefixo ("aliment" → Alimentação) se houver um só match.
            var candidatos = categorias
                .Where(c => c.Tipo == TipoTransacao.Despesa && Normalizar(c.Nome).StartsWith(chave, StringComparison.Ordinal))
                .ToList();
            categoria = candidatos.Count == 1 ? candidatos[0] : null;
        }

        if (categoria is null)
        {
            return $"Não encontrei a categoria \"{nomeCategoria}\".";
        }

        var total = await _db.Transacoes
            .AsNoTracking()
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.Tipo == TipoTransacao.Despesa &&
                t.CategoriaId == categoria.Id &&
                t.DataMovimentacao >= inicio &&
                t.DataMovimentacao <= fim)
            .SumAsync(t => (decimal?)t.Valor, cancellationToken) ?? 0m;

        return $"Despesas com {categoria.Nome} {rotuloPeriodo}: R$ {Formatar(total)}.";
    }

    private async Task<string> ResponderMaiorDespesaAsync(
        Guid usuarioId,
        DateOnly inicio,
        DateOnly fim,
        string rotuloPeriodo,
        CancellationToken cancellationToken)
    {
        var maior = await _db.Transacoes
            .AsNoTracking()
            .Include(t => t.Categoria)
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.Tipo == TipoTransacao.Despesa &&
                t.DataMovimentacao >= inicio &&
                t.DataMovimentacao <= fim)
            .OrderByDescending(t => t.Valor)
            .ThenByDescending(t => t.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        if (maior is null)
        {
            return $"Não há despesas {rotuloPeriodo}.";
        }

        var data = maior.DataMovimentacao.ToString("dd/MM/yyyy", PtBr);
        return $"Maior despesa {rotuloPeriodo}: R$ {Formatar(maior.Valor)} — {maior.Descricao} ({maior.Categoria.Nome}) em {data}.";
    }

    private (DateOnly Inicio, DateOnly Fim, string Rotulo) Intervalo(string periodo)
    {
        var hoje = _relogio.Hoje();
        return periodo switch
        {
            PeriodoConsulta.Hoje => (hoje, hoje, "de hoje"),
            PeriodoConsulta.Ontem => (hoje.AddDays(-1), hoje.AddDays(-1), "de ontem"),
            PeriodoConsulta.MesPassado => MesPassado(hoje),
            _ => (new DateOnly(hoje.Year, hoje.Month, 1), hoje, "deste mês"),
        };
    }

    private static (DateOnly Inicio, DateOnly Fim, string Rotulo) MesPassado(DateOnly hoje)
    {
        var primeiroAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = primeiroAtual.AddDays(-1);
        var inicio = new DateOnly(fim.Year, fim.Month, 1);
        return (inicio, fim, "do mês passado");
    }

    private static string ResolverPeriodo(string n)
    {
        if (Contem(n, "semana passada")
            || Contem(n, "ano passado")
            || Contem(n, "trimestre")
            || Contem(n, "ultimos 30")
            || Contem(n, "mes passado")
            || Regex.IsMatch(n, @"\bem\s+(janeiro|fevereiro|marco|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro)\b"))
        {
            return PeriodoConsulta.NaoSuportado;
        }

        if (Contem(n, "ontem"))
        {
            return PeriodoConsulta.Ontem;
        }

        if (Contem(n, "hoje"))
        {
            return PeriodoConsulta.Hoje;
        }

        return PeriodoConsulta.MesAtual;
    }

    private static bool PareceConsulta(string n) =>
        Regex.IsMatch(n, @"\b(quanto|saldo|gastei|recebi|despesa|receita|gasto)\b");

    private static string? ExtrairNomeCategoria(string n)
    {
        var m = Regex.Match(
            n,
            @"\b(?:com|em)\s+([a-z0-9çãõáéíóúâêôàü ]+?)(?:\s+(?:hoje|ontem|este mes|esse mes|neste mes|do mes|deste mes))?\s*$");
        if (!m.Success)
        {
            return null;
        }

        var nome = m.Groups[1].Value.Trim();
        if (nome is "hoje" or "ontem" or "mes" or "este" or "esse" or "neste" or "deste")
        {
            return null;
        }

        return nome;
    }

    private static string? ExtrairNomeConta(string n)
    {
        var m = Regex.Match(n, @"\b(?:na|no|em)\s+([a-z0-9çãõáéíóúâêôàü ]{2,40})$");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string Formatar(decimal valor) => valor.ToString("N2", PtBr);

    private static bool Contem(string texto, string termo) =>
        texto.Contains(Normalizar(termo), StringComparison.Ordinal);

    private static string Normalizar(string texto)
    {
        var form = texto.Trim().ToLower(PtBr).Normalize(NormalizationForm.FormD);
        return string.Concat(form.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    }
}
