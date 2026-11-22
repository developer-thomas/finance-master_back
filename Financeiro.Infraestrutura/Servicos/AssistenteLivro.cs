using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Assistente de conversa: o livro calcula o snapshot; a IA só redige com esses fatos.
/// </summary>
public sealed class AssistenteLivro : IAssistenteLivro
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const int MaxHistorico = 12;
    private const int MaxMensagem = 2000;

    private readonly IPainelLivro _painel;
    private readonly IContasLivro _contas;
    private readonly IRelogioDominio _relogio;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssistenteLivro> _logger;

    public AssistenteLivro(
        IPainelLivro painel,
        IContasLivro contas,
        IRelogioDominio relogio,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AssistenteLivro> logger)
    {
        _painel = painel;
        _contas = contas;
        _relogio = relogio;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Monta o contexto oficial e pede ao Gemini uma resposta; sem chave, devolve resumo determinístico.
    /// </summary>
    public async Task<RespostaAssistente> ResponderAsync(
        Guid usuarioId,
        string mensagem,
        IReadOnlyList<MensagemAssistenteHistorico>? historico,
        CancellationToken cancellationToken)
    {
        var texto = (mensagem ?? string.Empty).Trim();
        if (texto.Length == 0)
        {
            return new RespostaAssistente(
                "Pergunte algo sobre seu saldo, gastos do mês ou hábitos. Eu uso só os números do seu livro.",
                false);
        }

        if (texto.Length > MaxMensagem)
        {
            texto = texto[..MaxMensagem];
        }

        var snapshot = await MontarSnapshotAsync(usuarioId, cancellationToken);
        var apiKey = ObterApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new RespostaAssistente(RespostaFallback(snapshot, texto, semChave: true), false);
        }

        try
        {
            var resposta = await ChamarGeminiAsync(apiKey, snapshot, texto, historico, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resposta))
            {
                return new RespostaAssistente(resposta.Trim(), true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no assistente Gemini; usando resumo local.");
        }

        return new RespostaAssistente(RespostaFallback(snapshot, texto, semChave: false), false);
    }

    private async Task<string> MontarSnapshotAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var painel = await _painel.ObterAsync(usuarioId, cancellationToken);
        var contas = await _contas.ListarAsync(usuarioId, cancellationToken);
        var hoje = _relogio.Hoje();
        var sb = new StringBuilder();
        sb.AppendLine($"Data de referência (America/Sao_Paulo): {hoje:yyyy-MM-dd}.");
        sb.AppendLine($"Saldo total (contas ativas): {Formatar(painel.SaldoTotal)}.");
        sb.AppendLine($"Mês do painel: {painel.ResumoMes.Mes}.");
        sb.AppendLine($"Receitas do mês (até hoje): {Formatar(painel.ResumoMes.Receitas)}.");
        sb.AppendLine($"Despesas do mês (até hoje): {Formatar(painel.ResumoMes.Despesas)}.");
        sb.AppendLine($"Resultado do mês: {Formatar(painel.ResumoMes.Resultado)}.");
        sb.AppendLine($"Sem movimentação registrada: {(painel.SemMovimentacao ? "sim" : "não")}.");

        var ativas = contas.Where(c => c.Conta.Ativa).ToList();
        sb.AppendLine($"Contas ativas ({ativas.Count}):");
        foreach (var c in ativas)
        {
            sb.AppendLine($"- {c.Conta.Nome}: {Formatar(c.Saldo)}{(c.Conta.Padrao ? " (padrão)" : "")}");
        }

        if (painel.ResumoMes.GastosPorCategoria.Count > 0)
        {
            sb.AppendLine("Gastos por categoria neste mês:");
            foreach (var g in painel.ResumoMes.GastosPorCategoria.Take(8))
            {
                sb.AppendLine($"- {g.Nome}: {Formatar(g.Total)} ({g.Percentual:P0})");
            }
        }

        if (painel.Recentes.Count > 0)
        {
            sb.AppendLine("Lançamentos recentes:");
            foreach (var t in painel.Recentes.Take(8))
            {
                var tx = t.Transacao;
                sb.AppendLine(
                    $"- {tx.DataMovimentacao:yyyy-MM-dd} {tx.Tipo} {Formatar(tx.Valor)} — {tx.Descricao} ({t.CategoriaNome} / {t.ContaNome})");
            }
        }

        return sb.ToString();
    }

    private async Task<string?> ChamarGeminiAsync(
        string apiKey,
        string snapshot,
        string mensagem,
        IReadOnlyList<MensagemAssistenteHistorico>? historico,
        CancellationToken cancellationToken)
    {
        var modelo = _configuration["Gemini:Model"] ?? GeminiHttp.ModeloPadrao;

        var system = new StringBuilder();
        system.AppendLine("Você é o assistente do Finance Master, app de finanças pessoais no Brasil.");
        system.AppendLine("Fale em português do Brasil, tom claro e objetivo, sem jargão desnecessário.");
        system.AppendLine("REGRAS OBRIGATÓRIAS:");
        system.AppendLine("1) Para qualquer número (saldo, totais, percentuais, valores), use SOMENTE o SNAPSHOT abaixo.");
        system.AppendLine("2) Nunca invente saldo, média, projeção ou comparação que não esteja no snapshot.");
        system.AppendLine("3) Se a pergunta pedir dado que não está no snapshot, diga que não tem esse dado no livro e sugira o que o usuário pode lançar ou olhar no app.");
        system.AppendLine("4) Pode dar orientação geral de hábitos (ex.: acompanhar categorias, revisar gastos altos), sem prometer rentabilidade.");
        system.AppendLine("5) Não peça senha, dados de cartão nem CPF. Não execute ações (não cria lançamentos).");
        system.AppendLine("6) Respostas curtas: no máximo 3 parágrafos curtos ou uma lista breve.");
        system.AppendLine();
        system.AppendLine("SNAPSHOT OFICIAL DO LIVRO:");
        system.AppendLine(snapshot);

        var contents = new List<object>();
        if (historico is not null)
        {
            foreach (var m in historico.TakeLast(MaxHistorico))
            {
                var papel = string.Equals(m.Papel, PapelAssistente.Assistente, StringComparison.OrdinalIgnoreCase)
                    ? "model"
                    : "user";
                var corpo = (m.Texto ?? string.Empty).Trim();
                if (corpo.Length == 0)
                {
                    continue;
                }

                contents.Add(new { role = papel, parts = new[] { new { text = corpo } } });
            }
        }

        contents.Add(new { role = "user", parts = new[] { new { text = mensagem } } });

        var client = _httpClientFactory.CreateClient(GeminiHttp.NomeCliente);
        using var pedido = GeminiHttp.CriarGenerateContent(
            apiKey,
            modelo,
            new
            {
                systemInstruction = new { parts = new[] { new { text = system.ToString() } } },
                contents,
                // Thinking consome maxOutputTokens; MINIMAL + teto alto evita corte no meio da frase.
                generationConfig = new
                {
                    temperature = 0.4,
                    maxOutputTokens = 8192,
                    thinkingConfig = new { thinkingLevel = "MINIMAL" },
                },
            });
        using var resposta = await client.SendAsync(pedido, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            var detalhe = await resposta.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Assistente Gemini retornou {Status} (modelo {Modelo}): {Detalhe}",
                (int)resposta.StatusCode,
                modelo,
                detalhe.Length > 300 ? detalhe[..300] : detalhe);
            return null;
        }

        await using var stream = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var candidato = candidates[0];
        if (candidato.TryGetProperty("finishReason", out var motivo)
            && string.Equals(motivo.GetString(), "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Assistente Gemini terminou por MAX_TOKENS; resposta pode estar incompleta.");
        }

        return ExtrairTextoResposta(candidato);
    }

    private static string? ExtrairTextoResposta(JsonElement candidato)
    {
        if (!candidato.TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            // Ignora partes de "thinking" quando a API as devolve no payload.
            if (part.TryGetProperty("thought", out var thought)
                && thought.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            if (part.TryGetProperty("text", out var texto))
            {
                sb.Append(texto.GetString());
            }
        }

        var resultado = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(resultado) ? null : resultado;
    }

    private static string RespostaFallback(string snapshot, string mensagem, bool semChave)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            semChave
                ? "A chave do Gemini ainda não está configurada. Enquanto isso, segue o que o livro mostra:"
                : "Não consegui falar com o Gemini agora. Enquanto isso, segue o que o livro mostra:");
        sb.AppendLine();
        foreach (var linha in snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(12))
        {
            sb.AppendLine(linha);
        }

        sb.AppendLine();
        sb.AppendLine($"Sua pergunta: \"{mensagem}\"");
        if (semChave)
        {
            sb.Append("Coloque Gemini:ApiKey em appsettings.Development.local.json e reinicie a API.");
        }
        else
        {
            sb.Append("Tente de novo em instantes. Se persistir, confira o modelo em Gemini:Model.");
        }

        return sb.ToString();
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

    private static string Formatar(decimal valor) => $"R$ {valor.ToString("N2", PtBr)}";
}
