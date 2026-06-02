using BancoImobiliario.Models.Jogo;
using Microsoft.EntityFrameworkCore;

namespace BancoImobiliario.Data;

public static class JogoModelBuilderExtensions
{
    public static void ConfigurarModuloJogo(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tb_BotPerfil>(entity =>
        {
            entity.ToTable("Tb_BotPerfis", "Jogo");

            entity.HasKey(x => x.IdPerfilBot);

            entity.Property(x => x.Nome)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(x => x.Descricao)
                .HasMaxLength(300);

            entity.Property(x => x.DataCriacao)
                .HasColumnType("datetime2(0)");

            entity.Property(x => x.DataAtualizacao)
                .HasColumnType("datetime2(0)");

            entity.HasIndex(x => x.Nome)
                .IsUnique();
        });

        modelBuilder.Entity<Tb_Partida>(entity =>
        {
            entity.ToTable("Tb_Partidas", "Jogo");

            entity.HasKey(x => x.IdPartida);

            entity.Property(x => x.CodigoSala)
                .HasMaxLength(25)
                .IsRequired();

            entity.Property(x => x.HostNome)
                .HasMaxLength(120);

            entity.Property(x => x.HostIdentificador)
                .HasMaxLength(150);

            entity.Property(x => x.Observacao)
                .HasMaxLength(500);

            entity.Property(x => x.DataCriacao)
                .HasColumnType("datetime2(0)");

            entity.Property(x => x.DataInicio)
                .HasColumnType("datetime2(0)");

            entity.Property(x => x.DataFim)
                .HasColumnType("datetime2(0)");

            entity.Property(x => x.DataExpiracaoLobby)
                .HasColumnType("datetime2(0)");

            entity.Property(x => x.DataAtualizacao)
                .HasColumnType("datetime2(0)");

            entity.HasIndex(x => x.CodigoSala)
                .IsUnique();

            entity.HasIndex(x => x.IdConfiguracao);

            entity.HasIndex(x => new { x.IdStatusPartida, x.DataCriacao });
        });

        modelBuilder.Entity<Tb_PartidaJogador>(entity =>
        {
            entity.ToTable("Tb_PartidaJogadores", "Jogo");

            entity.HasKey(x => x.IdPartidaJogador);

            entity.Property(x => x.NomeJogador)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(x => x.IdentificadorJogador)
                .HasMaxLength(150);

            entity.Property(x => x.ConnectionIdSignalR)
                .HasMaxLength(150);

            entity.Property(x => x.CorHex)
                .HasMaxLength(20);

            entity.Property(x => x.UrlAvatar)
                .HasMaxLength(500);

            entity.Property(x => x.SaldoAtual)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.DataEntrada)
                .HasColumnType("datetime2(0)");

            entity.Property(x => x.DataSaida)
                .HasColumnType("datetime2(0)");

            entity.Property(x => x.DataAtualizacao)
                .HasColumnType("datetime2(0)");

            entity.HasIndex(x => new { x.IdPartida, x.OrdemJogador });

            entity.HasIndex(x => new { x.IdPartida, x.IdStatusJogador });

            entity.HasIndex(x => new { x.IdPartida, x.OrdemTurno })
                .HasFilter("[OrdemTurno] IS NOT NULL");
        });

        modelBuilder.Entity<Tb_PartidaEvento>(entity =>
        {
            entity.ToTable("Tb_PartidaEventos", "Jogo");

            entity.HasKey(x => x.IdPartidaEvento);

            entity.Property(x => x.Descricao)
                .HasMaxLength(500);

            entity.Property(x => x.DadosJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.DataEvento)
                .HasColumnType("datetime2(0)");

            entity.HasIndex(x => new { x.IdPartida, x.DataEvento });
        });

        modelBuilder.Entity<Tb_PartidaOrdemRolagem>(entity =>
        {
            entity.ToTable("Tb_PartidaOrdemRolagens", "Jogo");

            entity.HasKey(x => x.IdOrdemRolagem);

            entity.Property(x => x.Observacao)
                .HasMaxLength(500);

            entity.Property(x => x.DataRolagem)
                .HasColumnType("datetime2(0)");

            entity.HasIndex(x => new
            {
                x.IdPartida,
                x.RodadaDesempate,
                x.GrupoDesempate
            });

            entity.HasIndex(x => new
            {
                x.IdPartidaJogador,
                x.DataRolagem
            });

            entity.HasIndex(x => new
            {
                x.IdPartida,
                x.IdPartidaJogador,
                x.RodadaDesempate,
                x.GrupoDesempate
            })
            .IsUnique();
        });
    }
}