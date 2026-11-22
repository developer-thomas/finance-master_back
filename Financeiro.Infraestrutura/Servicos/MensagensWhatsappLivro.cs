using System.Globalization;
using System.Text.RegularExpressions;
using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Dominio.Whatsapp;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Canal WhatsApp: identifica o usuário pelo número, responde consultas e grava texto completo na conta padrão.
/// </summary>
public sealed class MensagensWhatsappLivro : IMensagensWhatsappLivro
{
    private static readonly TimeSpan DuracaoContextoConsulta = TimeSpan.FromMinutes(30);

    private readonly FinanceiroDbContext _db;
    private readonly IRelogioDominio _relogio;
    private readonly IInterpretadorTexto _interpretador;
    private readonly ITransacoesLivro _transacoes;
    private readonly IWhatsappLivro _whatsapp;
    private readonly IConsultasLivro _consultas;

    public MensagensWhatsappLivro(
        FinanceiroDbContext db,
        IRelogioDominio relogio,
        IInterpretadorTexto interpretador,
        ITransacoesLivro transacoes,
        IWhatsappLivro whatsapp,
        IConsultasLivro consultas)
    {
        _db = db;
        _relogio = relogio;
        _interpretador = interpretador;
        _transacoes = transacoes;
        _whatsapp = whatsapp;
        _consultas = consultas;
    }

    /// <summary>
    /// Processa texto inbound com idempotência por <paramref name="identificadorExterno"/>.
    /// </summary>
    public async Task<ResultadoMensagemWhatsapp> ProcessarTextoAsync(
        string numeroDigitado,
        string texto,
        string identificadorExterno,
        CancellationToken cancellationToken)
    {
        var id = (identificadorExterno ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Identificador da mensagem é obrigatório.");
        }

        var existente = await _db.MensagensProcessadas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdentificadorExterno == id, cancellationToken);
        if (existente is not null)
        {
            return new ResultadoMensagemWhatsapp(
                existente.Status,
                existente.Resposta ?? "Mensagem já processada.",
                existente.TransacaoId,
                existente.Efeito);
        }

