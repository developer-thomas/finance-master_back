using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Interpretação via Gemini Flash (texto e imagem). Null em falha de rede/parse para fallback.
/// </summary>
public sealed class InterpretadorTextoGemini
{
    private const long TamanhoMaximoBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> MimePermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICategoriasLivro _categorias;
    private readonly IRelogioDominio _relogio;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InterpretadorTextoGemini> _logger;

    public InterpretadorTextoGemini(
        IHttpClientFactory httpClientFactory,
        ICategoriasLivro categorias,
        IRelogioDominio relogio,
        IConfiguration configuration,
        ILogger<InterpretadorTextoGemini> logger)
    {
        _httpClientFactory = httpClientFactory;
        _categorias = categorias;
        _relogio = relogio;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// True quando há chave configurada (Gemini:ApiKey ou GEMINI_API_KEY).
    /// </summary>
    public bool EstaConfigurado()
    {
        var chave = ObterApiKey();
        return !string.IsNullOrWhiteSpace(chave);
    }

    /// <summary>
    /// Tenta interpretar texto com Gemini. Null = falhou (usar heurística).
    /// </summary>
    public async Task<ResultadoInterpretacao?> TentarInterpretarAsync(
        Guid usuarioId,
        string texto,
        CancellationToken cancellationToken)
    {
        var categorias = await _categorias.ListarVisiveisAsync(usuarioId, cancellationToken);
        var hoje = _relogio.Hoje();
        var prompt = MontarPromptTexto(texto, hoje, categorias);
        var parts = new object[] { new { text = prompt } };
        return await ChamarEMapearAsync(parts, categorias, hoje, origemImagem: false, cancellationToken);
    }

    /// <summary>
    /// Tenta extrair valor total de comprovante/nota. Null = falhou.
    /// </summary>
    public async Task<ResultadoInterpretacao?> TentarInterpretarImagemAsync(
        Guid usuarioId,
        byte[] conteudo,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (conteudo.Length == 0 || conteudo.Length > TamanhoMaximoBytes)
        {
            return new ResultadoInterpretacao(
                false,
                null,
                "A imagem passa de 10 MB ou está vazia. Tente uma foto menor.");
        }

        var mime = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();
        if (string.Equals(mime, "image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            mime = "image/jpeg";
        }

        if (!MimePermitidos.Contains(mime))
        {
            return new ResultadoInterpretacao(
                false,
                null,
                "Envie uma imagem JPEG, PNG ou WebP.");
        }

        var categorias = await _categorias.ListarVisiveisAsync(usuarioId, cancellationToken);
        var hoje = _relogio.Hoje();
        var prompt = MontarPromptImagem(hoje, categorias);
        var parts = new object[]
        {
            new { inline_data = new { mime_type = mime, data = Convert.ToBase64String(conteudo) } },
            new { text = prompt },
        };
        return await ChamarEMapearAsync(parts, categorias, hoje, origemImagem: true, cancellationToken);
    }

    private async Task<ResultadoInterpretacao?> ChamarEMapearAsync(
        object[] parts,
        IReadOnlyList<Categoria> categorias,
        DateOnly hoje,
        bool origemImagem,
        CancellationToken cancellationToken)
    {
        var apiKey = ObterApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var modelo = _configuration["Gemini:Model"] ?? "gemini-2.0-flash";
            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var client = _httpClientFactory.CreateClient(nameof(InterpretadorTextoGemini));
            using var resposta = await client.PostAsJsonAsync(
                url,
                new
                {
                    contents = new[] { new { parts } },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        responseMimeType = "application/json",
                    },
                },
                cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gemini retornou {Status} ao interpretar {Origem}.",
                    (int)resposta.StatusCode,
                    origemImagem ? "imagem" : "texto");
                return null;
            }

            await using var stream = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
                || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var textoJson = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
            if (string.IsNullOrWhiteSpace(textoJson))
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<PayloadGemini>(textoJson, JsonOptions);
            if (payload is null)
            {
                return null;
            }

            return Mapear(payload, categorias, hoje, origemImagem);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao chamar Gemini ({Origem}).", origemImagem ? "imagem" : "texto");
            return null;
        }
    }

    private string? ObterApiKey()
    {
        var deConfig = _configuration["Gemini:ApiKey"];
        if (!string.IsNullOrWhiteSpace(deConfig))
        {
            return deConfig.Trim();
        }

        return Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    }

    private static string MontarPromptTexto(string texto, DateOnly hoje, IReadOnlyList<Categoria> categorias)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CabecalhoComum(hoje, categorias));
        sb.AppendLine("Texto do usuário:");
        sb.AppendLine(texto);
        return sb.ToString();
    }

    private static string MontarPromptImagem(DateOnly hoje, IReadOnlyList<Categoria> categorias)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CabecalhoComum(hoje, categorias));
        sb.AppendLine("A imagem é um comprovante, cupom ou nota. Extraia UMA transação pelo valor TOTAL pago.");
        sb.AppendLine("Não some itens separados se houver total. Sem total legível, valor=null.");
        sb.AppendLine("Se parecer despesa de compra/pagamento, tipo=DESPESA. Se for recebimento/crédito, tipo=RECEITA.");
        sb.AppendLine("descricao: estabelecimento ou resumo curto do comprovante.");
        return sb.ToString();
    }

    private static string CabecalhoComum(DateOnly hoje, IReadOnlyList<Categoria> categorias)
    {
        var nomesDespesa = string.Join(", ", categorias.Where(c => c.Tipo == TipoTransacao.Despesa).Select(c => c.Nome));
        var nomesReceita = string.Join(", ", categorias.Where(c => c.Tipo == TipoTransacao.Receita).Select(c => c.Nome));
        var sb = new StringBuilder();
        sb.AppendLine("Extraia um lançamento financeiro em português do Brasil.");
        sb.AppendLine($"Hoje (America/Sao_Paulo) é {hoje:yyyy-MM-dd}.");
        sb.AppendLine("Responda só JSON com: tipo (DESPESA|RECEITA|null), valor (number|null), data (yyyy-MM-dd|null), descricao (string), categoria (string|null).");
        sb.AppendLine("Se faltar valor ou tipo, use null nesses campos. Não invente valor.");
        sb.AppendLine($"Categorias de despesa: {nomesDespesa}.");
        sb.AppendLine($"Categorias de receita: {nomesReceita}.");
        return sb.ToString();
    }

    private static ResultadoInterpretacao Mapear(
        PayloadGemini payload,
        IReadOnlyList<Categoria> categorias,
        DateOnly hoje,
        bool origemImagem)
    {
        var tipo = NormalizarTipo(payload.Tipo);
        var valor = payload.Valor ?? 0m;
        if (valor <= 0)
        {
            return new ResultadoInterpretacao(
                false,
                null,
                origemImagem
                    ? "Não consegui ler o valor total desta imagem. Preencha o formulário ou tente outra foto, mais nítida."
                    : "Não encontrei o valor no texto. Preencha o formulário ou tente de novo com o valor, como \"R$ 45\".");
        }

        // Comprovante sem tipo explícito: assume despesa (caso mais comum no app).
        if (origemImagem && tipo is null)
        {
            tipo = TipoTransacao.Despesa;
        }

        var data = ParseData(payload.Data) ?? hoje;
        var descricao = string.IsNullOrWhiteSpace(payload.Descricao)
            ? (payload.Categoria ?? (origemImagem ? "Comprovante" : "Lançamento"))
            : payload.Descricao.Trim();

        Guid? categoriaId = null;
        string? naoEncontrada = null;
        if (tipo is not null && !string.IsNullOrWhiteSpace(payload.Categoria))
        {
            var doTipo = categorias.Where(c => c.Tipo == tipo).ToList();
            var match = doTipo.FirstOrDefault(c =>
                string.Equals(c.Nome, payload.Categoria, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                categoriaId = match.Id;
            }
            else
            {
                naoEncontrada = payload.Categoria;
                categoriaId = doTipo.FirstOrDefault(c =>
                    string.Equals(c.Nome, "Outros", StringComparison.OrdinalIgnoreCase))?.Id;
            }
        }
        else if (tipo is not null)
        {
            categoriaId = categorias
                .FirstOrDefault(c =>
                    c.Tipo == tipo &&
                    string.Equals(c.Nome, "Outros", StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        return new ResultadoInterpretacao(
            true,
            new SugestaoInterpretacao(tipo, valor, data, descricao, categoriaId, naoEncontrada),
            null);
    }

    private static string? NormalizarTipo(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return null;
        }

        var t = tipo.Trim().ToUpperInvariant();
        return t switch
        {
            "DESPESA" or "DESPESAS" or "EXPENSE" => TipoTransacao.Despesa,
            "RECEITA" or "RECEITAS" or "INCOME" => TipoTransacao.Receita,
            _ => null,
        };
    }

    private static DateOnly? ParseData(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        if (DateOnly.TryParseExact(
                data.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var d))
        {
            return d;
        }

        return DateOnly.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : null;
    }

    private sealed class PayloadGemini
    {
        [JsonPropertyName("tipo")]
        public string? Tipo { get; set; }

        [JsonPropertyName("valor")]
        public decimal? Valor { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }

        [JsonPropertyName("categoria")]
        public string? Categoria { get; set; }
    }
}
