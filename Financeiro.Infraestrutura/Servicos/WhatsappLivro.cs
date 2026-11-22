using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Financeiro.Dominio.Entidades;
using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infraestrutura.Servicos;

public sealed class WhatsappLivro(
    FinanceiroDbContext db,
    IRelogioDominio relogio) : IWhatsappLivro
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(15);

    // Cache em memória do código em claro só para o status da sessão atual (hash fica no banco).
    private static readonly Dictionary<Guid, CodigoVinculoAtivo> CodigosEmClaro = new();

    public async Task<StatusWhatsapp> ObterStatusAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var vinculo = await db.VinculosWhatsapp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);

        var pendente = await db.CodigosVinculo
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null && c.ExpiraEm > relogio.Agora())
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

    public async Task<CodigoVinculoAtivo> GerarCodigoAsync(
        Guid usuarioId,
        string numeroDigitado,
        CancellationToken cancellationToken)
    {
        var numero = NormalizarNumero(numeroDigitado)
            ?? throw new InvalidOperationException(
                "Informe um celular válido com DDD, por exemplo (11) 98765-4321.");

        var vinculoAtual = await db.VinculosWhatsapp
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);
        if (vinculoAtual?.NumeroE164 == numero)
        {
            throw new InvalidOperationException("Este número já está vinculado à sua conta.");
        }

        var numeroDeOutro = await db.VinculosWhatsapp
            .AsNoTracking()
            .AnyAsync(v => v.NumeroE164 == numero && v.UsuarioId != usuarioId, cancellationToken);
        if (numeroDeOutro)
        {
            throw new InvalidOperationException("Este número já está vinculado a outra conta.");
        }

        var antigos = await db.CodigosVinculo
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null)
            .ToListAsync(cancellationToken);
        db.CodigosVinculo.RemoveRange(antigos);

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var agora = relogio.Agora();
        var registro = new CodigoVinculo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            NumeroE164 = numero,
            CodigoHash = Hash(codigo),
            ExpiraEm = agora.Add(Validade),
        };

        db.CodigosVinculo.Add(registro);
        await db.SaveChangesAsync(cancellationToken);

        var ativo = new CodigoVinculoAtivo(numero, codigo, registro.ExpiraEm);
        CodigosEmClaro[usuarioId] = ativo;
        return ativo;
    }

    public async Task CancelarCodigoAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var antigos = await db.CodigosVinculo
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null)
            .ToListAsync(cancellationToken);
        db.CodigosVinculo.RemoveRange(antigos);
        await db.SaveChangesAsync(cancellationToken);
        CodigosEmClaro.Remove(usuarioId);
    }

    public async Task ConfirmarPorCodigoAsync(
        string numeroE164,
        string codigo,
        CancellationToken cancellationToken)
    {
        var numero = NormalizarNumero(numeroE164)
            ?? throw new InvalidOperationException("Número inválido.");

        var agora = relogio.Agora();
        var pendente = await db.CodigosVinculo
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

    public async Task SimularConfirmacaoAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var agora = relogio.Agora();
        var pendente = await db.CodigosVinculo
            .Where(c => c.UsuarioId == usuarioId && c.UsadoEm == null && c.ExpiraEm > agora)
            .OrderByDescending(c => c.ExpiraEm)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("O código expirou. Gere um novo.");

        await AplicarVinculoAsync(pendente, agora, cancellationToken);
    }

    public async Task DesvincularAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var vinculo = await db.VinculosWhatsapp
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);
        if (vinculo is null)
        {
            return;
        }

        db.VinculosWhatsapp.Remove(vinculo);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AplicarVinculoAsync(
        CodigoVinculo pendente,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        var ocupado = await db.VinculosWhatsapp
            .FirstOrDefaultAsync(
                v => v.NumeroE164 == pendente.NumeroE164 && v.UsuarioId != pendente.UsuarioId,
                cancellationToken);
        if (ocupado is not null)
        {
            throw new InvalidOperationException("Este número já está vinculado a outra conta.");
        }

        var atual = await db.VinculosWhatsapp
            .FirstOrDefaultAsync(v => v.UsuarioId == pendente.UsuarioId, cancellationToken);
        if (atual is null)
        {
            db.VinculosWhatsapp.Add(new VinculoWhatsapp
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
        await db.SaveChangesAsync(cancellationToken);
        CodigosEmClaro.Remove(pendente.UsuarioId);
    }

    private static string Hash(string codigo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(codigo));
        return Convert.ToHexString(bytes);
    }

    public static string? NormalizarNumero(string entrada)
    {
        var digitos = Regex.Replace(entrada, @"\D", string.Empty);
        if (digitos.Length is 12 or 13)
        {
            if (!digitos.StartsWith("55", StringComparison.Ordinal))
            {
                return null;
            }

            digitos = digitos[2..];
        }

        if (digitos.Length is not (10 or 11))
        {
            return null;
        }

        if (digitos.Length == 11 && digitos[2] != '9')
        {
            return null;
        }

        return $"+55{digitos}";
    }
}