        var agora = _relogio.Agora();
        var registro = new MensagemProcessada
        {
            IdentificadorExterno = id,
            Status = "processando",
            RecebidaEm = agora,
            ProcessamentoIniciadoEm = agora,
        };
        _db.MensagensProcessadas.Add(registro);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var resultado = await ProcessarInternoAsync(registro, numeroDigitado, texto, cancellationToken);
            registro.Status = "concluida";
            registro.Efeito = resultado.Efeito;
            registro.Resposta = resultado.Resposta;
            registro.TransacaoId = resultado.TransacaoId;
            registro.ConcluidaEm = _relogio.Agora();
            await _db.SaveChangesAsync(cancellationToken);
            return resultado;
        }
        catch (InvalidOperationException ex)
        {
            var resultado = new ResultadoMensagemWhatsapp("concluida", ex.Message, null, "recusado");
            registro.Status = "concluida";
            registro.Efeito = resultado.Efeito;
            registro.Resposta = resultado.Resposta;
            registro.ConcluidaEm = _relogio.Agora();
            await _db.SaveChangesAsync(cancellationToken);
            return resultado;
        }
        catch (Exception)
        {
            var resultado = new ResultadoMensagemWhatsapp(
                "falha",
                "Não consegui processar essa mensagem agora. Tente de novo em instantes.",
                null,
                "erro");
            registro.Status = "falha";
            registro.Efeito = resultado.Efeito;
            registro.Resposta = resultado.Resposta;
            registro.ConcluidaEm = _relogio.Agora();
            await _db.SaveChangesAsync(cancellationToken);
            return resultado;
        }
    }

    private async Task<ResultadoMensagemWhatsapp> ProcessarInternoAsync(
        MensagemProcessada registro,
        string numeroDigitado,
        string texto,
        CancellationToken cancellationToken)
    {
        var numero = NumeroWhatsapp.Normalizar(numeroDigitado);
        if (numero is null)
        {
            return Resposta(registro, null, "ignorada", "Número inválido.", null);
        }

        var corpo = (texto ?? string.Empty).Trim();
        if (corpo.Length == 0)
        {
            return Resposta(
                registro,
                null,
                "ignorada",
                "Envie um texto com a movimentação, por exemplo: gastei 45 no almoço.",
                null);
        }

        // Código de vínculo: mensagem só com 6 dígitos.
        if (Regex.IsMatch(corpo, @"^\d{6}$"))
        {
            try
            {
                await _whatsapp.ConfirmarPorCodigoAsync(numero, corpo, cancellationToken);
                var vinculo = await _db.VinculosWhatsapp
                    .AsNoTracking()
                    .FirstAsync(v => v.NumeroE164 == numero, cancellationToken);
                registro.UsuarioId = vinculo.UsuarioId;
                return Resposta(
                    registro,
                    vinculo.UsuarioId,
                    "vinculo_confirmado",
                    "WhatsApp vinculado. Pode mandar gastos e receitas por aqui.",
                    null);
            }
            catch (InvalidOperationException ex)
            {
                return Resposta(registro, null, "vinculo_recusado", ex.Message, null);
            }
        }

        var vinculoAtivo = await _db.VinculosWhatsapp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.NumeroE164 == numero, cancellationToken);
        if (vinculoAtivo is null)
        {
            return Resposta(
                registro,
                null,
                "nao_vinculado",
                "Este número ainda não está vinculado. No app, gere um código e envie aqui.",
                null);
        }

        registro.UsuarioId = vinculoAtivo.UsuarioId;
        var usuarioId = vinculoAtivo.UsuarioId;

        // Continuação "e mês passado?" — só com contexto válido.
        if (_consultas.EhContinuacaoMesPassado(corpo))
        {
            return await ResponderContinuacaoMesPassadoAsync(registro, usuarioId, cancellationToken);
        }

        // Lista fechada de consultas (antes de exigir conta padrão).
        var pedido = _consultas.Classificar(corpo);
        if (pedido is not null)
        {
            var textoResposta = await _consultas.ResponderAsync(usuarioId, pedido, cancellationToken);
            if (pedido.Periodo != PeriodoConsulta.NaoSuportado)
            {
                await SalvarContextoConsultaAsync(usuarioId, pedido, cancellationToken);
            }

            return Resposta(registro, usuarioId, "consulta_respondida", textoResposta, null);
        }

        var contaPadrao = await _db.ContasFinanceiras
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.UsuarioId == usuarioId && c.Ativa && c.Padrao,
                cancellationToken);
        if (contaPadrao is null)
        {
            return Resposta(
                registro,
                usuarioId,
                "sem_conta",
                "Você não tem uma conta ativa padrão. Crie ou reative uma conta no app para lançar pelo WhatsApp.",
                null);
        }

        var interpretacao = await _interpretador.InterpretarAsync(usuarioId, corpo, cancellationToken);
        if (!interpretacao.Ok || interpretacao.Sugestao is null)
        {
            return Resposta(
                registro,
                usuarioId,
                "interpretacao_falhou",
                interpretacao.Motivo ?? "Não entendi a mensagem. Tente algo como: gastei 45 no almoço.",
                null);
        }

        var sugestao = interpretacao.Sugestao;
        if (string.IsNullOrWhiteSpace(sugestao.Tipo))
        {
            return Resposta(
                registro,
                usuarioId,
                "incompleto",
                "Faltou o tipo: diga se foi despesa (gastei/paguei) ou receita (recebi/ganhei).",
                null);
        }

        if (sugestao.Valor <= 0)
        {
            return Resposta(
                registro,
                usuarioId,
                "incompleto",
                "Faltou o valor. Envie de novo com o valor, por exemplo: gastei 45 no almoço.",
                null);
        }

        if (sugestao.CategoriaId is null)
        {
            return Resposta(
                registro,
                usuarioId,
                "incompleto",
                "Não consegui escolher a categoria. Tente de novo ou lance pelo app.",
                null);
        }

        var criada = await _transacoes.CriarAsync(
            usuarioId,
            new DadosTransacao(
                sugestao.Tipo,
                sugestao.Valor,
                sugestao.Data,
                sugestao.Descricao,
                sugestao.CategoriaId.Value,
                contaPadrao.Id,
                OrigemTransacao.WhatsappText,
                registro.IdentificadorExterno),
            cancellationToken);

        var detalhe = await _transacoes.ObterAsync(usuarioId, criada.Id, cancellationToken);
        var tipoRotulo = criada.Tipo == TipoTransacao.Despesa ? "Despesa" : "Receita";
        var valorBr = criada.Valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        var dataBr = criada.DataMovimentacao.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        var resposta =
            $"{tipoRotulo} de R$ {valorBr} — {criada.Descricao}. " +
            $"Categoria: {detalhe?.CategoriaNome}. Data: {dataBr}. Conta: {contaPadrao.Nome}.";

        return Resposta(registro, usuarioId, "transacao_criada", resposta, criada.Id);
    }

    private async Task<ResultadoMensagemWhatsapp> ResponderContinuacaoMesPassadoAsync(
        MensagemProcessada registro,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var agora = _relogio.Agora();
        var contexto = await _db.ContextosConsulta
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);

        if (contexto is null || contexto.ExpiraEm < agora)
        {
            return Resposta(
                registro,
                usuarioId,
                "consulta_sem_contexto",
                "Não tenho uma consulta recente para repetir. Pergunte de novo, por exemplo: quanto gastei este mês?",
                null);
        }

        var pedido = new PedidoConsulta(
            contexto.Consulta,
            PeriodoConsulta.MesPassado,
            await NomeCategoriaAsync(usuarioId, contexto.CategoriaId, cancellationToken),
            await NomeContaAsync(usuarioId, contexto.ContaId, cancellationToken));

        var textoResposta = await _consultas.ResponderAsync(usuarioId, pedido, cancellationToken);
        contexto.Periodo = PeriodoConsulta.MesPassado;
        contexto.ExpiraEm = agora.Add(DuracaoContextoConsulta);
        await _db.SaveChangesAsync(cancellationToken);

        return Resposta(registro, usuarioId, "consulta_respondida", textoResposta, null);
    }

    private async Task SalvarContextoConsultaAsync(
        Guid usuarioId,
        PedidoConsulta pedido,
        CancellationToken cancellationToken)
    {
        // Saldo não se beneficia de "mês passado"; ainda assim guarda para a regra de continuidade genérica.
        var agora = _relogio.Agora();
        Guid? categoriaId = null;
        Guid? contaId = null;

        if (!string.IsNullOrWhiteSpace(pedido.NomeCategoria))
        {
            var chave = pedido.NomeCategoria.Trim().ToLowerInvariant();
            var cats = await _db.Categorias
                .AsNoTracking()
                .Where(c =>
                    c.Escopo == EscopoCategoria.Padrao ||
                    (c.Escopo == EscopoCategoria.Personalizada && c.UsuarioId == usuarioId))
                .ToListAsync(cancellationToken);
            categoriaId = cats
                .FirstOrDefault(c => c.Nome.Equals(pedido.NomeCategoria, StringComparison.OrdinalIgnoreCase)
                    || c.Nome.ToLowerInvariant().Contains(chave))
                ?.Id;
        }

        if (!string.IsNullOrWhiteSpace(pedido.NomeConta))
        {
            var contas = await _db.ContasFinanceiras
                .AsNoTracking()
                .Where(c => c.UsuarioId == usuarioId)
                .ToListAsync(cancellationToken);
            contaId = contas
                .FirstOrDefault(c => c.Nome.Equals(pedido.NomeConta, StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        var existente = await _db.ContextosConsulta
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);
        if (existente is null)
        {
            _db.ContextosConsulta.Add(new ContextoConsulta
            {
                UsuarioId = usuarioId,
                Consulta = pedido.Tipo,
                Periodo = pedido.Periodo,
                CategoriaId = categoriaId,
                ContaId = contaId,
                ExpiraEm = agora.Add(DuracaoContextoConsulta),
            });
        }
        else
        {
            existente.Consulta = pedido.Tipo;
            existente.Periodo = pedido.Periodo;
            existente.CategoriaId = categoriaId;
            existente.ContaId = contaId;
            existente.ExpiraEm = agora.Add(DuracaoContextoConsulta);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> NomeCategoriaAsync(
        Guid usuarioId,
        Guid? categoriaId,
        CancellationToken cancellationToken)
    {
        if (categoriaId is null)
        {
            return null;
        }

        var cat = await _db.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == categoriaId &&
                     (c.Escopo == EscopoCategoria.Padrao ||
                      (c.Escopo == EscopoCategoria.Personalizada && c.UsuarioId == usuarioId)),
                cancellationToken);
        return cat?.Nome;
    }

    private async Task<string?> NomeContaAsync(
        Guid usuarioId,
        Guid? contaId,
        CancellationToken cancellationToken)
    {
        if (contaId is null)
        {
            return null;
        }

        var conta = await _db.ContasFinanceiras
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contaId && c.UsuarioId == usuarioId, cancellationToken);
        return conta?.Nome;
    }

    private static ResultadoMensagemWhatsapp Resposta(
        MensagemProcessada registro,
        Guid? usuarioId,
        string efeito,
        string texto,
        Guid? transacaoId)
    {
        if (usuarioId is not null)
        {
            registro.UsuarioId = usuarioId;
        }

        return new ResultadoMensagemWhatsapp("concluida", texto, transacaoId, efeito);
    }
}
