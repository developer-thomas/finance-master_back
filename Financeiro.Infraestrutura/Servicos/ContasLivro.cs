using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Livro de contas financeiras: listagem com saldo, criação, edição e ativação.
/// </summary>
public sealed class ContasLivro : IContasLivro
{
    private readonly FinanceiroDbContext _db;
    private readonly IRelogioDominio _relogio;

    public ContasLivro(FinanceiroDbContext db, IRelogioDominio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    /// <summary>
    /// Lista as contas do usuário com saldo até hoje e se já houve lançamento (inclui excluídos).
    /// </summary>
    public async Task<IReadOnlyList<ContaComSaldo>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var hoje = _relogio.Hoje();
        var contas = await _db.ContasFinanceiras
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderBy(c => c.CriadoEm)
            .ToListAsync(cancellationToken);

        var lancamentos = await _db.Transacoes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.UsuarioId == usuarioId)
            .Select(t => new { t.ContaId, t.Tipo, t.Valor, t.DataMovimentacao, t.ExcluidaEm })
            .ToListAsync(cancellationToken);

        return contas.Select(conta =>
        {
            var daConta = lancamentos.Where(t => t.ContaId == conta.Id).ToList();
            var saldoMovimento = daConta
                .Where(t => t.ExcluidaEm is null && t.DataMovimentacao <= hoje)
                .Sum(t => t.Tipo == TipoTransacao.Receita ? t.Valor : -t.Valor);

            return new ContaComSaldo(
                conta,
                conta.SaldoInicial + saldoMovimento,
                daConta.Count > 0);
        }).ToList();
    }

    /// <summary>
    /// Cria uma conta ativa. A primeira ativa (ou a marcada como padrão) vira a conta padrão.
    /// </summary>
    public async Task<ContaFinanceira> CriarAsync(Guid usuarioId, DadosConta dados, CancellationToken cancellationToken)
    {
        var nome = NormalizarNome(dados.Nome);
        ValidarCampos(nome, dados.Tipo, dados.SaldoInicial);

        var nomeOcupado = await _db.ContasFinanceiras
            .AnyAsync(
                c => c.UsuarioId == usuarioId && c.Nome.ToLower() == nome.ToLower(),
                cancellationToken);
        if (nomeOcupado)
        {
            throw new InvalidOperationException("Já existe uma conta com esse nome.");
        }

        var temPadrao = await _db.ContasFinanceiras
            .AnyAsync(c => c.UsuarioId == usuarioId && c.Ativa && c.Padrao, cancellationToken);

        var agora = _relogio.Agora();
        var conta = new ContaFinanceira
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Nome = nome,
            Tipo = dados.Tipo,
            SaldoInicial = decimal.Round(dados.SaldoInicial, 2, MidpointRounding.AwayFromZero),
            Ativa = true,
            Padrao = dados.Padrao || !temPadrao,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        if (conta.Padrao && temPadrao)
        {
            await LimparPadraoAsync(usuarioId, agora, cancellationToken);
        }

        _db.ContasFinanceiras.Add(conta);
        await _db.SaveChangesAsync(cancellationToken);
        return conta;
    }

    /// <summary>
    /// Atualiza nome, tipo e saldo inicial. Saldo inicial só muda sem lançamentos (mesmo excluídos).
    /// </summary>
    public async Task<ContaFinanceira> EditarAsync(
        Guid usuarioId,
        Guid id,
        DadosConta dados,
        CancellationToken cancellationToken)
    {
        var conta = await ObterAsync(usuarioId, id, cancellationToken);
        var nome = NormalizarNome(dados.Nome);
        ValidarCampos(nome, dados.Tipo, dados.SaldoInicial);

        var nomeOcupado = await _db.ContasFinanceiras
            .AnyAsync(
                c => c.UsuarioId == usuarioId && c.Id != id && c.Nome.ToLower() == nome.ToLower(),
                cancellationToken);
        if (nomeOcupado)
        {
            throw new InvalidOperationException("Já existe uma conta com esse nome.");
        }

        var temLancamentos = await _db.Transacoes
            .IgnoreQueryFilters()
            .AnyAsync(t => t.UsuarioId == usuarioId && t.ContaId == id, cancellationToken);

        var saldoInicial = decimal.Round(dados.SaldoInicial, 2, MidpointRounding.AwayFromZero);
        if (temLancamentos && saldoInicial != conta.SaldoInicial)
        {
            throw new InvalidOperationException(
                "O saldo inicial só pode ser corrigido enquanto a conta não tiver lançamentos.");
        }

        var agora = _relogio.Agora();
        conta.Nome = nome;
        conta.Tipo = dados.Tipo;
        conta.SaldoInicial = saldoInicial;
        conta.AtualizadoEm = agora;

        if (dados.Padrao && conta.Ativa)
        {
            await LimparPadraoAsync(usuarioId, agora, cancellationToken);
            conta.Padrao = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return conta;
    }

    /// <summary>
    /// Desativa a conta e promove outra ativa a padrão, se necessário.
    /// </summary>
    public async Task DesativarAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken)
    {
        var conta = await ObterAsync(usuarioId, id, cancellationToken);
        if (!conta.Ativa)
        {
            return;
        }

        var agora = _relogio.Agora();
        conta.Ativa = false;
        conta.Padrao = false;
        conta.AtualizadoEm = agora;
        await GarantirPadraoAsync(usuarioId, agora, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reativa a conta. Só vira padrão se não houver outra padrão ativa.
    /// </summary>
    public async Task ReativarAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken)
    {
        var conta = await ObterAsync(usuarioId, id, cancellationToken);
        if (conta.Ativa)
        {
            return;
        }

        var agora = _relogio.Agora();
        var temPadrao = await _db.ContasFinanceiras
            .AnyAsync(c => c.UsuarioId == usuarioId && c.Ativa && c.Padrao, cancellationToken);

        conta.Ativa = true;
        conta.Padrao = !temPadrao;
        conta.AtualizadoEm = agora;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ContaFinanceira> ObterAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken)
    {
        return await _db.ContasFinanceiras
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada.");
    }

    private async Task LimparPadraoAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var atuais = await _db.ContasFinanceiras
            .Where(c => c.UsuarioId == usuarioId && c.Padrao)
            .ToListAsync(cancellationToken);
        foreach (var atual in atuais)
        {
            atual.Padrao = false;
            atual.AtualizadoEm = agora;
        }
    }

    private async Task GarantirPadraoAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var temPadrao = await _db.ContasFinanceiras
            .AnyAsync(c => c.UsuarioId == usuarioId && c.Ativa && c.Padrao, cancellationToken);
        if (temPadrao)
        {
            return;
        }

        var maisAntiga = await _db.ContasFinanceiras
            .Where(c => c.UsuarioId == usuarioId && c.Ativa)
            .OrderBy(c => c.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);
        if (maisAntiga is null)
        {
            return;
        }

        maisAntiga.Padrao = true;
        maisAntiga.AtualizadoEm = agora;
    }

    private static void ValidarCampos(string nome, string tipo, decimal saldoInicial)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new InvalidOperationException("O nome da conta é obrigatório.");
        }

        if (!TipoConta.Todos.Contains(tipo))
        {
            throw new InvalidOperationException("Tipo de conta inválido.");
        }

        if (saldoInicial < 0)
        {
            throw new InvalidOperationException("O saldo inicial não pode ser negativo.");
        }
    }

    private static string NormalizarNome(string nome) => nome.Trim();
}
