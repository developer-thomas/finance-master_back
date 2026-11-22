using Financeiro.Dominio.Relogio;
using Financeiro.Dominio.Servicos;
using Financeiro.Infraestrutura.Persistencia;
using Financeiro.Infraestrutura.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Financeiro.Infraestrutura;

public static class Dependencias
{
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Financeiro")
            ?? throw new InvalidOperationException("ConnectionStrings:Financeiro não configurada.");

        services.AddDbContext<FinanceiroDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddHttpClient(nameof(InterpretadorTextoGemini), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
        });

        services.AddSingleton<IRelogioDominio, RelogioSaoPaulo>();
        services.AddScoped<IResolvedorUsuario, ResolvedorUsuario>();
        services.AddScoped<IContasLivro, ContasLivro>();
        services.AddScoped<ICategoriasLivro, CategoriasLivro>();
        services.AddScoped<ITransacoesLivro, TransacoesLivro>();
        services.AddScoped<IPainelLivro, PainelLivro>();
        services.AddScoped<IWhatsappLivro, WhatsappLivro>();
        services.AddScoped<IConsultasLivro, ConsultasLivro>();
        services.AddScoped<InterpretadorTextoHeuristico>();
        services.AddScoped<InterpretadorTextoGemini>();
        services.AddScoped<IInterpretadorTexto, InterpretadorTextoComposto>();
        services.AddScoped<IMensagensWhatsappLivro, MensagensWhatsappLivro>();
        services.AddScoped<IAssistenteLivro, AssistenteLivro>();

        return services;
    }
}

