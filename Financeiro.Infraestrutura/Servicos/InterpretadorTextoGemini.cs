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
/// Interpretação via Gemini Flash. Devolve null em falha de rede/parse para o chamador usar fallback.
/// </summary>
public sealed class InterpretadorTextoGemini
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
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
    /// Tenta interpretar com Gemini. Null = falhou (usar heurística).
    /// </summary>
    public async Task<ResultadoInterpretacao?> TentarInterpretarAsync(
        Guid usuarioId,
        string texto,
        CancellationToken cancellationToken)
    {
        var apiKey = ObterApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var categorias = await _categorias.ListarVisiveisAsync(usuarioId, cancellationToken);
            var hoje = _relogio.Hoje();
            var prompt = MontarPrompt(texto, hoje, categorias);

            var modelo = _configuration["Gemini:Model"] ?? "gemini-2.0-flash";
            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var client = _httpClientFactory.CreateClient(nameof(InterpretadorTextoGemini));
            using var resposta = await client.PostAsJsonAsync(
                url,
                new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } },
                    },
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
                    "Gemini retornou {Status} ao interpretar texto.",
                    (int)resposta.StatusCode);
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

            return Mapear(payload, categorias, hoje);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao chamar Gemini; usando fallback heurístico.");
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

    private static string MontarPrompt(string texto, DateOnly hoje, IReadOnlyList<Categoria> categorias)
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
        sb.AppendLine("Texto do usuário:");
        sb.AppendLine(texto);
        return sb.ToString();
    }

    private static ResultadoInterpretacao Mapear(
        PayloadGemini payload,
        IReadOnlyList<Categoria> categorias,
        DateOnly hoje)
    {
        var tipo = NormalizarTipo(payload.Tipo);
        var valor = payload.Valor ?? 0m;
        if (valor <= 0)
        {
            return new ResultadoInterpretacao(
                false,
                null,
                "Não encontrei o valor no texto. Preencha o formulário ou tente de novo com o valor, como \"R$ 45\".");
        }

        var data = ParseData(payload.Data) ?? hoje;
        var descricao = string.IsNullOrWhiteSpace(payload.Descricao)
            ? (payload.Categoria ?? "Lançamento")
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
