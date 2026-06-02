using BancoImobiliario.Components;
using BancoImobiliario.Data;
using BancoImobiliario.Models;
using BancoImobiliario.Services;
using BancoImobiliario.Services.Casas;
using BancoImobiliario.Services.Jogador;
using BancoImobiliario.Services.Jogo;
using BancoImobiliario.Services.Tabuleiro;
using DllTeste.Banco.Autenticacao;            // + LOGIN
using DllTeste.Banco.Centralizador;
using DllTeste.Banco.ConexaoCentralizada.Services;
using DllTeste.Banco.SistemaPaginas.Models;
using DllTeste.Banco.SistemaPaginas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;

var builder = WebApplication.CreateBuilder(args);

var nomeSistema = builder.Configuration["Sistema:Nome"];
var ambiente = builder.Configuration["Sistema:Ambiente"];

if (string.IsNullOrWhiteSpace(nomeSistema))
    throw new Exception("Sistema:Nome não configurado no appsettings.json.");

if (string.IsNullOrWhiteSpace(ambiente))
    throw new Exception("Sistema:Ambiente não configurado no appsettings.json.");

var conexaoCentralizada = new ConexaoCentralizadaService();

var connectionString = await conexaoCentralizada.ObterConnectionStringAsync(
    nomeSistema,
    ambiente);

Console.WriteLine("Conexão centralizada resolvida com sucesso.");
Console.WriteLine($"Sistema: {nomeSistema}");
Console.WriteLine($"Ambiente: {ambiente}");

// EF Core
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
    }
});

// Serviços do Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// + LOGIN — cookie + sessão no servidor + inatividade
builder.Services.AddLoginPadrao(opcoes =>
{
    opcoes.Titulo = nomeSistema!;
    opcoes.Subtitulo = "Acesse sua conta";
    opcoes.NomeCookie = ".BancoImobiliario.Auth";
    opcoes.TempoInatividade = TimeSpan.FromMinutes(30);
    opcoes.DuracaoLembrarMe = TimeSpan.FromDays(30);
    opcoes.UrlPosLogin = "/";
});

// + LOGIN — validação no banco (hoje); troque por AD no futuro
builder.Services.AddScoped<ILoginAuthenticator, EfLoginAuthenticator<AppDbContext>>();

//Serviços
builder.Services.AddScoped<ConfiguracaoService>();
builder.Services.AddScoped<PartidaService>();
builder.Services.AddScoped<AvatarService>();

builder.Services.Configure<SistemaAplicacaoOptions>(
builder.Configuration.GetSection("Sistema"));
builder.Services.AddSingleton<CentralizadorConnectionFactory>();
builder.Services.AddScoped<SistemaPaginasService>();
builder.Services.AddScoped<BrowserIdentificacaoService>();

builder.Services.AddScoped<PrisaoService>();
builder.Services.AddScoped<ImpostoService>();
builder.Services.AddScoped<CidadeService>();
builder.Services.AddScoped<CompanhiaService>();
builder.Services.AddScoped<EfeitoService>();
builder.Services.AddScoped<CasaEspecialService>();
builder.Services.AddScoped<AcaoCasaService>();
builder.Services.AddScoped<GerenciamentoTabuleiroService>();
builder.Services.AddScoped<TabuleiroService>();
builder.Services.Configure<CoresTabuleiroOptions>(
    builder.Configuration.GetSection(CoresTabuleiroOptions.SectionName));
var app = builder.Build();



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();   // + LOGIN (antes de Authorization)
app.UseAuthorization();    // + LOGIN

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(LoginOptions).Assembly);   // + LOGIN (Router acha a /login da DLL)

app.MapLoginPadrao();      // + LOGIN (POST /account/login e /logout)

app.Run();