namespace Financeiro.Dominio.Relogio;

/// <summary>
/// Fonte única do dia civil do domínio. Sempre America/Sao_Paulo.
/// </summary>
public interface IRelogioDominio
{
    DateOnly Hoje();

    DateTimeOffset Agora();
}
