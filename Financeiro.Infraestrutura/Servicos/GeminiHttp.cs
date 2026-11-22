using System.Net.Http.Json;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Chamadas HTTP ao Gemini Developer API (AI Studio).
/// </summary>
public static class GeminiHttp
{
    public const string NomeCliente = nameof(InterpretadorTextoGemini);

    /// <summary>Modelo padrão atual (2.x flash não aceita mais chaves novas).</summary>
    public const string ModeloPadrao = "gemini-3.5-flash";

    /// <summary>
    /// Monta POST generateContent com chave no header (compatível com chaves AQ.).
    /// </summary>
    public static HttpRequestMessage CriarGenerateContent(string apiKey, string modelo, object corpo)
    {
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent";
        var pedido = new HttpRequestMessage(HttpMethod.Post, url);
        pedido.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        pedido.Content = JsonContent.Create(corpo);
        return pedido;
    }
}
