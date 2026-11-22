using Financeiro.Dominio.Servicos;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Tenta Gemini quando há API key; se falhar ou não estiver configurado, usa heurística local.
/// </summary>
public sealed class InterpretadorTextoComposto : IInterpretadorTexto
{
    private readonly InterpretadorTextoGemini _gemini;
    private readonly InterpretadorTextoHeuristico _heuristico;

    public InterpretadorTextoComposto(
        InterpretadorTextoGemini gemini,
        InterpretadorTextoHeuristico heuristico)
    {
        _gemini = gemini;
        _heuristico = heuristico;
    }

    /// <summary>
    /// Interpreta o texto: Gemini primeiro (se configurado), senão heurística.
    /// </summary>
    public async Task<ResultadoInterpretacao> InterpretarAsync(
        Guid usuarioId,
        string texto,
        CancellationToken cancellationToken)
    {
        if (_gemini.EstaConfigurado())
        {
            var viaGemini = await _gemini.TentarInterpretarAsync(usuarioId, texto, cancellationToken);
            if (viaGemini is not null)
            {
                return viaGemini;
            }
        }

        return await _heuristico.InterpretarAsync(usuarioId, texto, cancellationToken);
    }

    /// <summary>
    /// Interpreta imagem via Gemini; sem chave ou em falha, mensagem amigável (sem heurística local).
    /// </summary>
    public async Task<ResultadoInterpretacao> InterpretarImagemAsync(
        Guid usuarioId,
        byte[] conteudo,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (_gemini.EstaConfigurado())
        {
            var viaGemini = await _gemini.TentarInterpretarImagemAsync(
                usuarioId,
                conteudo,
                contentType,
                cancellationToken);
            if (viaGemini is not null)
            {
                return viaGemini;
            }
        }

        return await _heuristico.InterpretarImagemAsync(usuarioId, conteudo, contentType, cancellationToken);
    }
}
