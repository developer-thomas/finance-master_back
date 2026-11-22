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
            return new RespostaAssistente(RespostaSemIa(snapshot, texto), false);
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

        return new RespostaAssistente(RespostaSemIa(snapshot, texto), false);
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
        var modelo = _configuration["Gemini:Model"] ?? "gemini-2.0-flash";
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent?key={Uri.EscapeDataString(apiKey)}";

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

        var client = _httpClientFactory.CreateClient(nameof(InterpretadorTextoGemini));
        using var resposta = await client.PostAsJsonAsync(
            url,
            new
            {
                systemInstruction = new { parts = new[] { new { text = system.ToString() } } },
                contents,
                generationConfig = new { temperature = 0.4, maxOutputTokens = 512 },
            },
            cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogWarning("Assistente Gemini retornou {Status}.", (int)resposta.StatusCode);
            return null;
        }

        await using var stream = await resposta.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        return candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
    }

    private static string RespostaSemIa(string snapshot, string mensagem)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ainda sem Gemini configurado, segue o que o livro mostra agora:");
        sb.AppendLine();
        // Snapshot já está em português legível.
        foreach (var linha in snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(12))
        {
            sb.AppendLine(linha);
        }

        sb.AppendLine();
        sb.AppendLine($"Sua pergunta: \"{mensagem}\"");
        sb.Append("Configure Gemini:ApiKey para eu responder com mais contexto, sempre usando esses números.");
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
