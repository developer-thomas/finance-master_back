using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Enums;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

public sealed class ContasLivro(
    FinanceiroDbContext db,
    IRelogioDominio relogio) : IContasLivro
{
    public async Task<IReadOnlyList<ContaComSaldo>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var hoje = relogio.Hoje();
        var contas = await db.ContasFinanceiras
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderBy(c => c.CriadoEm)
            .ToListAsync(cancellationToken);

        var lancamentos = await db.Transacoes
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

    public async Task<ContaFinanceira> CriarAsync(Guid usuarioId, DadosConta dados, CancellationToken cancellationToken)
    {
        var nome = NormalizarNome(dados.Nome);
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new InvalidOperationException("O nome da conta é obrigatório.");
        }

        if (!TipoConta.Todos.Contains(dados.Tipo))
        {
            throw new InvalidOperationException("Tipo de conta inválido.");
        }

        if (dados.SaldoInicial < 0)
        {
            throw new InvalidOperationException("O saldo inicial não pode ser negativo.");
        }

        var nomeOcupado = await db.ContasFinanceiras
            .AnyAsync(
                c => c.UsuarioId == usuarioId && c.Nome.ToLower() == nome.ToLower(),
                cancellationToken);
        if (nomeOcupado)
        {
            throw new InvalidOperationException("Já existe uma conta com esse nome.");
        }

        var temPadrao = await db.ContasFinanceiras
            .AnyAsync(c => c.UsuarioId == usuarioId && c.Ativa && c.Padrao, cancellationToken);

        var agora = relogio.Agora();
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
            var atuais = await db.ContasFinanceiras
                .Where(c => c.UsuarioId == usuarioId && c.Padrao)
                .ToListAsync(cancellationToken);
            foreach (var atual in atuais)
            {
                atual.Padrao = false;
                atual.AtualizadoEm = agora;
            }
        }

        db.ContasFinanceiras.Add(conta);
        await db.SaveChangesAsync(cancellationToken);
        return conta;
    }

    private static string NormalizarNome(string nome) => nome.Trim();
}
