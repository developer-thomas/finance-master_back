using Financeiro.Dominio.Entidades;

namespace Financeiro.Dominio.Servicos;

/// <summary>
/// Contrato do livro de categorias visíveis ao usuário.
/// </summary>
public interface ICategoriasLivro
{
    /// <summary>Lista categorias padrão e personalizadas do dono.</summary>
    Task<IReadOnlyList<Categoria>> ListarVisiveisAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Cria categoria personalizada única por tipo.</summary>
    Task<Categoria> CriarAsync(Guid usuarioId, string tipo, string nome, CancellationToken cancellationToken);
}
