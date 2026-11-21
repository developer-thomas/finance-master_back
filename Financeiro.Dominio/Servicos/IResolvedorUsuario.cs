using Financeiro.Dominio.Entidades;

namespace Financeiro.Dominio.Servicos;

public interface IResolvedorUsuario
{
    Task<Usuario> ObterOuCriarAsync(IdentidadeExterna identidade, CancellationToken cancellationToken);
}
