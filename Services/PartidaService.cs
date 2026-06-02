using BancoImobiliario.Data;
using BancoImobiliario.Models;
using BancoImobiliario.Models.Casas;
using BancoImobiliario.Models.DTOs.Jogo;
using BancoImobiliario.Models.Enums;
using BancoImobiliario.Models.Helpers;
using BancoImobiliario.Models.Jogo;
using Microsoft.EntityFrameworkCore;

namespace BancoImobiliario.Services;

public class PartidaService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<PartidaService> _logger;

    // Apos esta quantidade de rodadas de desempate, o empate restante
    // e decidido por sorteio aleatorio entre os jogadores empatados.
    private const int LimiteRodadasDesempate = 5;

    private static readonly int[] StatusJogadoresAtivos =
    [
        (int)StatusJogadorPartida.Aguardando,
        (int)StatusJogadorPartida.Pronto,
        (int)StatusJogadorPartida.Jogando
    ];

    public PartidaService(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<PartidaService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<CriarPartidaResultadoDTO> CriarPartidaAsync(
        Configuracao configuracao,
        string? nomeHost = null,
        string? identificadorHost = null,
        CancellationToken cancellationToken = default)
    {
        if (configuracao == null)
            throw new ArgumentNullException(nameof(configuracao));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            AplicarPadroesConfiguracao(configuracao);
            ValidarConfiguracaoParaPartida(configuracao);

            var codigoSala = await GerarCodigoSalaUnicoAsync(context, cancellationToken);

            configuracao.IdConfiguracao = 0;
            configuracao.CodigoSala = codigoSala;

            context.Set<Configuracao>().Add(configuracao);
            await context.SaveChangesAsync(cancellationToken);

            var agora = DateTime.Now;

            var partida = new Tb_Partida
            {
                IdConfiguracao = configuracao.IdConfiguracao,
                CodigoSala = codigoSala,
                TipoJogo = configuracao.TipoJogo,
                IdStatusPartida = (int)StatusPartida.Lobby,
                QtdMaxJogadores = configuracao.QtdJogadores,
                HostNome = NormalizarNomeJogador(nomeHost, "Jogador 1"),
                HostIdentificador = NormalizarTextoOpcional(identificadorHost),
                RodadaDesempateAtual = 1,
                DataCriacao = agora,
                DataExpiracaoLobby = agora.AddHours(2)
            };

            context.Tb_Partidas.Add(partida);
            await context.SaveChangesAsync(cancellationToken);

            var jogadorHost = new Tb_PartidaJogador
            {
                IdPartida = partida.IdPartida,
                NomeJogador = partida.HostNome ?? "Jogador 1",
                TipoJogador = (int)TipoJogadorPartida.Humano,
                IdStatusJogador = (int)StatusJogadorPartida.Pronto,
                EhHost = true,
                OrdemJogador = 1,
                SaldoAtual = Convert.ToDecimal(configuracao.ValorInicial),
                IdentificadorJogador = partida.HostIdentificador,
                DataEntrada = agora
            };

            context.Tb_PartidaJogadores.Add(jogadorHost);
            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                jogadorHost.IdPartidaJogador,
                TipoEventoPartida.SalaCriada,
                $"Sala {codigoSala} criada.",
                cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                jogadorHost.IdPartidaJogador,
                TipoEventoPartida.JogadorEntrou,
                $"{jogadorHost.NomeJogador} entrou como host.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new CriarPartidaResultadoDTO
            {
                IdPartida = partida.IdPartida,
                IdConfiguracao = configuracao.IdConfiguracao,
                CodigoSala = partida.CodigoSala,
                TipoJogo = partida.TipoJogo,
                QtdMaxJogadores = partida.QtdMaxJogadores
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao criar partida. TipoJogo={TipoJogo}; QtdJogadores={QtdJogadores}",
                configuracao.TipoJogo,
                configuracao.QtdJogadores);

            throw;
        }
    }

    public async Task<LobbyPartidaDTO?> ObterLobbyAsync(
        int idPartida,
        string? identificadorUsuarioAtual = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var partida = await context.Tb_Partidas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

        if (partida == null)
            return null;

        var configuracao = await context.Set<Configuracao>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdConfiguracao == partida.IdConfiguracao, cancellationToken);

        var jogadores = await (
            from jogador in context.Tb_PartidaJogadores.AsNoTracking()
            join perfilBot in context.Tb_BotPerfis.AsNoTracking()
                on jogador.IdPerfilBot equals perfilBot.IdPerfilBot into perfilBotJoin
            from perfilBot in perfilBotJoin.DefaultIfEmpty()
            where jogador.IdPartida == idPartida
            orderby jogador.OrdemTurno ?? 9999, jogador.OrdemJogador
            select new
            {
                Jogador = jogador,
                PerfilBot = perfilBot
            })
            .ToListAsync(cancellationToken);

        var usuarioAtual = !string.IsNullOrWhiteSpace(identificadorUsuarioAtual)
            ? jogadores
                .Select(x => x.Jogador)
                .FirstOrDefault(x =>
                    x.IdentificadorJogador == identificadorUsuarioAtual &&
                    StatusJogadoresAtivos.Contains(x.IdStatusJogador))
            : null;

        var usuarioAtualIdJogador = usuarioAtual?.IdPartidaJogador;

        var jogadoresDto = jogadores
            .Select(x => MapearJogadorLobby(
                x.Jogador,
                x.PerfilBot,
                partida,
                usuarioAtualIdJogador))
            .ToList();

        var jogadoresAtivos = jogadoresDto
            .Where(x => StatusJogadoresAtivos.Contains(x.IdStatusJogador))
            .ToList();

        var usuarioAtualEhHost = usuarioAtual?.EhHost == true;

        var todosComOrdem = jogadoresAtivos.Count > 0 &&
                            jogadoresAtivos.All(x => x.OrdemTurno.HasValue);

        var existeEmpatePendente = jogadoresAtivos.Any(x => x.ParticipaDesempateOrdem);

        return new LobbyPartidaDTO
        {
            IdPartida = partida.IdPartida,
            IdConfiguracao = partida.IdConfiguracao,
            CodigoSala = partida.CodigoSala,
            TipoJogo = partida.TipoJogo,
            TipoJogoDescricao = EnumDescricaoHelper.ObterDescricao((TipoJogoConfiguracao)partida.TipoJogo),
            IdStatusPartida = partida.IdStatusPartida,
            StatusPartidaDescricao = EnumDescricaoHelper.ObterDescricao((StatusPartida)partida.IdStatusPartida),
            QtdMaxJogadores = partida.QtdMaxJogadores,
            QtdJogadoresAtivos = jogadoresAtivos.Count,
            HostNome = partida.HostNome,
            UsuarioAtualEhHost = usuarioAtualEhHost,
            UsuarioAtualIdPartidaJogador = usuarioAtual?.IdPartidaJogador,
            UsuarioAtualNome = usuarioAtual?.NomeJogador,
            PodeAdicionarJogadorOuBot = usuarioAtualEhHost &&
                                        partida.IdStatusPartida == (int)StatusPartida.Lobby &&
                                        jogadoresAtivos.Count < partida.QtdMaxJogadores,
            PodeIniciarDefinicaoOrdem = usuarioAtualEhHost &&
                                         partida.IdStatusPartida == (int)StatusPartida.Lobby &&
                                         jogadoresAtivos.Count >= 2,
            PodeIniciarPartida = usuarioAtualEhHost &&
                                  partida.IdStatusPartida == (int)StatusPartida.OrdemDefinida &&
                                  todosComOrdem,
            ExisteEmpatePendente = existeEmpatePendente,
            TodosJogadoresComOrdemDefinida = todosComOrdem,
            DataCriacao = partida.DataCriacao,
            Jogadores = jogadoresDto,
            ValMinDado = configuracao?.ValMinDado ?? 1,
            ValMaxDado = configuracao?.ValMaxDado ?? 6,
        };
    }

    public async Task<List<BotPerfilDTO>> ListarPerfisBotAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Tb_BotPerfis
            .AsNoTracking()
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new BotPerfilDTO
            {
                IdPerfilBot = x.IdPerfilBot,
                Nome = x.Nome,
                Descricao = x.Descricao,
                DificuldadePadrao = x.DificuldadePadrao,
                DificuldadePadraoDescricao = EnumDescricaoHelper.ObterDescricao((DificuldadeBot)x.DificuldadePadrao)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<LobbyPartidaDTO> AdicionarBotAsync(
        AdicionarBotDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == dto.IdPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaEmLobby(partida);

            var perfilBot = await context.Tb_BotPerfis
                .FirstOrDefaultAsync(x => x.IdPerfilBot == dto.IdPerfilBot && x.Ativo, cancellationToken);

            if (perfilBot == null)
                throw new InvalidOperationException("Perfil de bot nao encontrado ou inativo.");

            var jogadoresAtivos = await ObterJogadoresAtivosQuery(context, partida.IdPartida)
                .ToListAsync(cancellationToken);

            if (jogadoresAtivos.Count >= partida.QtdMaxJogadores)
                throw new InvalidOperationException("A sala ja atingiu a quantidade maxima de jogadores.");

            var ordem = jogadoresAtivos.Count == 0
                ? 1
                : jogadoresAtivos.Max(x => x.OrdemJogador) + 1;

            var nomeBotBase = string.IsNullOrWhiteSpace(dto.NomeBot)
                ? $"Bot {perfilBot.Nome}"
                : dto.NomeBot.Trim();

            var nomeBot = GerarNomeUnicoJogador(nomeBotBase, jogadoresAtivos.Select(x => x.NomeJogador));

            var dificuldade = dto.DificuldadeBot ?? perfilBot.DificuldadePadrao;

            if (dificuldade is < 1 or > 3)
                dificuldade = perfilBot.DificuldadePadrao;

            var bot = new Tb_PartidaJogador
            {
                IdPartida = partida.IdPartida,
                NomeJogador = nomeBot,
                TipoJogador = (int)TipoJogadorPartida.Bot,
                IdStatusJogador = (int)StatusJogadorPartida.Pronto,
                EhHost = false,
                OrdemJogador = ordem,
                SaldoAtual = 0,
                IdPerfilBot = perfilBot.IdPerfilBot,
                DificuldadeBot = dificuldade,
                DataEntrada = DateTime.Now
            };

            context.Tb_PartidaJogadores.Add(bot);
            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                bot.IdPartidaJogador,
                TipoEventoPartida.BotAdicionado,
                $"Bot {bot.NomeJogador} adicionado.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(partida.IdPartida, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao adicionar bot. IdPartida={IdPartida}; IdPerfilBot={IdPerfilBot}",
                dto.IdPartida,
                dto.IdPerfilBot);

            throw;
        }
    }

    // ============================================================
    // SUBSTITUA o método RemoverBotAsync existente por este.
    // Agora remove bot OU jogador local humano, mas NUNCA o host.
    // (A assinatura é a mesma, então o lobby continua chamando igual;
    //  só renomeei para deixar claro que serve aos dois.)
    // ============================================================

    public async Task<LobbyPartidaDTO> RemoverJogadorAsync(
        int idPartida,
        int idPartidaJogador,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaEmLobby(partida);

            var jogador = await context.Tb_PartidaJogadores
                .FirstOrDefaultAsync(x =>
                    x.IdPartida == idPartida &&
                    x.IdPartidaJogador == idPartidaJogador,
                    cancellationToken);

            if (jogador == null)
                throw new InvalidOperationException("Jogador nao encontrado.");

            if (jogador.EhHost)
                throw new InvalidOperationException("O host nao pode ser removido da sala.");

            jogador.IdStatusJogador = (int)StatusJogadorPartida.Removido;
            jogador.DataSaida = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            var ehBot = jogador.TipoJogador == (int)TipoJogadorPartida.Bot;

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                jogador.IdPartidaJogador,
                ehBot ? TipoEventoPartida.BotRemovido : TipoEventoPartida.JogadorSaiu,
                ehBot
                    ? $"Bot {jogador.NomeJogador} removido."
                    : $"{jogador.NomeJogador} removido da sala.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(partida.IdPartida, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao remover jogador. IdPartida={IdPartida}; IdPartidaJogador={IdPartidaJogador}",
                idPartida,
                idPartidaJogador);

            throw;
        }
    }
    // ============================================================
    // Cole este método na classe PartidaService (junto dos demais).
    // Edita nome e/ou cor de um JOGADOR LOCAL (humano não-host), por
    // IdPartidaJogador — já que o jogador local não tem identificador.
    //
    // Bots NÃO passam por aqui. O host edita os próprios dados pelos
    // cards/modal de aparência dele.
    // ============================================================

    /// <summary>
    /// Atualiza nome e cor de um jogador humano local (Local Multiplayer).
    /// Identifica pelo IdPartidaJogador (jogador local não tem identificador de browser).
    /// </summary>
    public async Task<LobbyPartidaDTO> AtualizarJogadorLocalAsync(
        int idPartida,
        int idPartidaJogador,
        string? nome,
        string? corHex,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaEmLobby(partida);

            var jogador = await context.Tb_PartidaJogadores
                .FirstOrDefaultAsync(x =>
                    x.IdPartida == idPartida &&
                    x.IdPartidaJogador == idPartidaJogador,
                    cancellationToken);

            if (jogador == null)
                throw new InvalidOperationException("Jogador nao encontrado.");

            if (jogador.EhHost)
                throw new InvalidOperationException("O host nao pode ser editado por esta acao.");

            if (jogador.TipoJogador != (int)TipoJogadorPartida.Humano)
                throw new InvalidOperationException("Somente jogadores humanos locais podem ser editados por esta acao.");

            if (!StatusJogadoresAtivos.Contains(jogador.IdStatusJogador))
                throw new InvalidOperationException("Jogador nao esta ativo na partida.");

            // ---- Nome ----
            var nomeNormalizado = string.IsNullOrWhiteSpace(nome)
                ? jogador.NomeJogador
                : nome.Trim();

            if (!string.Equals(nomeNormalizado, jogador.NomeJogador, StringComparison.Ordinal))
            {
                var nomeJaExiste = await context.Tb_PartidaJogadores
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.IdPartida == idPartida &&
                        x.IdPartidaJogador != jogador.IdPartidaJogador &&
                        x.NomeJogador == nomeNormalizado &&
                        StatusJogadoresAtivos.Contains(x.IdStatusJogador),
                        cancellationToken);

                if (nomeJaExiste)
                    throw new InvalidOperationException("Ja existe outro jogador ativo com esse nome na sala.");
            }

            // ---- Cor ----
            var corNormalizada = string.IsNullOrWhiteSpace(corHex) ? null : corHex.Trim();

            if (corNormalizada is { Length: > 20 })
                throw new InvalidOperationException("Cor invalida.");

            jogador.NomeJogador = nomeNormalizado;
            jogador.CorHex = corNormalizada;
            jogador.DataAtualizacao = DateTime.Now;

            partida.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                jogador.IdPartidaJogador,
                TipoEventoPartida.ConfiguracaoAlterada,
                $"{jogador.NomeJogador} teve os dados atualizados no lobby.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(partida.IdPartida, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao atualizar jogador local. IdPartida={IdPartida}; IdPartidaJogador={IdPartidaJogador}",
                idPartida,
                idPartidaJogador);

            throw;
        }
    }
    public async Task<LobbyPartidaDTO> RemoverBotAsync(
        int idPartida,
        int idPartidaJogador,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaEmLobby(partida);

            var jogador = await context.Tb_PartidaJogadores
                .FirstOrDefaultAsync(x =>
                    x.IdPartida == idPartida &&
                    x.IdPartidaJogador == idPartidaJogador,
                    cancellationToken);

            if (jogador == null)
                throw new InvalidOperationException("Jogador nao encontrado.");

            if (jogador.TipoJogador != (int)TipoJogadorPartida.Bot)
                throw new InvalidOperationException("Somente bots podem ser removidos por esta acao.");

            jogador.IdStatusJogador = (int)StatusJogadorPartida.Removido;
            jogador.DataSaida = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                jogador.IdPartidaJogador,
                TipoEventoPartida.BotRemovido,
                $"Bot {jogador.NomeJogador} removido.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(partida.IdPartida, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao remover bot. IdPartida={IdPartida}; IdPartidaJogador={IdPartidaJogador}",
                idPartida,
                idPartidaJogador);

            throw;
        }
    }

    public async Task<LobbyPartidaDTO> IniciarDefinicaoOrdemAsync(
        int idPartida,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaEmLobby(partida);

            var jogadoresAtivos = await ObterJogadoresAtivosQuery(context, idPartida)
                .OrderBy(x => x.OrdemJogador)
                .ToListAsync(cancellationToken);

            if (jogadoresAtivos.Count < 2)
                throw new InvalidOperationException("E necessario ter pelo menos 2 jogadores para definir a ordem.");

            foreach (var jogador in jogadoresAtivos)
            {
                jogador.OrdemTurno = null;
                jogador.UltimoResultadoOrdem = null;
                jogador.ParticipaDesempateOrdem = true;
                jogador.DataAtualizacao = DateTime.Now;
            }

            var rolagensAntigas = await context.Tb_PartidaOrdemRolagens
                .Where(x => x.IdPartida == idPartida)
                .ToListAsync(cancellationToken);

            if (rolagensAntigas.Count > 0)
                context.Tb_PartidaOrdemRolagens.RemoveRange(rolagensAntigas);

            // Reinicia o controle de rodada de desempate.
            partida.RodadaDesempateAtual = 1;
            partida.IdStatusPartida = (int)StatusPartida.DefinindoOrdem;
            partida.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                null,
                TipoEventoPartida.DefinicaoOrdemIniciada,
                "Definicao da ordem dos jogadores iniciada.",
                cancellationToken);

            // Bots rolam automaticamente; depois tenta resolver a ordem.
            await RolarBotsPendentesNoContextoAsync(context, partida, cancellationToken);
            await TentarResolverOrdemNoContextoAsync(context, partida, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(partida.IdPartida, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao iniciar definicao de ordem. IdPartida={IdPartida}",
                idPartida);

            throw;
        }
    }

    public async Task<RolarDadoOrdemResultadoDTO> RolarDadoOrdemAsync(
        int idPartida,
        int idPartidaJogador,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaDefinindoOrdem(partida);

            var jogador = await context.Tb_PartidaJogadores
                .FirstOrDefaultAsync(x =>
                    x.IdPartida == idPartida &&
                    x.IdPartidaJogador == idPartidaJogador,
                    cancellationToken);

            if (jogador == null)
                throw new InvalidOperationException("Jogador nao encontrado.");

            if (jogador.TipoJogador == (int)TipoJogadorPartida.Bot)
                throw new InvalidOperationException("Bots rolam automaticamente.");

            var resultado = await RolarDadoOrdemNoContextoAsync(
                context,
                partida,
                jogador,
                automatico: false,
                cancellationToken);

            await RolarBotsPendentesNoContextoAsync(context, partida, cancellationToken);
            await TentarResolverOrdemNoContextoAsync(context, partida, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var lobby = await ObterLobbyObrigatorioAsync(
                partida.IdPartida, jogador.IdentificadorJogador, cancellationToken);

            return new RolarDadoOrdemResultadoDTO
            {
                IdPartida = partida.IdPartida,
                IdPartidaJogador = jogador.IdPartidaJogador,
                NomeJogador = jogador.NomeJogador,
                ResultadoDado = resultado.ResultadoDado,
                RodadaDesempate = resultado.RodadaDesempate,
                GrupoDesempate = resultado.GrupoDesempate,
                OrdemFoiDefinida = lobby.IdStatusPartida == (int)StatusPartida.OrdemDefinida,
                ExisteEmpatePendente = lobby.ExisteEmpatePendente,
                LobbyAtualizado = lobby
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao rolar dado para ordem. IdPartida={IdPartida}; IdPartidaJogador={IdPartidaJogador}",
                idPartida,
                idPartidaJogador);

            throw;
        }
    }

    public async Task<LobbyPartidaDTO> RolarBotsPendentesAsync(
        int idPartida,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaDefinindoOrdem(partida);

            await RolarBotsPendentesNoContextoAsync(context, partida, cancellationToken);
            await TentarResolverOrdemNoContextoAsync(context, partida, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(idPartida, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao rolar bots pendentes. IdPartida={IdPartida}",
                idPartida);

            throw;
        }
    }

    public async Task IniciarPartidaAsync(
        int idPartida,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            if (partida.IdStatusPartida != (int)StatusPartida.OrdemDefinida)
                throw new InvalidOperationException("A partida so pode ser iniciada apos a definicao da ordem dos jogadores.");

            var jogadoresAtivos = await ObterJogadoresAtivosQuery(context, idPartida)
                .OrderBy(x => x.OrdemTurno)
                .ToListAsync(cancellationToken);

            if (jogadoresAtivos.Count < 2)
                throw new InvalidOperationException("A partida precisa de pelo menos 2 jogadores.");

            if (jogadoresAtivos.Any(x => !x.OrdemTurno.HasValue))
                throw new InvalidOperationException("Ainda existem jogadores sem ordem de turno definida.");

            var configuracao = await context.Set<Configuracao>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdConfiguracao == partida.IdConfiguracao, cancellationToken);

            if (configuracao == null)
                throw new InvalidOperationException("Configuracao da partida nao encontrada.");

            foreach (var jogador in jogadoresAtivos)
            {
                jogador.IdStatusJogador = (int)StatusJogadorPartida.Jogando;
                jogador.SaldoAtual = Convert.ToDecimal(configuracao.ValorInicial);
                jogador.DataAtualizacao = DateTime.Now;
            }

            partida.IdStatusPartida = (int)StatusPartida.EmAndamento;
            partida.DataInicio = DateTime.Now;
            partida.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                null,
                TipoEventoPartida.PartidaIniciada,
                "Partida iniciada.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao iniciar partida. IdPartida={IdPartida}",
                idPartida);

            throw;
        }
    }

    /// <summary>
    /// Adiciona um jogador humano local (Local Multiplayer) à sala.
    /// Esse jogador compartilha o dispositivo do host e não possui
    /// IdentificadorJogador (controle é manual, não por browser).
    /// </summary>
    public async Task<LobbyPartidaDTO> AdicionarJogadorLocalAsync(
        AdicionarJogadorLocalDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == dto.IdPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            ValidarPartidaEmLobby(partida);

            // Jogador local só faz sentido em Local Multiplayer.
            if (partida.TipoJogo != (int)TipoJogoConfiguracao.LocalMultiplayer)
                throw new InvalidOperationException("Jogadores locais só podem ser adicionados em partidas Local Multiplayer.");

            var jogadoresAtivos = await ObterJogadoresAtivosQuery(context, partida.IdPartida)
                .ToListAsync(cancellationToken);

            if (jogadoresAtivos.Count >= partida.QtdMaxJogadores)
                throw new InvalidOperationException("A sala ja atingiu a quantidade maxima de jogadores.");

            var configuracao = await context.Set<Configuracao>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdConfiguracao == partida.IdConfiguracao, cancellationToken);

            if (configuracao == null)
                throw new InvalidOperationException("Configuracao da partida nao encontrada.");

            var ordem = jogadoresAtivos.Count == 0
                ? 1
                : jogadoresAtivos.Max(x => x.OrdemJogador) + 1;

            var nomeBase = string.IsNullOrWhiteSpace(dto.Nome)
                ? $"Jogador {ordem}"
                : dto.Nome.Trim();

            var nomeJogador = GerarNomeUnicoJogador(nomeBase, jogadoresAtivos.Select(x => x.NomeJogador));

            var corHex = string.IsNullOrWhiteSpace(dto.CorHex) ? null : dto.CorHex.Trim();
            var urlAvatar = string.IsNullOrWhiteSpace(dto.UrlAvatar) ? null : dto.UrlAvatar.Trim();

            if (corHex is { Length: > 20 })
                throw new InvalidOperationException("Cor invalida.");

            if (urlAvatar is { Length: > 500 })
                throw new InvalidOperationException("Caminho da imagem invalido.");

            var jogador = new Tb_PartidaJogador
            {
                IdPartida = partida.IdPartida,
                NomeJogador = nomeJogador,
                TipoJogador = (int)TipoJogadorPartida.Humano,
                IdStatusJogador = (int)StatusJogadorPartida.Pronto,
                EhHost = false,
                OrdemJogador = ordem,
                SaldoAtual = Convert.ToDecimal(configuracao.ValorInicial),
                IdentificadorJogador = null, // jogador local não tem browser próprio
                CorHex = corHex,
                UrlAvatar = urlAvatar,
                DataEntrada = DateTime.Now
            };

            context.Tb_PartidaJogadores.Add(jogador);
            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                jogador.IdPartidaJogador,
                TipoEventoPartida.JogadorEntrou,
                $"{jogador.NomeJogador} entrou como jogador local.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(partida.IdPartida, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao adicionar jogador local. IdPartida={IdPartida}",
                dto.IdPartida);

            throw;
        }
    }

    public async Task<LobbyPartidaDTO> AtualizarNomeJogadorAsync(
        int idPartida,
        string identificadorJogador,
        string novoNome,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identificadorJogador))
            throw new InvalidOperationException("Identificador do jogador nao informado.");

        if (string.IsNullOrWhiteSpace(novoNome))
            throw new InvalidOperationException("Informe o nome do jogador.");

        novoNome = novoNome.Trim();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            if (partida.IdStatusPartida != (int)StatusPartida.Lobby &&
                partida.IdStatusPartida != (int)StatusPartida.DefinindoOrdem &&
                partida.IdStatusPartida != (int)StatusPartida.OrdemDefinida)
            {
                throw new InvalidOperationException("Nao e possivel alterar o nome neste momento.");
            }

            var jogador = await context.Tb_PartidaJogadores
                .FirstOrDefaultAsync(x =>
                    x.IdPartida == idPartida &&
                    x.IdentificadorJogador == identificadorJogador &&
                    StatusJogadoresAtivos.Contains(x.IdStatusJogador),
                    cancellationToken);

            if (jogador == null)
                throw new InvalidOperationException("Jogador atual nao encontrado nesta sala.");

            var nomeJaExiste = await context.Tb_PartidaJogadores
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IdPartida == idPartida &&
                    x.IdPartidaJogador != jogador.IdPartidaJogador &&
                    x.NomeJogador == novoNome &&
                    StatusJogadoresAtivos.Contains(x.IdStatusJogador),
                    cancellationToken);

            if (nomeJaExiste)
                throw new InvalidOperationException("Ja existe outro jogador ativo com esse nome na sala.");

            jogador.NomeJogador = novoNome;
            jogador.DataAtualizacao = DateTime.Now;

            if (jogador.EhHost)
                partida.HostNome = novoNome;

            partida.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                idPartida,
                jogador.IdPartidaJogador,
                TipoEventoPartida.ConfiguracaoAlterada,
                $"{novoNome} atualizou o nome no lobby.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(idPartida, identificadorJogador, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao atualizar nome do jogador. IdPartida={IdPartida}; Identificador={Identificador}",
                idPartida,
                identificadorJogador);

            throw;
        }
    }
    // ============================================================
    // Adicione este metodo a classe PartidaService.
    //
    // Tambem adicione este using no topo do arquivo, junto dos demais:
    //     using BancoImobiliario.Models.Casas;
    // ============================================================

    /// <summary>
    /// Le todas as casas do tabuleiro de uma partida, ordenadas pela Posicao.
    /// A tela de jogo deve sempre reler por aqui, pois a Posicao das casas pode
    /// mudar em runtime (efeito de reordenacao do tabuleiro).
    /// </summary>
    public async Task<List<TabuleiroCasaDTO>> ObterTabuleiroPartidaAsync(
        int idPartida,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var casas = await context.Set<PartidaTabuleiro>()
            .AsNoTracking()
            .Where(x => x.PartidaId == idPartida)
            .OrderBy(x => x.Posicao)
            .ToListAsync(cancellationToken);

        return casas
            .Select(x => new TabuleiroCasaDTO
            {
                PartidaTabuleiroId = x.PartidaTabuleiroId,
                Posicao = x.Posicao,
                TipoCasaId = x.TipoCasaId,
                TipoCasaDescricao = DescreverTipoCasa(x.TipoCasaId),
                Nome = x.Nome,
                Imagem = x.Imagem,
                CorHexadecimal = x.CorHexadecimal,
                GrupoId = x.GrupoId,
                ProprietarioId = x.ProprietarioId,
                QtdCasas = x.QtdCasas,
                QtdHoteis = x.QtdHoteis,
                IsRevelada = x.IsRevelada,
                ValorCompraAtual = x.ValorCompraAtual,
                ValorAluguelAtual = x.ValorAluguelAtual
            })
            .ToList();
    }

    /// <summary>
    /// Traduz o TipoCasaId para uma descricao legivel.
    /// 1=Cidade, 2=Companhia, 3=Imposto, 4=Prisao, 5=Efeito, 6=Especial.
    /// </summary>
    private static string DescreverTipoCasa(byte tipoCasaId)
    {
        switch (tipoCasaId)
        {
            case 1:
                return "Cidade";

            case 2:
                return "Companhia";

            case 3:
                return "Imposto";

            case 4:
                return "Prisao";

            case 5:
                return "Efeito";

            case 6:
                return "Especial";

            default:
                return "Casa";
        }
    }

    /// <summary>
    /// Atualiza a cor e/ou o avatar (foto) de um jogador na partida.
    /// </summary>
    public async Task<LobbyPartidaDTO> AtualizarAparenciaJogadorAsync(
        int idPartida,
        string identificadorJogador,
        string? corHex,
        string? urlAvatar,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identificadorJogador))
            throw new InvalidOperationException("Identificador do jogador nao informado.");

        corHex = string.IsNullOrWhiteSpace(corHex) ? null : corHex.Trim();
        urlAvatar = string.IsNullOrWhiteSpace(urlAvatar) ? null : urlAvatar.Trim();

        if (corHex is { Length: > 20 })
            throw new InvalidOperationException("Cor invalida.");

        if (urlAvatar is { Length: > 500 })
            throw new InvalidOperationException("Caminho da imagem invalido.");

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var partida = await context.Tb_Partidas
                .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

            if (partida == null)
                throw new InvalidOperationException("Partida nao encontrada.");

            if (partida.IdStatusPartida != (int)StatusPartida.Lobby &&
                partida.IdStatusPartida != (int)StatusPartida.DefinindoOrdem &&
                partida.IdStatusPartida != (int)StatusPartida.OrdemDefinida)
            {
                throw new InvalidOperationException("Nao e possivel alterar a aparencia neste momento.");
            }

            var jogador = await context.Tb_PartidaJogadores
                .FirstOrDefaultAsync(x =>
                    x.IdPartida == idPartida &&
                    x.IdentificadorJogador == identificadorJogador &&
                    StatusJogadoresAtivos.Contains(x.IdStatusJogador),
                    cancellationToken);

            if (jogador == null)
                throw new InvalidOperationException("Jogador atual nao encontrado nesta sala.");

            jogador.CorHex = corHex;
            jogador.UrlAvatar = urlAvatar;
            jogador.DataAtualizacao = DateTime.Now;

            partida.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                idPartida,
                jogador.IdPartidaJogador,
                TipoEventoPartida.ConfiguracaoAlterada,
                $"{jogador.NomeJogador} atualizou a aparencia no lobby.",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await ObterLobbyObrigatorioAsync(idPartida, identificadorJogador, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Erro ao atualizar aparencia do jogador. IdPartida={IdPartida}; Identificador={Identificador}",
                idPartida,
                identificadorJogador);

            throw;
        }
    }
    /// <summary>
    /// Obtém a configuração vinculada a uma partida específica.
    /// Útil para a tela principal do jogo ler o IdTabuleiro e outras regras ativas.
    /// </summary>
    public async Task<Configuracao?> ObterConfiguracaoAsync(
        int idPartida,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var partida = await context.Tb_Partidas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdPartida == idPartida, cancellationToken);

        if (partida == null)
            return null;

        return await context.Set<Configuracao>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdConfiguracao == partida.IdConfiguracao, cancellationToken);
    }

    // ============================================================
    // DEFINICAO DE ORDEM
    //
    // A partida tem UMA rodada de desempate em andamento, controlada
    // por partida.RodadaDesempateAtual (comeca em 1).
    // Cada jogador rola UMA vez nessa rodada. Se ja tem rolagem nela,
    // nao pode rolar de novo. A rodada so incrementa quando o
    // TentarResolverOrdem detecta empate e promove os empatados.
    // Apos LimiteRodadasDesempate, o empate restante e decidido por
    // sorteio aleatorio.
    // ============================================================

    /// <summary>
    /// Registra a rolagem de um jogador na rodada de desempate atual da partida.
    /// Recusa se o jogador ja rolou nessa rodada.
    /// </summary>
    private async Task<Tb_PartidaOrdemRolagem> RolarDadoOrdemNoContextoAsync(
        AppDbContext context,
        Tb_Partida partida,
        Tb_PartidaJogador jogador,
        bool automatico,
        CancellationToken cancellationToken)
    {
        if (!StatusJogadoresAtivos.Contains(jogador.IdStatusJogador))
            throw new InvalidOperationException("Jogador nao esta ativo na partida.");

        if (!jogador.ParticipaDesempateOrdem)
            throw new InvalidOperationException("Este jogador nao precisa rolar dado neste momento.");

        var rodada = partida.RodadaDesempateAtual;

        // Cada jogador rola apenas uma vez por rodada de desempate.
        var jaRolou = await context.Tb_PartidaOrdemRolagens
            .AsNoTracking()
            .AnyAsync(x =>
                x.IdPartida == partida.IdPartida &&
                x.IdPartidaJogador == jogador.IdPartidaJogador &&
                x.RodadaDesempate == rodada,
                cancellationToken);

        if (jaRolou)
            throw new InvalidOperationException("Voce ja rolou o dado nesta rodada. Aguarde os demais jogadores.");

        var configuracao = await context.Set<Configuracao>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdConfiguracao == partida.IdConfiguracao, cancellationToken);

        if (configuracao == null)
            throw new InvalidOperationException("Configuracao da partida nao encontrada.");

        var resultadoDado = RolarDado(configuracao);

        var rolagem = new Tb_PartidaOrdemRolagem
        {
            IdPartida = partida.IdPartida,
            IdPartidaJogador = jogador.IdPartidaJogador,
            RodadaDesempate = rodada,
            GrupoDesempate = 1, // mantido por compatibilidade com o schema
            ResultadoDado = resultadoDado,
            Automatico = automatico,
            DataRolagem = DateTime.Now,
            Observacao = automatico ? "Rolagem automatica de bot." : "Rolagem manual de jogador."
        };

        context.Tb_PartidaOrdemRolagens.Add(rolagem);

        jogador.UltimoResultadoOrdem = resultadoDado;
        jogador.DataAtualizacao = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        await RegistrarEventoAsync(
            context,
            partida.IdPartida,
            jogador.IdPartidaJogador,
            TipoEventoPartida.DadoOrdemRolado,
            $"{jogador.NomeJogador} rolou {resultadoDado} para definicao da ordem.",
            cancellationToken);

        return rolagem;
    }

    /// <summary>
    /// Faz todos os bots pendentes rolarem na rodada de desempate atual,
    /// caso ainda nao tenham rolado nela.
    /// </summary>
    private async Task RolarBotsPendentesNoContextoAsync(
        AppDbContext context,
        Tb_Partida partida,
        CancellationToken cancellationToken)
    {
        var rodada = partida.RodadaDesempateAtual;

        var botsPendentes = await ObterJogadoresAtivosQuery(context, partida.IdPartida)
            .Where(x =>
                x.TipoJogador == (int)TipoJogadorPartida.Bot &&
                x.ParticipaDesempateOrdem)
            .OrderBy(x => x.OrdemJogador)
            .ToListAsync(cancellationToken);

        foreach (var bot in botsPendentes)
        {
            var jaRolou = await context.Tb_PartidaOrdemRolagens
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IdPartida == partida.IdPartida &&
                    x.IdPartidaJogador == bot.IdPartidaJogador &&
                    x.RodadaDesempate == rodada,
                    cancellationToken);

            if (!jaRolou)
            {
                await RolarDadoOrdemNoContextoAsync(
                    context, partida, bot, automatico: true, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Verifica se a rodada de desempate atual pode ser resolvida.
    /// - Se algum pendente ainda nao rolou: retorna false (aguarda).
    /// - Se todos rolaram sem empate: define a ordem final.
    /// - Se houve empate: promove os empatados para a proxima rodada
    ///   (incrementa RodadaDesempateAtual) e os bots rolam de novo.
    /// - Apos LimiteRodadasDesempate: decide por sorteio.
    /// </summary>
    private async Task<bool> TentarResolverOrdemNoContextoAsync(
        AppDbContext context,
        Tb_Partida partida,
        CancellationToken cancellationToken)
    {
        var pendentes = await ObterJogadoresAtivosQuery(context, partida.IdPartida)
            .Where(x => x.ParticipaDesempateOrdem)
            .OrderBy(x => x.OrdemJogador)
            .ToListAsync(cancellationToken);

        if (pendentes.Count == 0)
        {
            await DefinirOrdemFinalNoContextoAsync(context, partida, cancellationToken);
            return true;
        }

        var rodada = partida.RodadaDesempateAtual;

        // Todos os pendentes precisam ter rolado na rodada atual.
        var rolagensRodada = await context.Tb_PartidaOrdemRolagens
            .AsNoTracking()
            .Where(x =>
                x.IdPartida == partida.IdPartida &&
                x.RodadaDesempate == rodada)
            .ToListAsync(cancellationToken);

        var idsPendentes = pendentes.Select(x => x.IdPartidaJogador).ToHashSet();

        var rolagensPendentes = rolagensRodada
            .Where(x => idsPendentes.Contains(x.IdPartidaJogador))
            .ToList();

        // Ainda falta alguem rolar nesta rodada -> nao resolve agora.
        if (rolagensPendentes.Select(x => x.IdPartidaJogador).Distinct().Count() < pendentes.Count)
            return false;

        // Todos rolaram. Pega o ultimo resultado de cada pendente nesta rodada.
        var resultadoPorJogador = rolagensPendentes
            .GroupBy(x => x.IdPartidaJogador)
            .Select(g => new
            {
                IdPartidaJogador = g.Key,
                Resultado = g.OrderByDescending(r => r.DataRolagem).First().ResultadoDado
            })
            .ToList();

        // Quem empatou continua; quem tem valor unico esta resolvido.
        var idsAindaEmpatados = resultadoPorJogador
            .GroupBy(x => x.Resultado)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(x => x.IdPartidaJogador))
            .ToHashSet();

        // Limite de rodadas atingido -> desempata por sorteio.
        if (idsAindaEmpatados.Count > 0 && rodada >= LimiteRodadasDesempate)
        {
            var empatados = pendentes
                .Where(x => idsAindaEmpatados.Contains(x.IdPartidaJogador))
                .ToList();

            await ResolverEmpatePorSorteioNoContextoAsync(
                context, partida, empatados, rodada, cancellationToken);

            await DefinirOrdemFinalNoContextoAsync(context, partida, cancellationToken);
            return true;
        }

        // Atualiza quem continua no desempate.
        foreach (var jogador in pendentes)
        {
            jogador.ParticipaDesempateOrdem = idsAindaEmpatados.Contains(jogador.IdPartidaJogador);
            jogador.DataAtualizacao = DateTime.Now;
        }

        if (idsAindaEmpatados.Count > 0)
        {
            // Avanca para a proxima rodada de desempate.
            partida.RodadaDesempateAtual = rodada + 1;
            partida.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            await RegistrarEventoAsync(
                context,
                partida.IdPartida,
                null,
                TipoEventoPartida.EmpateOrdemDetectado,
                "Empate detectado. Jogadores empatados devem rolar novamente.",
                cancellationToken);

            // Bots empatados rolam a nova rodada automaticamente.
            await RolarBotsPendentesNoContextoAsync(context, partida, cancellationToken);

            // Apos os bots rolarem, tenta resolver de novo (caso so restem bots).
            return await TentarResolverOrdemNoContextoAsync(context, partida, cancellationToken);
        }

        // Ninguem mais empatado -> ordem definida.
        await context.SaveChangesAsync(cancellationToken);
        await DefinirOrdemFinalNoContextoAsync(context, partida, cancellationToken);
        return true;
    }

    /// <summary>
    /// Decide o desempate restante por sorteio aleatorio, gravando uma
    /// rolagem decisora unica para cada jogador empatado.
    /// </summary>
    private async Task ResolverEmpatePorSorteioNoContextoAsync(
        AppDbContext context,
        Tb_Partida partida,
        List<Tb_PartidaJogador> empatados,
        int rodadaAtual,
        CancellationToken cancellationToken)
    {
        var ordemSorteada = empatados
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var valorDecisor = ordemSorteada.Count;

        foreach (var jogador in ordemSorteada)
        {
            var rolagem = new Tb_PartidaOrdemRolagem
            {
                IdPartida = partida.IdPartida,
                IdPartidaJogador = jogador.IdPartidaJogador,
                RodadaDesempate = rodadaAtual + 1,
                GrupoDesempate = 1,
                ResultadoDado = valorDecisor,
                Automatico = true,
                DataRolagem = DateTime.Now,
                Observacao = "Desempate decidido por sorteio (limite de rodadas atingido)."
            };

            context.Tb_PartidaOrdemRolagens.Add(rolagem);

            jogador.UltimoResultadoOrdem = valorDecisor;
            jogador.ParticipaDesempateOrdem = false;
            jogador.DataAtualizacao = DateTime.Now;

            valorDecisor--;
        }

        partida.RodadaDesempateAtual = rodadaAtual + 1;
        partida.DataAtualizacao = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        await RegistrarEventoAsync(
            context,
            partida.IdPartida,
            null,
            TipoEventoPartida.EmpateOrdemDetectado,
            "Limite de rodadas de desempate atingido. Ordem decidida por sorteio.",
            cancellationToken);
    }

    /// <summary>
    /// Calcula a ordem de turno final, com base na sequencia de rolagens
    /// de cada jogador (resultados mais altos primeiro).
    /// </summary>
    private async Task DefinirOrdemFinalNoContextoAsync(
        AppDbContext context,
        Tb_Partida partida,
        CancellationToken cancellationToken)
    {
        var jogadoresAtivos = await ObterJogadoresAtivosQuery(context, partida.IdPartida)
            .OrderBy(x => x.OrdemJogador)
            .ToListAsync(cancellationToken);

        var sequencias = new List<SequenciaRolagemJogador>();

        foreach (var jogador in jogadoresAtivos)
        {
            var resultados = await context.Tb_PartidaOrdemRolagens
                .AsNoTracking()
                .Where(x =>
                    x.IdPartida == partida.IdPartida &&
                    x.IdPartidaJogador == jogador.IdPartidaJogador)
                .OrderBy(x => x.RodadaDesempate)
                .ThenBy(x => x.DataRolagem)
                .Select(x => x.ResultadoDado)
                .ToListAsync(cancellationToken);

            if (resultados.Count == 0)
                return; // alguem ainda nao rolou; nao finaliza

            sequencias.Add(new SequenciaRolagemJogador(jogador, resultados));
        }

        var jogadoresOrdenados = sequencias
            .OrderBy(x => x, new SequenciaRolagemJogadorComparer())
            .Select(x => x.Jogador)
            .ToList();

        var ordem = 1;

        foreach (var jogador in jogadoresOrdenados)
        {
            jogador.OrdemTurno = ordem++;
            jogador.ParticipaDesempateOrdem = false;
            jogador.DataAtualizacao = DateTime.Now;
        }

        partida.IdStatusPartida = (int)StatusPartida.OrdemDefinida;
        partida.DataAtualizacao = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        await RegistrarEventoAsync(
            context,
            partida.IdPartida,
            null,
            TipoEventoPartida.OrdemTurnoDefinida,
            "Ordem dos jogadores definida.",
            cancellationToken);
    }

    private static int RolarDado(Configuracao configuracao)
    {
        var minimo = configuracao.ValMinDado;
        var maximo = configuracao.ValMaxDado;

        // Config nunca preenchida (ambos zerados) -> fallback padrao 1-6.
        if (minimo == 0 && maximo == 0)
        {
            minimo = 1;
            maximo = 6;
        }

        if (maximo < minimo)
            maximo = minimo;

        // Monta a lista de valores validos: intervalo [min,max] menos os
        // numeros restritos informados em RestricaoDado (separados por virgula).
        var restritos = ParsearNumerosRestritos(configuracao.RestricaoDado);

        var validos = new List<int>();
        for (var v = minimo; v <= maximo; v++)
        {
            if (!restritos.Contains(v))
                validos.Add(v);
        }

        // Se as restricoes esvaziarem o intervalo, ignora-as (fallback seguro)
        // para nao travar a partida. Sorteia do intervalo puro.
        if (validos.Count == 0)
        {
            for (var v = minimo; v <= maximo; v++)
                validos.Add(v);
        }

        return validos[Random.Shared.Next(validos.Count)];
    }

    /// <summary>
    /// Interpreta o campo RestricaoDado como uma lista de inteiros separados
    /// por virgula. Tolera espacos e ignora pedacos invalidos.
    /// </summary>
    private static HashSet<int> ParsearNumerosRestritos(string? restricaoDado)
    {
        var resultado = new HashSet<int>();

        if (string.IsNullOrWhiteSpace(restricaoDado))
            return resultado;

        var partes = restricaoDado.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var parte in partes)
        {
            if (int.TryParse(parte, out var numero))
                resultado.Add(numero);
        }

        return resultado;
    }

    private IQueryable<Tb_PartidaJogador> ObterJogadoresAtivosQuery(
        AppDbContext context,
        int idPartida)
    {
        return context.Tb_PartidaJogadores
            .Where(x =>
                x.IdPartida == idPartida &&
                StatusJogadoresAtivos.Contains(x.IdStatusJogador));
    }

    private async Task<LobbyPartidaDTO> ObterLobbyObrigatorioAsync(
        int idPartida,
        string? identificadorUsuarioAtual,
        CancellationToken cancellationToken)
    {
        var lobby = await ObterLobbyAsync(idPartida, identificadorUsuarioAtual, cancellationToken);

        return lobby ?? throw new InvalidOperationException("Lobby nao encontrado.");
    }

    private static void AplicarPadroesConfiguracao(Configuracao configuracao)
    {
        if (configuracao.TipoJogo <= 0)
            configuracao.TipoJogo = (int)TipoJogoConfiguracao.Solo;

        if (configuracao.QtdJogadores <= 0)
            configuracao.QtdJogadores = 2;

        if (configuracao.Dificuldade <= 0)
            configuracao.Dificuldade = (int)DificuldadeJogo.Normal;

        // Dado: so aplica padrao se a config nunca foi preenchida (ambos zerados).
        if (configuracao.ValMinDado == 0 && configuracao.ValMaxDado == 0)
        {
            configuracao.ValMinDado = 1;
            configuracao.ValMaxDado = 6;
        }

        if (configuracao.ValMaxDado < configuracao.ValMinDado)
            configuracao.ValMaxDado = configuracao.ValMinDado;

        if (configuracao.TipoFinalizacao <= 0)
            configuracao.TipoFinalizacao = (int)TipoFinalizacaoJogo.NumeroVoltas;

        if (configuracao.VenceQuem <= 0)
            configuracao.VenceQuem = (int)VenceQuemJogo.MaiorSaldoTotal;

        if (configuracao.RegraFronteira <= 0)
            configuracao.RegraFronteira = (int)RegraFronteiraJogo.DonoDeApenasUma;

        if (configuracao.PercentualDevolucaoVenda <= 0)
            configuracao.PercentualDevolucaoVenda = 50;

        if (configuracao.DataCriacao == default)
            configuracao.DataCriacao = DateTime.Now;

        configuracao.StatusJogo = 1;
    }

    private static void ValidarConfiguracaoParaPartida(Configuracao configuracao)
    {
        if (configuracao.QtdJogadores < 2)
            throw new InvalidOperationException("A partida precisa ter pelo menos 2 jogadores.");

        if (configuracao.ValorInicial < 0)
            throw new InvalidOperationException("O valor inicial nao pode ser negativo.");

        if (configuracao.ValMaxDado < configuracao.ValMinDado)
            throw new InvalidOperationException("O valor maximo do dado deve ser maior ou igual ao valor minimo.");

        if (configuracao.QtdMaxCasas < configuracao.MinCasasParaHotel)
            throw new InvalidOperationException("A quantidade maxima de casas nao pode ser menor que o minimo de casas para hotel.");

        if (configuracao.PercentualDevolucaoVenda is < 0 or > 100)
            throw new InvalidOperationException("O percentual de devolucao em venda deve estar entre 0 e 100.");
    }

    private static void ValidarPartidaEmLobby(Tb_Partida partida)
    {
        if (partida.IdStatusPartida != (int)StatusPartida.Lobby)
            throw new InvalidOperationException("Esta acao so pode ser feita enquanto a partida esta no lobby.");
    }

    private static void ValidarPartidaDefinindoOrdem(Tb_Partida partida)
    {
        if (partida.IdStatusPartida != (int)StatusPartida.DefinindoOrdem)
            throw new InvalidOperationException("Esta acao so pode ser feita enquanto a partida esta definindo a ordem dos jogadores.");
    }

    private async Task<string> GerarCodigoSalaUnicoAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        for (var tentativa = 1; tentativa <= 30; tentativa++)
        {
            var codigo = GerarCodigoSala();

            var existe = await context.Tb_Partidas
                .AsNoTracking()
                .AnyAsync(x => x.CodigoSala == codigo, cancellationToken);

            if (!existe)
                return codigo;
        }

        throw new InvalidOperationException("Nao foi possivel gerar um codigo de sala unico.");
    }

    private static string GerarCodigoSala()
    {
        var numero = Random.Shared.Next(100000, 999999);
        return $"SALA-{numero}";
    }

    private static string NormalizarNomeJogador(string? nome, string padrao)
    {
        return string.IsNullOrWhiteSpace(nome)
            ? padrao
            : nome.Trim();
    }

    private static string? NormalizarTextoOpcional(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }

    private static string GerarNomeUnicoJogador(
        string nomeBase,
        IEnumerable<string> nomesExistentes)
    {
        var nomes = nomesExistentes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!nomes.Contains(nomeBase))
            return nomeBase;

        for (var i = 2; i <= 99; i++)
        {
            var nome = $"{nomeBase} {i}";

            if (!nomes.Contains(nome))
                return nome;
        }

        throw new InvalidOperationException("Nao foi possivel gerar um nome unico para o jogador.");
    }

    private static LobbyJogadorDTO MapearJogadorLobby(
        Tb_PartidaJogador jogador,
        Tb_BotPerfil? perfilBot,
        Tb_Partida partida,
        int? usuarioAtualIdPartidaJogador)
    {
        var tipoJogador = (TipoJogadorPartida)jogador.TipoJogador;
        var statusJogador = (StatusJogadorPartida)jogador.IdStatusJogador;

        var podeRolar = partida.IdStatusPartida == (int)StatusPartida.DefinindoOrdem &&
                        jogador.TipoJogador == (int)TipoJogadorPartida.Humano &&
                        jogador.ParticipaDesempateOrdem &&
                        usuarioAtualIdPartidaJogador == jogador.IdPartidaJogador;

        return new LobbyJogadorDTO
        {
            IdPartidaJogador = jogador.IdPartidaJogador,
            IdPartida = jogador.IdPartida,
            NomeJogador = jogador.NomeJogador,
            TipoJogador = jogador.TipoJogador,
            TipoJogadorDescricao = EnumDescricaoHelper.ObterDescricao(tipoJogador),
            IdStatusJogador = jogador.IdStatusJogador,
            StatusJogadorDescricao = EnumDescricaoHelper.ObterDescricao(statusJogador),
            EhHost = jogador.EhHost,
            EhBot = jogador.TipoJogador == (int)TipoJogadorPartida.Bot,
            OrdemJogador = jogador.OrdemJogador,
            OrdemTurno = jogador.OrdemTurno,
            UltimoResultadoOrdem = jogador.UltimoResultadoOrdem,
            ParticipaDesempateOrdem = jogador.ParticipaDesempateOrdem,
            PodeRolarDadoOrdem = podeRolar,
            SaldoAtual = jogador.SaldoAtual,
            IdPerfilBot = jogador.IdPerfilBot,
            NomePerfilBot = perfilBot?.Nome,
            DificuldadeBot = jogador.DificuldadeBot,
            DificuldadeBotDescricao = jogador.DificuldadeBot.HasValue
                ? EnumDescricaoHelper.ObterDescricao((DificuldadeBot)jogador.DificuldadeBot.Value)
                : null,
            CorHex = jogador.CorHex,
            UrlAvatar = jogador.UrlAvatar
        };
    }

    private static async Task RegistrarEventoAsync(
        AppDbContext context,
        int idPartida,
        int? idPartidaJogador,
        TipoEventoPartida tipoEvento,
        string descricao,
        CancellationToken cancellationToken)
    {
        var evento = new Tb_PartidaEvento
        {
            IdPartida = idPartida,
            IdPartidaJogador = idPartidaJogador,
            TipoEvento = (int)tipoEvento,
            Descricao = descricao,
            DataEvento = DateTime.Now
        };

        context.Tb_PartidaEventos.Add(evento);
        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record SequenciaRolagemJogador(
        Tb_PartidaJogador Jogador,
        List<int> Resultados);

    private sealed class SequenciaRolagemJogadorComparer : IComparer<SequenciaRolagemJogador>
    {
        public int Compare(SequenciaRolagemJogador? x, SequenciaRolagemJogador? y)
        {
            if (x == null && y == null)
                return 0;

            if (x == null)
                return 1;

            if (y == null)
                return -1;

            var limite = Math.Max(x.Resultados.Count, y.Resultados.Count);

            for (var i = 0; i < limite; i++)
            {
                var valorX = i < x.Resultados.Count ? x.Resultados[i] : int.MinValue;
                var valorY = i < y.Resultados.Count ? y.Resultados[i] : int.MinValue;

                var comparacao = valorY.CompareTo(valorX);

                if (comparacao != 0)
                    return comparacao;
            }

            return x.Jogador.OrdemJogador.CompareTo(y.Jogador.OrdemJogador);
        }
    }
}
