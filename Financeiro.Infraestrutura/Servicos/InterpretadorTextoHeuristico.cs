using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Interpretação local por heurísticas (MVP). Mesmo contrato que um provedor de IA usaria depois.
/// </summary>
public sealed class InterpretadorTextoHeuristico : IInterpretadorTexto
{
    private static readonly string[] VerbosDespesa =
    [
        "gastei", "paguei", "comprei", "gasto", "despesa", "pagamento", "pago", "transferi", "mandei", "enviei",
        "fiz um pix", "fiz pix",
    ];

    private static readonly string[] VerbosReceita =
    [
        "recebi", "ganhei", "entrou", "caiu", "receita", "recebimento", "me pagou", "me pagaram", "me mandou",
        "me transferiu",
    ];

    private static readonly Dictionary<string, string[]> PistasDespesa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alimentação"] = ["almoco", "almocei", "jantar", "jantei", "lanche", "restaurante", "ifood", "cafe", "padaria", "pizza", "hamburguer", "comida"],
        ["Mercado"] = ["mercado", "supermercado", "feira", "acougue", "hortifruti", "atacadao"],
        ["Transporte"] = ["uber", "99", "gasolina", "combustivel", "onibus", "metro", "estacionamento", "posto", "taxi", "pedagio"],
        ["Moradia"] = ["aluguel", "condominio", "luz", "energia", "agua", "internet", "gas", "iptu"],
        ["Saúde"] = ["farmacia", "remedio", "medico", "consulta", "dentista", "exame", "plano de saude", "academia"],
        ["Educação"] = ["curso", "faculdade", "escola", "livro", "mensalidade", "apostila"],
        ["Lazer"] = ["cinema", "show", "bar", "viagem", "passeio", "ingresso", "cerveja"],
        ["Compras"] = ["roupa", "tenis", "loja", "presente", "shopping", "amazon", "shopee", "mercado livre"],
        ["Assinaturas"] = ["netflix", "spotify", "assinatura", "streaming", "prime", "disney", "youtube premium", "icloud"],
    };

    private static readonly Dictionary<string, string[]> PistasReceita = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Salário"] = ["salario", "pagamento do mes", "holerite"],
        ["Freelance"] = ["freela", "freelance", "projeto", "cliente", "job"],
        ["Reembolso"] = ["reembolso", "devolucao", "estorno"],
        ["Transferência recebida"] = ["transferencia", "pix"],
        ["Investimentos"] = ["dividendo", "dividendos", "rendimento", "rendimentos", "juros", "investimento"],
    };

    private static readonly string[] DiasSemana =
        ["domingo", "segunda", "terca", "quarta", "quinta", "sexta", "sabado"];

    private static readonly HashSet<string> PalavrasVazias = new(StringComparer.Ordinal)
    {
        "no", "na", "nos", "nas", "de", "do", "da", "dos", "das", "com", "em", "o", "a", "os", "as", "um", "uma",
        "pro", "pra", "para", "por", "reais", "real", "conto", "contos", "r$", "hoje", "ontem", "anteontem", "dia",
        "feira", "e", "que", "eu", "meu", "minha",
    };

    private readonly ICategoriasLivro _categorias;
    private readonly IRelogioDominio _relogio;

    public InterpretadorTextoHeuristico(ICategoriasLivro categorias, IRelogioDominio relogio)
    {
        _categorias = categorias;
        _relogio = relogio;
    }

    /// <summary>
    /// Heurística local não lê imagem; devolve falha amigável.
    /// </summary>
    public Task<ResultadoInterpretacao> InterpretarImagemAsync(
        Guid usuarioId,
        byte[] conteudo,
        string contentType,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ResultadoInterpretacao(
            false,
            null,
            "Não consegui ler o valor total desta imagem. Preencha o formulário ou tente outra foto, mais nítida."));
    }

    /// <summary>
    /// Extrai tipo, valor, data, descrição e categoria do texto livre.
    /// </summary>
    public async Task<ResultadoInterpretacao> InterpretarAsync(
        Guid usuarioId,
        string texto,
        CancellationToken cancellationToken)
    {
        var original = (texto ?? string.Empty).Trim();
        if (original.Length == 0)
        {
            return new ResultadoInterpretacao(
                false,
                null,
                "Escreva o que aconteceu, por exemplo: \"gastei 49,90 no almoço hoje\".");
        }

        var categorias = await _categorias.ListarVisiveisAsync(usuarioId, cancellationToken);
        var baseTexto = Normalizar(original);
        var (data, semData) = LerData(baseTexto);
        var valor = LerValor(semData);
        if (valor is null)
        {
            return new ResultadoInterpretacao(
                false,
                null,
                "Não encontrei o valor no texto. Preencha o formulário ou tente de novo com o valor, como \"R$ 45\".");
        }

        var tipo = LerTipo(baseTexto);
        var categoria = tipo is null
            ? (Id: (Guid?)null, NaoEncontrada: (string?)null)
            : ResolverCategoria(tipo, baseTexto, categorias);

        return new ResultadoInterpretacao(
            true,
            new SugestaoInterpretacao(
                tipo,
                valor.Value.Valor,
                data,
                LerDescricao(original, valor.Value.Trecho),
                categoria.Id,
                categoria.NaoEncontrada),
            null);
    }

    private static string? LerTipo(string texto)
    {
        var despesa = VerbosDespesa.Any(v => ContemPalavra(texto, v));
        var receita = VerbosReceita.Any(v => ContemPalavra(texto, v));
        if (despesa != receita)
        {
            return despesa ? TipoTransacao.Despesa : TipoTransacao.Receita;
        }

        if (!despesa)
        {
            if (TemPista(TipoTransacao.Despesa, texto) && !TemPista(TipoTransacao.Receita, texto))
            {
                return TipoTransacao.Despesa;
            }

            if (TemPista(TipoTransacao.Receita, texto) &&
                !TemPista(TipoTransacao.Despesa, texto) &&
                !ContemPalavra(texto, "pix"))
            {
                return TipoTransacao.Receita;
            }
        }

        return null;
    }

    private static bool TemPista(string tipo, string texto)
    {
        var pistas = tipo == TipoTransacao.Despesa ? PistasDespesa : PistasReceita;
        return pistas.Values.Any(termos => termos.Any(t => ContemPalavra(texto, t)));
    }

    private static (Guid? Id, string? NaoEncontrada) ResolverCategoria(
        string tipo,
        string texto,
        IReadOnlyList<Categoria> categorias)
    {
        var doTipo = categorias.Where(c => c.Tipo == tipo).ToList();
        var propria = doTipo.FirstOrDefault(c =>
            c.Escopo == EscopoCategoria.Personalizada &&
            ContemPalavra(texto, Normalizar(c.Nome)));
        if (propria is not null)
        {
            return (propria.Id, null);
        }

        var pistas = tipo == TipoTransacao.Despesa ? PistasDespesa : PistasReceita;
        foreach (var (nome, termos) in pistas)
        {
            if (!termos.Any(t => ContemPalavra(texto, t)))
            {
                continue;
            }

            var categoria = doTipo.FirstOrDefault(c =>
                string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase));
            if (categoria is not null)
            {
                return (categoria.Id, null);
            }

            var outros = doTipo.FirstOrDefault(c =>
                string.Equals(c.Nome, "Outros", StringComparison.OrdinalIgnoreCase));
            return (outros?.Id, nome);
        }

        var fallback = doTipo.FirstOrDefault(c =>
            string.Equals(c.Nome, "Outros", StringComparison.OrdinalIgnoreCase));
        return (fallback?.Id, null);
    }

    private (DateOnly Data, string SemData) LerData(string texto)
    {
        var hoje = _relogio.Hoje();

        var explicita = Regex.Match(texto, @"\b(\d{1,2})/(\d{1,2})(?:/(\d{2,4}))?\b");
        if (explicita.Success)
        {
            var dia = int.Parse(explicita.Groups[1].Value, CultureInfo.InvariantCulture);
            var mes = int.Parse(explicita.Groups[2].Value, CultureInfo.InvariantCulture);
            var anoGrupo = explicita.Groups[3].Value;
            var ano = string.IsNullOrEmpty(anoGrupo)
                ? hoje.Year
                : anoGrupo.Length == 2
                    ? 2000 + int.Parse(anoGrupo, CultureInfo.InvariantCulture)
                    : int.Parse(anoGrupo, CultureInfo.InvariantCulture);

            if (DateOnly.TryParseExact(
                    $"{ano:D4}-{mes:D2}-{dia:D2}",
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var data))
            {
                return (data, texto.Replace(explicita.Value, " ", StringComparison.Ordinal));
            }
        }

        var diaDoMes = Regex.Match(texto, @"\bdia (\d{1,2})\b");
        if (diaDoMes.Success)
        {
            var dia = int.Parse(diaDoMes.Groups[1].Value, CultureInfo.InvariantCulture);
            var candidata = new DateOnly(hoje.Year, hoje.Month, Math.Min(dia, DateTime.DaysInMonth(hoje.Year, hoje.Month)));
            if (dia <= DateTime.DaysInMonth(hoje.Year, hoje.Month))
            {
                candidata = new DateOnly(hoje.Year, hoje.Month, dia);
            }

            if (candidata > hoje)
            {
                var anterior = hoje.AddMonths(-1);
                if (dia <= DateTime.DaysInMonth(anterior.Year, anterior.Month))
                {
                    candidata = new DateOnly(anterior.Year, anterior.Month, dia);
                }
            }

            if (DateOnly.TryParse(candidata.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out _))
            {
                return (candidata, texto.Replace(diaDoMes.Value, " ", StringComparison.Ordinal));
            }
        }

        if (ContemPalavra(texto, "anteontem"))
        {
            return (hoje.AddDays(-2), texto);
        }

        if (ContemPalavra(texto, "ontem"))
        {
            return (hoje.AddDays(-1), texto);
        }

        for (var indice = 0; indice < DiasSemana.Length; indice++)
        {
            if (!ContemPalavra(texto, DiasSemana[indice]))
            {
                continue;
            }

            var recuo = ((int)hoje.DayOfWeek - indice + 7) % 7;
            return (hoje.AddDays(-recuo), texto);
        }

        return (hoje, texto);
    }

    private static (decimal Valor, string Trecho)? LerValor(string texto)
    {
        var candidatos = Regex.Matches(
            texto,
            @"(r\$\s*)?(\d{1,3}(?:\.\d{3})+(?:,\d{1,2})?|\d+(?:[.,]\d{1,2})?)(\s*(?:reais|real|contos?))?");

        Match? preferido = null;
        foreach (Match c in candidatos)
        {
            if (!string.IsNullOrEmpty(c.Groups[1].Value) ||
                !string.IsNullOrEmpty(c.Groups[3].Value) ||
                c.Groups[2].Value.Contains(',', StringComparison.Ordinal))
            {
                preferido = c;
                break;
            }
        }

        preferido ??= candidatos.Cast<Match>().FirstOrDefault(c =>
            c.Groups[2].Value != "99" || candidatos.Count == 1);

        if (preferido is null)
        {
            return null;
        }

        var bruto = preferido.Groups[2].Value;
        var normalizado = bruto.Contains(',', StringComparison.Ordinal)
            ? bruto.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal)
            : Regex.IsMatch(bruto, @"^\d+\.\d{1,2}$")
                ? bruto
                : bruto.Replace(".", string.Empty, StringComparison.Ordinal);

        if (!decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ||
            valor <= 0)
        {
            return null;
        }

        valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        return (valor, preferido.Value);
    }

    private static string LerDescricao(string original, string trechoValor)
    {
        var termosRemovidos = new HashSet<string>(
            VerbosDespesa.Concat(VerbosReceita).Where(v => !v.Contains(' ', StringComparison.Ordinal)),
            StringComparer.Ordinal);

        var escapado = Regex.Escape(trechoValor);
        var limpo = Regex.Replace(original, escapado, " ", RegexOptions.IgnoreCase);
        limpo = Regex.Replace(limpo, @"\b\d{1,2}/\d{1,2}(/\d{2,4})?\b", " ");
        limpo = Regex.Replace(limpo, @"\bdia \d{1,2}\b", " ", RegexOptions.IgnoreCase);

        var uteis = limpo
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Regex.IsMatch(p, @"^[\d.,]+$"))
            .Where(p =>
            {
                var n = Regex.Replace(Normalizar(p), @"[^\p{L}\d$]", string.Empty, RegexOptions.None);
                return n.Length > 0 &&
                       !termosRemovidos.Contains(n) &&
                       !PalavrasVazias.Contains(n) &&
                       !DiasSemana.Any(d => n.StartsWith(d, StringComparison.Ordinal));
            })
            .ToList();

        var texto = string.Join(' ', uteis).TrimEnd(',', '.', ';', ':', '!', '?').Trim();
        if (texto.Length == 0)
        {
            return string.Empty;
        }

        return char.ToUpper(texto[0], CultureInfo.GetCultureInfo("pt-BR")) + texto[1..];
    }

    private static string Normalizar(string texto)
    {
        var form = texto.ToLower(CultureInfo.GetCultureInfo("pt-BR")).Normalize(NormalizationForm.FormD);
        return string.Concat(form.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    }

    private static bool ContemPalavra(string texto, string termo)
    {
        var escapado = Regex.Escape(termo);
        return Regex.IsMatch(texto, $@"(^|[^\p{{L}}\d]){escapado}($|[^\p{{L}}\d])", RegexOptions.IgnoreCase);
    }
}
