using Financeiro.Dominio.Entidades;

namespace Financeiro.Dominio.Servicos;

public interface ICategoriasLivro
{
    Task<IReadOnlyList<Categoria>> ListarVisiveisAsync(Guid usuarioId, CancellationToken cancellationToken);

    Task<Categoria> CriarAsync(Guid usuarioId, string tipo, string nome, CancellationToken cancellationToken);
}
