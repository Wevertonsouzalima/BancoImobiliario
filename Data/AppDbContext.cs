using BancoImobiliario.Models;
using BancoImobiliario.Models.Casas;
using BancoImobiliario.Models.Jogo;
using Microsoft.EntityFrameworkCore;

namespace BancoImobiliario.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Configuracao> Configuracoes { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>()
            .HavePrecision(18, 2);

        configurationBuilder.Properties<DateTime>()
            .HavePrecision(0);

        configurationBuilder.Properties<string>()
            .AreUnicode(false);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigurarModuloJogo();
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Configuracao>(entity =>
        {
            entity.ToTable("Configuracoes", "Config");
            entity.HasKey(x => x.IdConfiguracao);
            entity.HasIndex(x => x.CodigoSala).IsUnique();
        });
    }

    public DbSet<Tb_Partida> Tb_Partidas => Set<Tb_Partida>();
    public DbSet<Tb_PartidaJogador> Tb_PartidaJogadores => Set<Tb_PartidaJogador>();
    public DbSet<Tb_BotPerfil> Tb_BotPerfis => Set<Tb_BotPerfil>();
    public DbSet<Tb_PartidaEvento> Tb_PartidaEventos => Set<Tb_PartidaEvento>();
    public DbSet<Tb_PartidaOrdemRolagem> Tb_PartidaOrdemRolagens => Set<Tb_PartidaOrdemRolagem>();
    public DbSet<Companhia> Companhias { get; set; }
    public DbSet<Efeito> Efeitos { get; set; }
    public DbSet<CasaEspecial> CasasEspeciais { get; set; }
    public DbSet<Imposto> Impostos { get; set; }
    public DbSet<Prisao> Prisoes { get; set; }
    public DbSet<Tabuleiros> Tabuleiros { get; set; }
    public DbSet<LayoutTabuleiro> LayoutTabuleiro { get; set; }
    public DbSet<Cidade> Cidades { get; set; }
    public DbSet<PartidaTabuleiro> PartidaTabuleiro { get; set; }
    public DbSet<TabuleiroFronteira> TabuleiroFronteira { get; set; }
    public DbSet<PartidaFronteira> PartidaFronteiras { get; set; }

}