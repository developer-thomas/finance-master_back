using Financeiro.Api.Autenticacao;
using Financeiro.Api.Endpoints;
using Financeiro.Infraestrutura;
using Financeiro.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Segredos locais (gitignored): appsettings.Development.local.json
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.AddInfraestrutura(builder.Configuration);

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:8100", "http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("App", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var firebaseProjectId = builder.Configuration["Firebase:ProjectId"];
var autenticacaoFirebase = !string.IsNullOrWhiteSpace(firebaseProjectId);

if (autenticacaoFirebase)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Mantém nomes curtos do Firebase (user_id, email, name).
            options.MapInboundClaims = false;
            options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
                ValidateAudience = true,
                ValidAudience = firebaseProjectId,
                ValidateLifetime = true,
                NameClaimType = "user_id",
            };
        });
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(EsquemasAutenticacao.Desenvolvimento)
        .AddScheme<AuthenticationSchemeOptions, AutenticacaoDesenvolvimentoHandler>(
            EsquemasAutenticacao.Desenvolvimento,
            _ => { });
}
else
{
    throw new InvalidOperationException("Firebase:ProjectId é obrigatório fora de Development.");
}

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("App");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ResolvedorUsuarioMiddleware>();

app.MapGet("/health", async (FinanceiroDbContext db, CancellationToken cancellationToken) =>
{
    var bancoOk = false;
    try
    {
        bancoOk = await db.Database.CanConnectAsync(cancellationToken);
    }
    catch
    {
        bancoOk = false;
    }

    return Results.Ok(new
    {
        status = bancoOk ? "ok" : "degradado",
        banco = bancoOk,
        autenticacao = autenticacaoFirebase ? "firebase" : "desenvolvimento",
        utc = DateTimeOffset.UtcNow,
    });
});

app.MapPerfilEndpoints();
app.MapContasEndpoints();
app.MapCategoriasEndpoints();
app.MapTransacoesEndpoints();
app.MapPainelEndpoints();
app.MapWhatsappEndpoints();
app.MapWhatsappMensagensEndpoints();
app.MapWhatsappWebhookEndpoints();
app.MapInterpretacaoEndpoints();

app.Run();

public partial class Program;
