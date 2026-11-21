using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Financeiro.Infraestrutura.Persistencia;

public sealed class FinanceiroDbContextFactory : IDesignTimeDbContextFactory<FinanceiroDbContext>
{
    public FinanceiroDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinanceiroDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=financeiro;Username=financeiro;Password=financeiro")
            .Options;

        return new FinanceiroDbContext(options);
    }
}
