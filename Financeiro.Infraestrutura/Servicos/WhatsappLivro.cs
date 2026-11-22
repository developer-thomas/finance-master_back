using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Dominio.Whatsapp;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

/// <summary>
/// Vínculo de WhatsApp: emissão de código, confirmação e número único por usuário.
/// </summary>
public sealed class WhatsappLivro : IWhatsappLivro
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Cache em memória do código em claro (o banco guarda só o hash).
    /// </summary>
    private static readonly Dictionary<Guid, CodigoVinculoAtivo> CodigosEmClaro = new();

    private readonly FinanceiroDbContext _db;
    private readonly IRelogioDominio _relogio;

    public WhatsappLivro(FinanceiroDbContext db, IRelogioDominio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    /// <summary>
    /// Retorna o vínculo atual e o código pendente, se ainda válido.
    /// </summary>
    public async Task<StatusWhatsapp> ObterStatusAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var vinculo = await _db.VinculosWhatsapp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);

        var pendente = await _db.CodigosVinculo
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null && c.ExpiraEm > _relogio.Agora())
            .OrderByDescending(c => c.ExpiraEm)
            .FirstOrDefaultAsync(cancellationToken);

        CodigoVinculoAtivo? codigo = null;
        if (pendente is not null)
        {
            if (CodigosEmClaro.TryGetValue(usuarioId, out var emClaro) &&
                emClaro.NumeroE164 == pendente.NumeroE164 &&
                emClaro.ExpiraEm == pendente.ExpiraEm)
            {
                codigo = emClaro;
            }
            else
            {
                codigo = new CodigoVinculoAtivo(pendente.NumeroE164, "******", pendente.ExpiraEm);
            }
        }
        else
        {
            CodigosEmClaro.Remove(usuarioId);
        }

        return new StatusWhatsapp(
            vinculo is null ? null : new VinculoWhatsappAtivo(vinculo.NumeroE164, vinculo.VinculadoEm),
            codigo);
    }

    /// <summary>
    /// Emite um código de 6 dígitos válido por 15 minutos e invalida códigos anteriores do usuário.
    /// </summary>
    public async Task<CodigoVinculoAtivo> GerarCodigoAsync(
        Guid usuarioId,
        string numeroDigitado,
        CancellationToken cancellationToken)
    {
        var numero = NumeroWhatsapp.Normalizar(numeroDigitado)
            ?? throw new InvalidOperationException(
                "Informe um celular válido com DDD, por exemplo (11) 98765-4321.");

        var vinculoAtual = await _db.VinculosWhatsapp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);
        if (vinculoAtual?.NumeroE164 == numero)
        {
            throw new InvalidOperationException("Este número já está vinculado à sua conta.");
        }

        var numeroDeOutro = await _db.VinculosWhatsapp
            .AsNoTracking()
            .AnyAsync(v => v.NumeroE164 == numero && v.UsuarioId != usuarioId, cancellationToken);
        if (numeroDeOutro)
        {
            throw new InvalidOperationException("Este número já está vinculado a outra conta.");
        }

        var antigos = await _db.CodigosVinculo
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null)
            .ToListAsync(cancellationToken);
        _db.CodigosVinculo.RemoveRange(antigos);

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var agora = _relogio.Agora();
        var registro = new CodigoVinculo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            NumeroE164 = numero,
            CodigoHash = Hash(codigo),
            ExpiraEm = agora.Add(Validade),
        };

        _db.CodigosVinculo.Add(registro);
        await _db.SaveChangesAsync(cancellationToken);

        var ativo = new CodigoVinculoAtivo(numero, codigo, registro.ExpiraEm);
        CodigosEmClaro[usuarioId] = ativo;
        return ativo;
    }

    /// <summary>
    /// Descarta o código pendente sem criar vínculo.
    /// </summary>
    public async Task CancelarCodigoAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var antigos = await _db.CodigosVinculo
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null)
            .ToListAsync(cancellationToken);
        _db.CodigosVinculo.RemoveRange(antigos);
        await _db.SaveChangesAsync(cancellationToken);
        CodigosEmClaro.Remove(usuarioId);
    }

    /// <summary>
    /// Confirma o vínculo quando a mensagem do número traz o código vigente (fluxo WhatsApp real).
    /// </summary>
    public async Task ConfirmarPorCodigoAsync(
        string numeroE164,
        string codigo,
        CancellationToken cancellationToken)
    {
        var numero = NumeroWhatsapp.Normalizar(numeroE164)
            ?? throw new InvalidOperationException("Número inválido.");

        var agora = _relogio.Agora();
        var pendente = await _db.CodigosVinculo
            .Where(c =>
                c.NumeroE164 == numero &&
                c.UsadoEm == null &&
                c.ExpiraEm > agora)
            .OrderByDescending(c => c.ExpiraEm)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Código inválido ou expirado.");

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(pendente.CodigoHash),
                Encoding.UTF8.GetBytes(Hash(codigo.Trim()))))
        {
            throw new InvalidOperationException("Código inválido ou expirado.");
        }

        await AplicarVinculoAsync(pendente, agora, cancellationToken);
    }

    /// <summary>
    /// Confirma o código pendente do usuário sem mensagem real (apenas desenvolvimento).
    /// </summary>
    public async Task SimularConfirmacaoAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var agora = _relogio.Agora();
        var pendente = await _db.CodigosVinculo
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null && c.ExpiraEm > agora)
            .OrderByDescending(c => c.ExpiraEm)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("O código expirou. Gere um novo.");

        await AplicarVinculoAsync(pendente, agora, cancellationToken);
    }

    /// <summary>
    /// Remove o vínculo WhatsApp do usuário.
    /// </summary>
    public async Task DesvincularAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var vinculo = await _db.VinculosWhatsapp
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);
        if (vinculo is null)
        {
            return;
        }

        _db.VinculosWhatsapp.Remove(vinculo);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Cria ou troca o vínculo e marca o código como usado.
    /// </summary>
    private async Task AplicarVinculoAsync(
        CodigoVinculo pendente,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        var ocupado = await _db.VinculosWhatsapp
            .FirstOrDefaultAsync(
                v => v.NumeroE164 == pendente.NumeroE164 && v.UsuarioId != pendente.UsuarioId,
                cancellationToken);
        if (ocupado is not null)
        {
            throw new InvalidOperationException("Este número já está vinculado a outra conta.");
        }

        var atual = await _db.VinculosWhatsapp
            .FirstOrDefaultAsync(v => v.UsuarioId == pendente.UsuarioId, cancellationToken);
        if (atual is null)
        {
            _db.VinculosWhatsapp.Add(new VinculoWhatsapp
            {
                Id = Guid.NewGuid(),
                UsuarioId = pendente.UsuarioId,
                NumeroE164 = pendente.NumeroE164,
                VinculadoEm = agora,
            });
        }
        else
        {
            atual.NumeroE164 = pendente.NumeroE164;
            atual.VinculadoEm = agora;
        }

        pendente.UsadoEm = agora;
        await _db.SaveChangesAsync(cancellationToken);
        CodigosEmClaro.Remove(pendente.UsuarioId);
    }

    private static string Hash(string codigo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(codigo));
        return Convert.ToHexString(bytes);
    }

}
