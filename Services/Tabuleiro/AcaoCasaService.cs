using System.Globalization;
using Microsoft.EntityFrameworkCore;

using BancoImobiliario.Data;          // AppDbContext
using BancoImobiliario.Models.Enums;  // TipoCasa, RegraEspecialCompanhia
using BancoImobiliario.Models.Jogo;   // Tb_PartidaJogador, DadosCasaPopup, VisibilidadeCasa, FronteiraCasa
using BancoImobiliario.Models.Casas;  // PartidaTabuleiro, PartidaFronteira, Companhia
using BancoImobiliario.Models;        // Cidade, Efeito (ajuste se estiverem em outro namespace)
using BancoImobiliario.Models.DTOs.Jogo; // PropriedadeSelecionavelDTO

namespace BancoImobiliario.Services.Jogo
{
    public sealed record ResultadoCairNaCasa(
        TipoAcaoCasa Acao,
        string Mensagem,
        decimal? Valor = null,
        int? ProprietarioId = null,
        int PartidaTabuleiroId = 0,
        byte TipoCasaId = 0);

    public class AcaoCasaService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public AcaoCasaService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<ResultadoCairNaCasa> AoCairNaCasaAsync(int idPartida, int idPartidaJogador, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException(
                    $"Não há casa na posição {jogador.PosicaoAtual} da partida {idPartida}.");

            ResultadoCairNaCasa resultado;

            switch ((TipoCasa)casa.TipoCasaId)
            {
                case TipoCasa.Cidade:
                case TipoCasa.Companhia:
                    resultado = await ProcessarPropriedadeAsync(context, jogador, casa, cancellationToken);
                    break;

                case TipoCasa.Imposto:
                    casa.IsRevelada = true;
                    resultado = new ResultadoCairNaCasa(
                        TipoAcaoCasa.PagouImposto,
                        $"Caiu em {casa.Nome} (Imposto).",
                        PartidaTabuleiroId: casa.PartidaTabuleiroId,
                        TipoCasaId: casa.TipoCasaId);
                    break;

                case TipoCasa.Prisao:
                    casa.IsRevelada = true;
                    resultado = new ResultadoCairNaCasa(
                        TipoAcaoCasa.FoiPreso,
                        $"Caiu em {casa.Nome} (Prisão).",
                        PartidaTabuleiroId: casa.PartidaTabuleiroId,
                        TipoCasaId: casa.TipoCasaId);
                    break;

                case TipoCasa.Efeito:
                    casa.IsRevelada = true;
                    resultado = new ResultadoCairNaCasa(
                        TipoAcaoCasa.Efeito,
                        $"Caiu em {casa.Nome} (Efeito).",
                        PartidaTabuleiroId: casa.PartidaTabuleiroId,
                        TipoCasaId: casa.TipoCasaId);
                    break;

                case TipoCasa.Especial:
                    casa.IsRevelada = true;
                    resultado = ProcessarEspecial(casa);
                    break;

                default:
                    resultado = new ResultadoCairNaCasa(
                        TipoAcaoCasa.Nada,
                        "Casa sem ação definida.",
                        PartidaTabuleiroId: casa.PartidaTabuleiroId,
                        TipoCasaId: casa.TipoCasaId);
                    break;
            }

            casa.DataAtualizacao = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return resultado;
        }

        public async Task<ResultadoCairNaCasa> ComprarPropriedadeAsync(int idPartida, int idPartidaJogador, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException(
                    $"Não há casa na posição {jogador.PosicaoAtual} da partida {idPartida}.");

            var tipo = (TipoCasa)casa.TipoCasaId;
            if (tipo != TipoCasa.Cidade && tipo != TipoCasa.Companhia)
                throw new InvalidOperationException("Esta casa não é comprável.");

            if (casa.ProprietarioId > 0)
                throw new InvalidOperationException("Esta propriedade já possui dono.");

            var preco = casa.ValorCompraAtual ?? 0m;

            jogador.SaldoAtual -= preco;

            casa.ProprietarioId = jogador.IdPartidaJogador;
            casa.IsRevelada = true;
            casa.DataAtualizacao = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoCairNaCasa(
                TipoAcaoCasa.PodeConstruir,
                $"Você comprou {casa.Nome} por {Moeda(preco)}.",
                Valor: preco,
                ProprietarioId: jogador.IdPartidaJogador,
                PartidaTabuleiroId: casa.PartidaTabuleiroId,
                TipoCasaId: casa.TipoCasaId);
        }

        // ========================================
        // SIMULAÇÃO / MOVIMENTO (helper de teste)
        // ========================================

        public async Task DefinirPosicaoAsync(int idPartida, int idPartidaJogador, int posicao, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            jogador.PosicaoAtual = posicao;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<Tb_PartidaJogador?> ObterJogadorAsync(int idPartida, int idPartidaJogador, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
        }

        public async Task DefinirEstadoTurnoAsync(int idPartida, int idPartidaJogador, EstadoTurnoJogador estado, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            jogador.EstadoTurno = (int)estado;

            if (estado == EstadoTurnoJogador.ResolvendoCasa)
                jogador.PodeFinalizar = false;
            else if (estado == EstadoTurnoJogador.TurnoConcluido)
                jogador.PodeFinalizar = true;

            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ConcluirAcaoCasaAsync(int idPartida, int idPartidaJogador, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            jogador.EstadoTurno = (int)EstadoTurnoJogador.TurnoConcluido;
            jogador.PodeFinalizar = true;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);
        }

        // ============================================================
        // TURNO DO BOT (automático, animado) — com Cidade e Companhia normal.
        // ============================================================

        #region Turno do bot

        public sealed record ResultadoTurnoBot(
            int IdPartidaJogador,
            string NomeJogador,
            int ValorDado,
            int PosicaoAnterior,
            int PosicaoFinal,
            bool CompletouVolta,
            decimal BonusVolta,
            byte TipoCasaId,
            string NomeCasa,
            AcaoBot Acao,
            decimal Valor,
            bool Eliminado,
            string Mensagem);

        public enum AcaoBot
        {
            Nada,
            Comprou,
            PagouAluguel,
            NaoComprou,
            Eliminado
        }

        public async Task<ResultadoTurnoBot> JogarTurnoBotAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            if (jogador.TipoJogador != (int)TipoJogadorPartida.Bot)
                throw new InvalidOperationException("Este jogador não é um bot.");

            var configuracao = await context.Set<Configuracao>()
                .FirstOrDefaultAsync(c => c.IdConfiguracao ==
                    context.Set<Tb_Partida>().Where(p => p.IdPartida == idPartida).Select(p => p.IdConfiguracao).First(),
                    cancellationToken);

            if (configuracao is null)
                throw new InvalidOperationException("Configuração da partida não encontrada.");

            var totalCasas = await context.Set<PartidaTabuleiro>()
                .CountAsync(c => c.PartidaId == idPartida, cancellationToken);

            if (totalCasas <= 0)
                throw new InvalidOperationException("Tabuleiro da partida sem casas.");

            // ----- 1. Rola o dado (mesma regra/range da config) -----
            // Restrição de paridade por efeito guardado (dado só par/ímpar).
            var paridade = await ObterParidadeDadoAsync(context, idPartida, jogador.IdPartidaJogador, cancellationToken);
            var valorDado = SortearDado(configuracao, paridade);
            var posicaoAnterior = jogador.PosicaoAtual;

            int posicaoFinal;
            var completouVolta = false;
            var bonusVolta = 0m;

            if (posicaoAnterior <= 0)
            {
                if (valorDado <= 0)
                {
                    posicaoFinal = 0;
                }
                else
                {
                    posicaoFinal = valorDado;
                    if (posicaoFinal > totalCasas)
                    {
                        completouVolta = true;
                        posicaoFinal = ((valorDado - 1) % totalCasas) + 1;
                    }
                }
            }
            else
            {
                var soma = posicaoAnterior + valorDado;
                if (valorDado > 0 && soma > totalCasas)
                    completouVolta = true;

                var zeroBased = ((soma - 1) % totalCasas);
                if (zeroBased < 0)
                    zeroBased += totalCasas;

                posicaoFinal = zeroBased + 1;
            }

            if (completouVolta)
            {
                jogador.VoltasCompletadas += 1;

                // Efeito "sem bônus de volta": se ativo, não credita o bônus nesta volta.
                var semBonus = await TemEfeitoAtivoInternoAsync(
                    context, idPartida, jogador.IdPartidaJogador, EF_SEM_BONUS_VOLTA, cancellationToken);

                if (!semBonus)
                {
                    var ateVolta = configuracao.BonusAteVolta;
                    if (ateVolta <= 0 || jogador.VoltasCompletadas <= ateVolta)
                    {
                        bonusVolta = Convert.ToDecimal(configuracao.BonusVolta);
                        jogador.SaldoAtual += bonusVolta;
                    }
                }

                // Expira os efeitos guardados que valem só até a volta (RemoverAposVolta=1).
                await ExpirarEfeitosPorVoltaAsync(context, idPartida, jogador.IdPartidaJogador, cancellationToken);
            }

            jogador.PosicaoAtual = posicaoFinal;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            if (posicaoFinal <= 0)
            {
                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    0, "", AcaoBot.Nada, 0m, false,
                    $"{jogador.NomeJogador} não saiu da largada.");
            }

            // ----- 2. Resolve a casa onde caiu -----
            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == posicaoFinal, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa de destino não encontrada.");

            if (!casa.IsRevelada)
            {
                casa.IsRevelada = true;
                casa.DataAtualizacao = DateTime.Now;
                await context.SaveChangesAsync(cancellationToken);
            }

            var tipo = (TipoCasa)casa.TipoCasaId;

            // Bloqueio de compra (efeito guardado): se o bot caiu numa propriedade
            // comprável (Cidade/Companhia sem dono), o efeito tem prioridade — consome
            // e o bot NÃO compra. Identificado pela ColunaEfeito (genérico).
            if ((tipo == TipoCasa.Cidade || tipo == TipoCasa.Companhia)
                && casa.ProprietarioId <= 0)
            {
                var bloqueado = await ConsumirEfeitoBloqueioInternoAsync(
                    context, idPartida, jogador.IdPartidaJogador, cancellationToken);

                if (bloqueado)
                {
                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                        $"{jogador.NomeJogador} estava com a compra bloqueada e não pôde comprar {casa.Nome}.");
                }
            }

            // ----- Companhia normal: bot compra (até 60% do saldo) ou paga aluguel -----
            if (tipo == TipoCasa.Companhia)
            {
                var companhia = await context.Set<Companhia>()
                    .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

                var regra = companhia?.RegraEspecial ?? 0;

                // Internet: compra obrigatória (se tem saldo) + dado define o aluguel.
                if (regra == (int)RegraEspecialCompanhia.Internet)
                {
                    var semDonoNet = casa.ProprietarioId <= 0;
                    var ehDoBotNet = casa.ProprietarioId == jogador.IdPartidaJogador;

                    if (semDonoNet)
                    {
                        var precoNet = casa.ValorCompraAtual ?? 0m;
                        var fatorNet = companhia!.FatorMultiplicador ?? 0;

                        if (jogador.SaldoAtual >= precoNet)
                        {
                            // compra
                            jogador.SaldoAtual -= precoNet;
                            casa.ProprietarioId = jogador.IdPartidaJogador;
                            casa.IsRevelada = true;
                            casa.ValorCompraAtual = precoNet;
                            casa.QtdCasas = 0;
                            casa.QtdHoteis = 0;

                            // rola o dado do aluguel (server-side)
                            var dadoAluguel = SortearDado(configuracao);
                            var dadoPos = dadoAluguel < 0 ? 0 : dadoAluguel;
                            var aluguelNet = dadoPos * (decimal)fatorNet;
                            casa.ValorAluguelAtual = aluguelNet;

                            casa.DataAtualizacao = DateTime.Now;
                            jogador.DataAtualizacao = DateTime.Now;
                            await context.SaveChangesAsync(cancellationToken);

                            return new ResultadoTurnoBot(
                                jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                                posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                                casa.TipoCasaId, casa.Nome, AcaoBot.Comprou, precoNet, false,
                                $"{jogador.NomeJogador} comprou {casa.Nome} por R$ {precoNet:N2} (dado {dadoPos} → aluguel R$ {aluguelNet:N2}).");
                        }

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.NaoComprou, 0m, false,
                            $"{jogador.NomeJogador} não tinha saldo para {casa.Nome} (compra pulada).");
                    }

                    if (ehDoBotNet)
                    {
                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                            $"{jogador.NomeJogador} parou na própria companhia ({casa.Nome}).");
                    }

                    // Internet de outro dono: paga aluguel fixo (já definido na compra).
                    var aluguelNetPagar = casa.ValorAluguelAtual ?? 0m;
                    var donoNet = await context.Set<Tb_PartidaJogador>()
                        .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId && j.IdPartida == idPartida, cancellationToken);

                    if (jogador.SaldoAtual >= aluguelNetPagar || configuracao.PermitirSaldoNegativo)
                    {
                        jogador.SaldoAtual -= aluguelNetPagar;
                        if (donoNet is not null) donoNet.SaldoAtual += aluguelNetPagar;
                        jogador.DataAtualizacao = DateTime.Now;
                        await context.SaveChangesAsync(cancellationToken);

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelNetPagar, false,
                            $"{jogador.NomeJogador} pagou R$ {aluguelNetPagar:N2} de aluguel em {casa.Nome}.");
                    }

                    var potNet = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);
                    if (jogador.SaldoAtual + potNet >= aluguelNetPagar)
                    {
                        jogador.SaldoAtual -= aluguelNetPagar;
                        if (donoNet is not null) donoNet.SaldoAtual += aluguelNetPagar;
                        jogador.DataAtualizacao = DateTime.Now;
                        await context.SaveChangesAsync(cancellationToken);

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelNetPagar, false,
                            $"{jogador.NomeJogador} pagou R$ {aluguelNetPagar:N2} de aluguel em {casa.Nome}.");
                    }

                    var transfNet = jogador.SaldoAtual;
                    if (donoNet is not null && transfNet > 0) donoNet.SaldoAtual += transfNet;
                    jogador.SaldoAtual = 0m;
                    await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Eliminado, aluguelNetPagar, true,
                        $"{jogador.NomeJogador} não pagou o aluguel de {casa.Nome} e foi eliminado.");
                }

                // Business: adquire grátis se for dono de todas as outras companhias.
                if (regra == (int)RegraEspecialCompanhia.Business)
                {
                    var semDonoBiz = casa.ProprietarioId <= 0;
                    var ehDoBotBiz = casa.ProprietarioId == jogador.IdPartidaJogador;

                    if (semDonoBiz)
                    {
                        var temTodas = await JogadorTemTodasOutrasCompanhiasAsync(
                            context, idPartida, jogador.IdPartidaJogador, companhia!.CompanhiaId, cancellationToken);

                        if (temTodas)
                        {
                            var soma = await SomarAluguelOutrasCompanhiasAsync(context, idPartida, companhia.CompanhiaId, cancellationToken);

                            casa.ProprietarioId = jogador.IdPartidaJogador;
                            casa.IsRevelada = true;
                            casa.ValorCompraAtual = 0m;
                            casa.ValorAluguelAtual = soma;
                            casa.QtdCasas = 0;
                            casa.QtdHoteis = 0;
                            casa.DataAtualizacao = DateTime.Now;
                            await context.SaveChangesAsync(cancellationToken);

                            return new ResultadoTurnoBot(
                                jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                                posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                                casa.TipoCasaId, casa.Nome, AcaoBot.Comprou, 0m, false,
                                $"{jogador.NomeJogador} adquiriu {casa.Nome}! Aluguel R$ {soma:N2}.");
                        }

                        // Não tem todas: sem ação.
                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                            $"{jogador.NomeJogador} caiu em {casa.Nome} (indisponível).");
                    }

                    if (ehDoBotBiz)
                    {
                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                            $"{jogador.NomeJogador} parou na própria companhia ({casa.Nome}).");
                    }

                    // Business de outro dono: paga o aluguel fixo (soma das outras na época).
                    var aluguelBiz = casa.ValorAluguelAtual ?? 0m;
                    var donoBiz = await context.Set<Tb_PartidaJogador>()
                        .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId && j.IdPartida == idPartida, cancellationToken);

                    if (jogador.SaldoAtual >= aluguelBiz || configuracao.PermitirSaldoNegativo)
                    {
                        jogador.SaldoAtual -= aluguelBiz;
                        if (donoBiz is not null) donoBiz.SaldoAtual += aluguelBiz;
                        jogador.DataAtualizacao = DateTime.Now;
                        await context.SaveChangesAsync(cancellationToken);

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelBiz, false,
                            $"{jogador.NomeJogador} pagou R$ {aluguelBiz:N2} de aluguel em {casa.Nome}.");
                    }

                    var potBiz = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);
                    if (jogador.SaldoAtual + potBiz >= aluguelBiz)
                    {
                        jogador.SaldoAtual -= aluguelBiz;
                        if (donoBiz is not null) donoBiz.SaldoAtual += aluguelBiz;
                        jogador.DataAtualizacao = DateTime.Now;
                        await context.SaveChangesAsync(cancellationToken);

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelBiz, false,
                            $"{jogador.NomeJogador} pagou R$ {aluguelBiz:N2} de aluguel em {casa.Nome}.");
                    }

                    var transfBiz = jogador.SaldoAtual;
                    if (donoBiz is not null && transfBiz > 0) donoBiz.SaldoAtual += transfBiz;
                    jogador.SaldoAtual = 0m;
                    await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Eliminado, aluguelBiz, true,
                        $"{jogador.NomeJogador} não pagou o aluguel de {casa.Nome} e foi eliminado.");
                }

                // Xerox: compra (se tem saldo + há copiável) e copia o maior aluguel.
                if (regra == (int)RegraEspecialCompanhia.Xerox)
                {
                    var semDonoX = casa.ProprietarioId <= 0;
                    var ehDoBotX = casa.ProprietarioId == jogador.IdPartidaJogador;

                    if (semDonoX)
                    {
                        var precoX = casa.ValorCompraAtual ?? 0m;
                        var copiaveis = await ListarCopiaveisXeroxAsync(idPartida, casa.Posicao, cancellationToken);

                        if (jogador.SaldoAtual >= precoX && copiaveis.Count > 0)
                        {
                            // escolhe a de maior aluguel (ganancioso)
                            var alvo = copiaveis.OrderByDescending(p => p.ValorAluguel).First();

                            jogador.SaldoAtual -= precoX;
                            casa.ProprietarioId = jogador.IdPartidaJogador;
                            casa.IsRevelada = true;
                            casa.ValorCompraAtual = precoX;
                            casa.ValorAluguelAtual = alvo.ValorAluguel; // copia fixo
                            casa.QtdCasas = 0;
                            casa.QtdHoteis = 0;
                            casa.DataAtualizacao = DateTime.Now;
                            jogador.DataAtualizacao = DateTime.Now;
                            await context.SaveChangesAsync(cancellationToken);

                            return new ResultadoTurnoBot(
                                jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                                posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                                casa.TipoCasaId, casa.Nome, AcaoBot.Comprou, precoX, false,
                                $"{jogador.NomeJogador} comprou {casa.Nome} e copiou o aluguel de {alvo.Nome} (R$ {alvo.ValorAluguel:N2}).");
                        }

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.NaoComprou, 0m, false,
                            $"{jogador.NomeJogador} não comprou {casa.Nome}.");
                    }

                    if (ehDoBotX)
                    {
                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                            $"{jogador.NomeJogador} parou na própria companhia ({casa.Nome}).");
                    }

                    // Xerox de outro dono: paga o aluguel copiado.
                    var aluguelX = casa.ValorAluguelAtual ?? 0m;
                    var donoX = await context.Set<Tb_PartidaJogador>()
                        .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId && j.IdPartida == idPartida, cancellationToken);

                    if (jogador.SaldoAtual >= aluguelX || configuracao.PermitirSaldoNegativo)
                    {
                        jogador.SaldoAtual -= aluguelX;
                        if (donoX is not null) donoX.SaldoAtual += aluguelX;
                        jogador.DataAtualizacao = DateTime.Now;
                        await context.SaveChangesAsync(cancellationToken);

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelX, false,
                            $"{jogador.NomeJogador} pagou R$ {aluguelX:N2} de aluguel em {casa.Nome}.");
                    }

                    var potX = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);
                    if (jogador.SaldoAtual + potX >= aluguelX)
                    {
                        jogador.SaldoAtual -= aluguelX;
                        if (donoX is not null) donoX.SaldoAtual += aluguelX;
                        jogador.DataAtualizacao = DateTime.Now;
                        await context.SaveChangesAsync(cancellationToken);

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelX, false,
                            $"{jogador.NomeJogador} pagou R$ {aluguelX:N2} de aluguel em {casa.Nome}.");
                    }

                    var transfX = jogador.SaldoAtual;
                    if (donoX is not null && transfX > 0) donoX.SaldoAtual += transfX;
                    jogador.SaldoAtual = 0m;
                    await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Eliminado, aluguelX, true,
                        $"{jogador.NomeJogador} não pagou o aluguel de {casa.Nome} e foi eliminado.");
                }

                // Demais especiais não tratadas para o bot.
                if (regra != (int)RegraEspecialCompanhia.Normal)
                {
                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                        $"{jogador.NomeJogador} caiu em {casa.Nome}.");
                }

                var semDonoComp = casa.ProprietarioId <= 0;
                var ehDoBotComp = casa.ProprietarioId == jogador.IdPartidaJogador;

                if (semDonoComp)
                {
                    var valorPago = DecidirValorCompraCompanhiaBot(jogador.SaldoAtual);

                    if (valorPago >= 1m && valorPago <= jogador.SaldoAtual)
                    {
                        var porc = companhia!.PorcValor ?? 0m;
                        var aluguelComp = Math.Round(valorPago * (porc / 100m), 2, MidpointRounding.AwayFromZero);

                        jogador.SaldoAtual -= valorPago;
                        casa.ProprietarioId = jogador.IdPartidaJogador;
                        casa.IsRevelada = true;
                        casa.ValorCompraAtual = valorPago;
                        casa.ValorAluguelAtual = aluguelComp;
                        casa.QtdCasas = 0;
                        casa.QtdHoteis = 0;
                        casa.DataAtualizacao = DateTime.Now;
                        jogador.DataAtualizacao = DateTime.Now;
                        await context.SaveChangesAsync(cancellationToken);

                        return new ResultadoTurnoBot(
                            jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                            posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                            casa.TipoCasaId, casa.Nome, AcaoBot.Comprou, valorPago, false,
                            $"{jogador.NomeJogador} comprou {casa.Nome} por R$ {valorPago:N2} (aluguel R$ {aluguelComp:N2}).");
                    }

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.NaoComprou, 0m, false,
                        $"{jogador.NomeJogador} não comprou {casa.Nome}.");
                }

                if (ehDoBotComp)
                {
                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                        $"{jogador.NomeJogador} parou na própria companhia ({casa.Nome}).");
                }

                var aluguelPagar = casa.ValorAluguelAtual ?? 0m;
                var donoComp = await context.Set<Tb_PartidaJogador>()
                    .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId && j.IdPartida == idPartida, cancellationToken);

                if (jogador.SaldoAtual >= aluguelPagar || configuracao.PermitirSaldoNegativo)
                {
                    jogador.SaldoAtual -= aluguelPagar;
                    if (donoComp is not null) donoComp.SaldoAtual += aluguelPagar;
                    jogador.DataAtualizacao = DateTime.Now;
                    await context.SaveChangesAsync(cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelPagar, false,
                        $"{jogador.NomeJogador} pagou R$ {aluguelPagar:N2} de aluguel em {casa.Nome}.");
                }

                var potencialComp = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);
                if (jogador.SaldoAtual + potencialComp >= aluguelPagar)
                {
                    jogador.SaldoAtual -= aluguelPagar;
                    if (donoComp is not null) donoComp.SaldoAtual += aluguelPagar;
                    jogador.DataAtualizacao = DateTime.Now;
                    await context.SaveChangesAsync(cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguelPagar, false,
                        $"{jogador.NomeJogador} pagou R$ {aluguelPagar:N2} de aluguel em {casa.Nome}.");
                }

                var transferidoComp = jogador.SaldoAtual;
                if (donoComp is not null && transferidoComp > 0)
                    donoComp.SaldoAtual += transferidoComp;
                jogador.SaldoAtual = 0m;
                await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);

                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.Eliminado, aluguelPagar, true,
                    $"{jogador.NomeJogador} não pagou o aluguel de {casa.Nome} e foi eliminado.");
            }

            // ----- Imposto: bot resolve e paga -----
            if (tipo == TipoCasa.Imposto)
            {
                // imunidade a imposto: consome e não paga
                if (await ConsumirEfeitoInternoAsync(context, idPartida, jogador.IdPartidaJogador, EF_IMUNIDADE_IMPOSTOS, cancellationToken))
                {
                    casa.IsRevelada = true;
                    casa.DataAtualizacao = DateTime.Now;
                    await context.SaveChangesAsync(cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                        $"{jogador.NomeJogador} estava imune e não pagou o imposto.");
                }
                var (aleatorios, naoRepetir) = LerConfigEfeitos(configuracao);
                var idsImp = await context.Set<Imposto>().Select(i => i.ImpostoId).ToListAsync(cancellationToken);
                var impId = await SortearCatalogoAsync(context, idPartida, CAT_IMPOSTO, casa.ReferenciaCatalogoId, idsImp, aleatorios, naoRepetir, cancellationToken);
                var imp = await context.Set<Imposto>().FirstOrDefaultAsync(i => i.ImpostoId == impId, cancellationToken);

                casa.IsRevelada = true;
                casa.DataAtualizacao = DateTime.Now;

                decimal valorImp;
                if (imp is null)
                    valorImp = 0m;
                else if (imp.ValorDependeDado)
                {
                    var d = SortearDado(configuracao);
                    valorImp = (d < 0 ? 0 : d) * (decimal)imp.FatorMultiplicador;
                }
                else if (imp.TipoValorId == 1)
                    valorImp = Math.Round(jogador.SaldoAtual * (imp.Valor / 100m), 2, MidpointRounding.AwayFromZero);
                else
                    valorImp = imp.Valor;

                if (jogador.SaldoAtual >= valorImp || configuracao.PermitirSaldoNegativo)
                {
                    jogador.SaldoAtual -= valorImp;
                    jogador.DataAtualizacao = DateTime.Now;
                    await context.SaveChangesAsync(cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, valorImp, false,
                        $"{jogador.NomeJogador} pagou R$ {valorImp:N2} de imposto.");
                }

                var potImp = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);
                if (jogador.SaldoAtual + potImp >= valorImp)
                {
                    jogador.SaldoAtual -= valorImp;
                    jogador.DataAtualizacao = DateTime.Now;
                    await context.SaveChangesAsync(cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, valorImp, false,
                        $"{jogador.NomeJogador} pagou R$ {valorImp:N2} de imposto.");
                }

                jogador.SaldoAtual = 0m;
                await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);
                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.Eliminado, valorImp, true,
                    $"{jogador.NomeJogador} não pagou o imposto e foi eliminado.");
            }

            // ----- Prisão: bot fica preso -----
            if (tipo == TipoCasa.Prisao)
            {
                // imunidade a prisão: consome e não prende
                if (await ConsumirEfeitoInternoAsync(context, idPartida, jogador.IdPartidaJogador, EF_IMUNIDADE_PRISAO, cancellationToken))
                {
                    casa.IsRevelada = true;
                    casa.DataAtualizacao = DateTime.Now;
                    await context.SaveChangesAsync(cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                        $"{jogador.NomeJogador} estava imune e escapou da prisão!");
                }
                var (aleatorios, naoRepetir) = LerConfigEfeitos(configuracao);
                var idsPri = await context.Set<Prisao>().Select(p => p.PrisaoId).ToListAsync(cancellationToken);
                var priId = await SortearCatalogoAsync(context, idPartida, CAT_PRISAO, casa.ReferenciaCatalogoId, idsPri, aleatorios, naoRepetir, cancellationToken);
                var pri = await context.Set<Prisao>().FirstOrDefaultAsync(p => p.PrisaoId == priId, cancellationToken);

                int rodadas;
                if (pri is null)
                    rodadas = 0;
                else if (pri.ValorDependeDado)
                {
                    var d = SortearDado(configuracao);
                    rodadas = d < 0 ? 0 : d;
                }
                else
                    rodadas = pri.QtdRodadas;

                casa.IsRevelada = true;
                casa.DataAtualizacao = DateTime.Now;
                jogador.TurnosPreso = rodadas;
                jogador.DataAtualizacao = DateTime.Now;
                await context.SaveChangesAsync(cancellationToken);

                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                    $"{jogador.NomeJogador} foi preso por {rodadas} rodada(s).");
            }

            // ----- Outros tipos (Efeito/Especial): nada por enquanto -----
            if (tipo != TipoCasa.Cidade)
            {
                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                    $"{jogador.NomeJogador} caiu em {casa.Nome}.");
            }

            // ----- Cidade -----
            var semDono = casa.ProprietarioId <= 0;
            var ehDoBot = casa.ProprietarioId == jogador.IdPartidaJogador;

            if (semDono)
            {
                var preco = casa.ValorCompraAtual ?? 0m;

                if (jogador.SaldoAtual >= preco)
                {
                    casa.ProprietarioId = jogador.IdPartidaJogador;
                    casa.IsRevelada = true;
                    jogador.SaldoAtual -= preco;
                    casa.DataAtualizacao = DateTime.Now;
                    jogador.DataAtualizacao = DateTime.Now;
                    await context.SaveChangesAsync(cancellationToken);

                    return new ResultadoTurnoBot(
                        jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                        posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                        casa.TipoCasaId, casa.Nome, AcaoBot.Comprou, preco, false,
                        $"{jogador.NomeJogador} comprou {casa.Nome} por R$ {preco:N2}.");
                }

                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.NaoComprou, preco, false,
                    $"{jogador.NomeJogador} não comprou {casa.Nome} (saldo insuficiente).");
            }

            if (ehDoBot)
            {
                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.Nada, 0m, false,
                    $"{jogador.NomeJogador} parou na própria propriedade ({casa.Nome}).");
            }

            var aluguel = casa.ValorAluguelAtual ?? 0m;
            var dono = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId && j.IdPartida == idPartida, cancellationToken);

            if (jogador.SaldoAtual >= aluguel || configuracao.PermitirSaldoNegativo)
            {
                jogador.SaldoAtual -= aluguel;
                if (dono is not null) dono.SaldoAtual += aluguel;
                jogador.DataAtualizacao = DateTime.Now;
                await context.SaveChangesAsync(cancellationToken);

                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguel, false,
                    $"{jogador.NomeJogador} pagou R$ {aluguel:N2} de aluguel em {casa.Nome}.");
            }

            var potencialVenda = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);

            if (jogador.SaldoAtual + potencialVenda >= aluguel)
            {
                jogador.SaldoAtual -= aluguel;
                if (dono is not null) dono.SaldoAtual += aluguel;
                jogador.DataAtualizacao = DateTime.Now;
                await context.SaveChangesAsync(cancellationToken);

                return new ResultadoTurnoBot(
                    jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                    posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                    casa.TipoCasaId, casa.Nome, AcaoBot.PagouAluguel, aluguel, false,
                    $"{jogador.NomeJogador} pagou R$ {aluguel:N2} de aluguel em {casa.Nome}.");
            }

            var transferido = jogador.SaldoAtual;
            if (dono is not null && transferido > 0)
                dono.SaldoAtual += transferido;

            jogador.SaldoAtual = 0m;
            await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);

            return new ResultadoTurnoBot(
                jogador.IdPartidaJogador, jogador.NomeJogador, valorDado,
                posicaoAnterior, posicaoFinal, completouVolta, bonusVolta,
                casa.TipoCasaId, casa.Nome, AcaoBot.Eliminado, aluguel, true,
                $"{jogador.NomeJogador} não conseguiu pagar o aluguel e foi eliminado.");
        }

        #endregion

        // ========================================
        // MONTAGEM DO DTO DO PAINEL DA CASA
        // ========================================

        public async Task<DadosCasaPopup> MontarDadosCasaAsync(int idPartida, int posicao, int idJogadorDaVez, bool debug = false, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == posicao, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException(
                    $"Não há casa na posição {posicao} da partida {idPartida}.");

            var jogadorDaVez = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idJogadorDaVez && j.IdPartida == idPartida, cancellationToken);

            var temDono = casa.ProprietarioId > 0;
            var tipo = (TipoCasa)casa.TipoCasaId;
            var ehPropriedade = tipo == TipoCasa.Cidade || tipo == TipoCasa.Companhia;
            var jogadorEstaNaCasa = jogadorDaVez is not null && jogadorDaVez.PosicaoAtual == casa.Posicao;
            var ehDonoJogadorDaVez = temDono && jogadorDaVez is not null && casa.ProprietarioId == jogadorDaVez.IdPartidaJogador;

            // ----- Regra de visibilidade -----
            VisibilidadeCasa visibilidade;
            if (casa.IsRevelada || debug)
                visibilidade = VisibilidadeCasa.Completa;
            else if (ehPropriedade && !temDono && jogadorEstaNaCasa)
                visibilidade = VisibilidadeCasa.SomentePreco;
            else
                visibilidade = VisibilidadeCasa.Oculta;

            // ----- Nome do proprietário -----
            var nomeProprietario = "Banco";
            if (temDono)
            {
                var dono = await context.Set<Tb_PartidaJogador>()
                    .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId && j.IdPartida == idPartida, cancellationToken);
                if (dono is not null)
                    nomeProprietario = dono.NomeJogador;
            }

            // ----- Custos de construção (SOMENTE Cidade, vindos do catálogo, fixos) -----
            decimal? custoCasa = null;
            decimal? custoHotel = null;
            if (tipo == TipoCasa.Cidade)
            {
                var cidade = await context.Set<Cidade>()
                    .FirstOrDefaultAsync(c => c.CidadeId == casa.ReferenciaCatalogoId, cancellationToken);
                if (cidade is not null)
                {
                    custoCasa = cidade.ValorAddCasa;
                    custoHotel = cidade.ValorAddHotel;
                }
            }

            // ----- Descrição do efeito do grupo (Efeito.Frase via GrupoId) -----
            string? descricaoEfeitoGrupo = null;
            if (casa.GrupoId is not null)
            {
                var efeitoGrupo = await context.Set<Efeito>()
                    .FirstOrDefaultAsync(e => e.EfeitoId == casa.GrupoId.Value, cancellationToken);
                descricaoEfeitoGrupo = efeitoGrupo?.Frase;
            }

            var dados = new DadosCasaPopup
            {
                PartidaTabuleiroId = casa.PartidaTabuleiroId,
                Posicao = casa.Posicao,
                TipoCasaId = casa.TipoCasaId,
                Visibilidade = visibilidade,
                Nome = casa.Nome,
                Imagem = casa.Imagem,
                CorHexadecimal = casa.CorHexadecimal,
                ProprietarioId = temDono ? casa.ProprietarioId : (int?)null,
                NomeProprietario = nomeProprietario,
                ValorCompra = casa.ValorCompraAtual,
                ValorAluguel = casa.ValorAluguelAtual,
                CustoCasa = custoCasa,
                CustoHotel = custoHotel,
                QtdCasas = casa.QtdCasas,
                QtdHoteis = casa.QtdHoteis,
                EhPropriedade = ehPropriedade,
                EhDonoJogadorDaVez = ehDonoJogadorDaVez,
                PodeOferecerCompra = ehPropriedade && !temDono && jogadorEstaNaCasa,
                GrupoId = casa.GrupoId,
                CorGrupo = casa.GrupoId is not null ? casa.CorHexadecimal : null,
                DescricaoEfeitoGrupo = descricaoEfeitoGrupo,
            };

            // ----- Fronteiras (direcional: PosicaoOrigem == número da casa) -----
            var fronteiras = await context.Set<PartidaFronteira>()
                .Where(f => f.IdPartida == idPartida && f.PosicaoOrigem == casa.Posicao)
                .ToListAsync(cancellationToken);

            foreach (var f in fronteiras)
            {
                string? descEfeito = null;
                if (f.EfeitoRequeridoId is not null)
                {
                    var efeito = await context.Set<Efeito>()
                        .FirstOrDefaultAsync(e => e.EfeitoId == f.EfeitoRequeridoId.Value, cancellationToken);
                    descEfeito = efeito?.Frase;
                }

                dados.Fronteiras.Add(new FronteiraCasa
                {
                    PosicaoDestino = f.PosicaoDestino,
                    ValorTravessia = f.ValorTravessia,
                    EfeitoRequeridoId = f.EfeitoRequeridoId,
                    DescricaoEfeitoRequerido = descEfeito,
                });
            }

            return dados;
        }

        // ========================================
        // PROPRIEDADE (Cidade / Companhia) — completo
        // ========================================

        private async Task<ResultadoCairNaCasa> ProcessarPropriedadeAsync(
            AppDbContext context, Tb_PartidaJogador jogador, PartidaTabuleiro casa, CancellationToken cancellationToken)
        {
            var temDono = casa.ProprietarioId > 0;
            var tipo = (TipoCasa)casa.TipoCasaId;

            if (!temDono)
            {
                // Companhia: a compra depende da regra especial.
                if (tipo == TipoCasa.Companhia)
                {
                    var companhia = await context.Set<Companhia>()
                        .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

                    var regra = companhia?.RegraEspecial ?? 0;

                    switch (regra)
                    {
                        case (int)RegraEspecialCompanhia.Normal:
                            return new ResultadoCairNaCasa(
                                TipoAcaoCasa.PodeComprarCompanhia,
                                $"{casa.Nome} está à venda. Escolha quanto pagar.",
                                PartidaTabuleiroId: casa.PartidaTabuleiroId,
                                TipoCasaId: casa.TipoCasaId);

                        case (int)RegraEspecialCompanhia.Internet:
                            return new ResultadoCairNaCasa(
                                TipoAcaoCasa.PodeComprarInternet,
                                $"{casa.Nome}: compra obrigatória.",
                                Valor: casa.ValorCompraAtual,
                                PartidaTabuleiroId: casa.PartidaTabuleiroId,
                                TipoCasaId: casa.TipoCasaId);

                        case (int)RegraEspecialCompanhia.Business:
                            {
                                // Só pode adquirir se for dono de todas as outras companhias.
                                var temTodas = await JogadorTemTodasOutrasCompanhiasAsync(
                                    context, jogador.IdPartida, jogador.IdPartidaJogador, companhia!.CompanhiaId, cancellationToken);

                                if (temTodas)
                                {
                                    return new ResultadoCairNaCasa(
                                        TipoAcaoCasa.PodeComprarBusiness,
                                        $"{casa.Nome}: você pode adquirir (dono de todas as outras companhias).",
                                        PartidaTabuleiroId: casa.PartidaTabuleiroId,
                                        TipoCasaId: casa.TipoCasaId);
                                }

                                // Sem o pré-requisito: nenhuma ação.
                                return new ResultadoCairNaCasa(
                                    TipoAcaoCasa.Nada,
                                    $"{casa.Nome}: indisponível (requer todas as outras companhias).",
                                    PartidaTabuleiroId: casa.PartidaTabuleiroId,
                                    TipoCasaId: casa.TipoCasaId);
                            }

                        case (int)RegraEspecialCompanhia.Xerox:
                            {
                                // Só pode comprar se houver ao menos uma propriedade copiável.
                                var copiaveis = await ListarCopiaveisXeroxAsync(jogador.IdPartida, casa.Posicao, cancellationToken);

                                if (copiaveis.Count > 0)
                                {
                                    return new ResultadoCairNaCasa(
                                        TipoAcaoCasa.PodeComprarXerox,
                                        $"{casa.Nome}: você pode comprar e copiar o aluguel de outra propriedade.",
                                        Valor: casa.ValorCompraAtual,
                                        PartidaTabuleiroId: casa.PartidaTabuleiroId,
                                        TipoCasaId: casa.TipoCasaId);
                                }

                                // Nada para copiar: indisponível.
                                return new ResultadoCairNaCasa(
                                    TipoAcaoCasa.Nada,
                                    $"{casa.Nome}: indisponível (não há propriedade para copiar).",
                                    PartidaTabuleiroId: casa.PartidaTabuleiroId,
                                    TipoCasaId: casa.TipoCasaId);
                            }

                        default:
                            return new ResultadoCairNaCasa(
                                TipoAcaoCasa.ADefinir,
                                $"{casa.Nome} (regra especial). Tratamento a definir.",
                                PartidaTabuleiroId: casa.PartidaTabuleiroId,
                                TipoCasaId: casa.TipoCasaId);
                    }
                }

                // Cidade sem dono: preço fixo (fluxo atual).
                return new ResultadoCairNaCasa(
                    TipoAcaoCasa.PodeComprar,
                    $"{casa.Nome} está à venda por {Moeda(casa.ValorCompraAtual)}.",
                    Valor: casa.ValorCompraAtual,
                    PartidaTabuleiroId: casa.PartidaTabuleiroId,
                    TipoCasaId: casa.TipoCasaId);
            }

            if (casa.ProprietarioId == jogador.IdPartidaJogador)
            {
                if (tipo == TipoCasa.Companhia)
                {
                    return new ResultadoCairNaCasa(
                        TipoAcaoCasa.Nada,
                        $"Você é o dono de {casa.Nome}.",
                        ProprietarioId: jogador.IdPartidaJogador,
                        PartidaTabuleiroId: casa.PartidaTabuleiroId,
                        TipoCasaId: casa.TipoCasaId);
                }

                return new ResultadoCairNaCasa(
                    TipoAcaoCasa.PodeConstruir,
                    $"Você é o dono de {casa.Nome}. Pode construir casa/hotel.",
                    ProprietarioId: jogador.IdPartidaJogador,
                    PartidaTabuleiroId: casa.PartidaTabuleiroId,
                    TipoCasaId: casa.TipoCasaId);
            }

            var dono = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId
                                          && j.IdPartida == jogador.IdPartida, cancellationToken);

            var aluguel = casa.ValorAluguelAtual ?? 0m;

            jogador.SaldoAtual -= aluguel;

            if (dono is not null)
            {
                dono.SaldoAtual += aluguel;
                dono.DataAtualizacao = DateTime.Now;
            }

            return new ResultadoCairNaCasa(
                TipoAcaoCasa.PagouAluguel,
                $"Você pagou {Moeda(aluguel)} de aluguel" +
                    (dono is not null ? $" a {dono.NomeJogador}." : "."),
                Valor: aluguel,
                ProprietarioId: casa.ProprietarioId,
                PartidaTabuleiroId: casa.PartidaTabuleiroId,
                TipoCasaId: casa.TipoCasaId);
        }

        // ========================================
        // TIPOS QUE DEPENDEM DE REGRAS AINDA NÃO DEFINIDAS
        // ========================================

        private ResultadoCairNaCasa ProcessarEspecial(PartidaTabuleiro casa)
            => new ResultadoCairNaCasa(TipoAcaoCasa.ADefinir,
                $"Caiu em {casa.Nome} (Especial). Regra a definir.",
                PartidaTabuleiroId: casa.PartidaTabuleiroId, TipoCasaId: casa.TipoCasaId);

        // ========================================
        // HELPERS
        // ========================================

        private static string Moeda(decimal? valor)
            => (valor ?? 0m).ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

        // ============================================================
        // MOTOR DE TURNO — Parte 1 (Cidade).
        // ============================================================

        #region Motor de turno

        public sealed record ResultadoTurno(
            int ValorDado,
            int PosicaoAnterior,
            int PosicaoFinal,
            bool CompletouVolta,
            decimal BonusVolta,
            ResultadoCairNaCasa Casa);

        public async Task<ResultadoTurno> RolarDadoTurnoAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            if (jogador.EstadoTurno != (int)EstadoTurnoJogador.AguardandoDado)
                throw new InvalidOperationException("Não é o momento de rolar o dado.");

            var configuracao = await context.Set<Configuracao>()
                .FirstOrDefaultAsync(c => c.IdConfiguracao ==
                    context.Set<Tb_Partida>().Where(p => p.IdPartida == idPartida).Select(p => p.IdConfiguracao).First(),
                    cancellationToken);

            if (configuracao is null)
                throw new InvalidOperationException("Configuração da partida não encontrada.");

            var totalCasas = await context.Set<PartidaTabuleiro>()
                .CountAsync(c => c.PartidaId == idPartida, cancellationToken);

            if (totalCasas <= 0)
                throw new InvalidOperationException("Tabuleiro da partida sem casas.");

            var paridadeBot = await ObterParidadeDadoAsync(context, idPartida, jogador.IdPartidaJogador, cancellationToken);
            var valorDado = SortearDado(configuracao, paridadeBot);

            var posicaoAnterior = jogador.PosicaoAtual; // 0 = fora do tabuleiro (largada)

            int posicaoFinal;
            var completouVolta = false;
            var bonusVolta = 0m;

            if (posicaoAnterior <= 0)
            {
                if (valorDado <= 0)
                {
                    posicaoFinal = 0;
                }
                else
                {
                    posicaoFinal = valorDado;
                    if (posicaoFinal > totalCasas)
                    {
                        completouVolta = true;
                        posicaoFinal = ((valorDado - 1) % totalCasas) + 1;
                    }
                }
            }
            else
            {
                var soma = posicaoAnterior + valorDado;

                if (valorDado > 0 && soma > totalCasas)
                    completouVolta = true;

                var zeroBased = ((soma - 1) % totalCasas);
                if (zeroBased < 0)
                    zeroBased += totalCasas;

                posicaoFinal = zeroBased + 1;
            }

            if (completouVolta)
            {
                jogador.VoltasCompletadas += 1;

                var semBonusBot = await TemEfeitoAtivoInternoAsync(
                    context, idPartida, jogador.IdPartidaJogador, EF_SEM_BONUS_VOLTA, cancellationToken);

                if (!semBonusBot)
                {
                    var ateVolta = configuracao.BonusAteVolta;
                    if (ateVolta <= 0 || jogador.VoltasCompletadas <= ateVolta)
                    {
                        bonusVolta = Convert.ToDecimal(configuracao.BonusVolta);
                        jogador.SaldoAtual += bonusVolta;
                    }
                }

                await ExpirarEfeitosPorVoltaAsync(context, idPartida, jogador.IdPartidaJogador, cancellationToken);
            }

            jogador.PosicaoAtual = posicaoFinal;
            jogador.EstadoTurno = (int)EstadoTurnoJogador.ResolvendoCasa;
            jogador.PodeFinalizar = false;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            var resultadoCasa = await AoCairNaCasaAsync(idPartida, idPartidaJogador, cancellationToken);

            if (!AcaoGeraPendencia(resultadoCasa.Acao))
                await ConcluirAcaoCasaAsync(idPartida, idPartidaJogador, cancellationToken);

            return new ResultadoTurno(
                valorDado,
                posicaoAnterior,
                posicaoFinal,
                completouVolta,
                bonusVolta,
                resultadoCasa);
        }

        private static bool AcaoGeraPendencia(TipoAcaoCasa acao)
        {
            switch (acao)
            {
                case TipoAcaoCasa.PagouAluguel:
                    return true;

                default:
                    return false;
            }
        }

        private static int SortearDado(Configuracao configuracao)
        {
            return SortearDado(configuracao, 0);
        }

        /// <summary>
        /// Sorteia o dado. paridade: 0 = qualquer; 1 = só ímpar; 2 = só par.
        /// Se a restrição de paridade não deixar nenhum valor válido, ela é ignorada.
        /// </summary>
        private static int SortearDado(Configuracao configuracao, int paridade)
        {
            var minimo = configuracao.ValMinDado;
            var maximo = configuracao.ValMaxDado;

            if (minimo == 0 && maximo == 0)
            {
                minimo = 1;
                maximo = 6;
            }

            if (maximo < minimo)
                maximo = minimo;

            var restritos = ParsearRestricoes(configuracao.RestricaoDado);

            var validos = new List<int>();
            for (var v = minimo; v <= maximo; v++)
            {
                if (!restritos.Contains(v))
                    validos.Add(v);
            }

            if (validos.Count == 0)
            {
                for (var v = minimo; v <= maximo; v++)
                    validos.Add(v);
            }

            // aplica a paridade (efeito dado só par/ímpar), se houver valor válido
            if (paridade == 1 || paridade == 2)
            {
                var resto = paridade == 1 ? 1 : 0; // ímpar -> resto 1; par -> resto 0
                var filtrados = validos.Where(v => ((v % 2) + 2) % 2 == resto).ToList();
                if (filtrados.Count > 0)
                    validos = filtrados;
                // se não houver valor da paridade pedida, ignora a restrição
            }

            return validos[Random.Shared.Next(validos.Count)];
        }

        /// <summary>
        /// Sorteia um valor de dado conforme a configuração da partida.
        /// Usado, por exemplo, para o dado do aluguel da Internet (bot).
        /// </summary>
        public async Task<int> SortearDadoConfigAsync(
            int idPartida, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var configuracao = await context.Set<Configuracao>()
                .FirstOrDefaultAsync(c => c.IdConfiguracao ==
                    context.Set<Tb_Partida>().Where(p => p.IdPartida == idPartida).Select(p => p.IdConfiguracao).First(),
                    cancellationToken);

            if (configuracao is null)
                throw new InvalidOperationException("Configuração da partida não encontrada.");

            return SortearDado(configuracao);
        }

        private static HashSet<int> ParsearRestricoes(string? restricaoDado)
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

        #endregion

        #region Aluguel, construção e finalização

        public async Task<ResultadoPagamentoAluguel> PagarAluguelAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            if (casa.ProprietarioId <= 0
                || casa.ProprietarioId == jogador.IdPartidaJogador)
                throw new InvalidOperationException("Não há aluguel a pagar nesta casa.");

            var configuracao = await context.Set<Configuracao>()
                .FirstOrDefaultAsync(c => c.IdConfiguracao ==
                    context.Set<Tb_Partida>().Where(p => p.IdPartida == idPartida).Select(p => p.IdConfiguracao).First(),
                    cancellationToken);

            if (configuracao is null)
                throw new InvalidOperationException("Configuração da partida não encontrada.");

            // Imunidade ao aluguel: consome o efeito e não paga.
            if (await ConsumirEfeitoInternoAsync(context, idPartida, idPartidaJogador, EF_EVITAR_ALUGUEL, cancellationToken))
            {
                await ConcluirInternoAsync(context, jogador, cancellationToken);
                return new ResultadoPagamentoAluguel(
                    Pago: false, Eliminado: false, ValorPago: 0m, jogador.SaldoAtual, Imune: true);
            }

            var aluguel = casa.ValorAluguelAtual ?? 0m;

            var dono = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId && j.IdPartida == idPartida, cancellationToken);

            if (jogador.SaldoAtual >= aluguel)
            {
                jogador.SaldoAtual -= aluguel;
                if (dono is not null) dono.SaldoAtual += aluguel;

                await ConcluirInternoAsync(context, jogador, cancellationToken);

                return new ResultadoPagamentoAluguel(
                    Pago: true, Eliminado: false, ValorPago: aluguel, jogador.SaldoAtual);
            }

            if (configuracao.PermitirSaldoNegativo)
            {
                jogador.SaldoAtual -= aluguel;
                if (dono is not null) dono.SaldoAtual += aluguel;

                await ConcluirInternoAsync(context, jogador, cancellationToken);

                return new ResultadoPagamentoAluguel(
                    Pago: true, Eliminado: false, ValorPago: aluguel, jogador.SaldoAtual);
            }

            var potencialVenda = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);

            if (jogador.SaldoAtual + potencialVenda >= aluguel)
            {
                return new ResultadoPagamentoAluguel(
                    Pago: false, Eliminado: false, ValorPago: 0m, jogador.SaldoAtual,
                    PrecisaVender: true, AluguelDevido: aluguel, PotencialVenda: potencialVenda);
            }

            var valorTransferido = jogador.SaldoAtual;
            if (dono is not null && valorTransferido > 0)
                dono.SaldoAtual += valorTransferido;

            jogador.SaldoAtual = 0m;
            await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);

            return new ResultadoPagamentoAluguel(
                Pago: false, Eliminado: true, ValorPago: valorTransferido, 0m, AluguelDevido: aluguel);
        }

        public async Task<ResultadoConstrucao> AdicionarCasaAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var (jogador, casa, cidade, configuracao) =
                await ObterContextoConstrucaoAsync(context, idPartida, idPartidaJogador, cancellationToken);

            if (casa.QtdCasas >= configuracao.QtdMaxCasas)
                throw new InvalidOperationException("Limite de casas atingido para esta propriedade.");

            var custo = Convert.ToDecimal(cidade.ValorAddCasa);

            if (!configuracao.PermitirSaldoNegativo && jogador.SaldoAtual < custo)
                throw new InvalidOperationException("Saldo insuficiente para construir uma casa.");

            jogador.SaldoAtual -= custo;
            casa.QtdCasas += 1;
            RecalcularAluguel(casa, cidade);

            casa.DataAtualizacao = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoConstrucao(casa.QtdCasas, casa.QtdHoteis, casa.ValorAluguelAtual ?? 0m, custo, jogador.SaldoAtual);
        }

        public async Task<ResultadoConstrucao> AdicionarHotelAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var (jogador, casa, cidade, configuracao) =
                await ObterContextoConstrucaoAsync(context, idPartida, idPartidaJogador, cancellationToken);

            if (casa.QtdCasas < configuracao.MinCasasParaHotel)
                throw new InvalidOperationException($"É necessário ter ao menos {configuracao.MinCasasParaHotel} casa(s) para construir um hotel.");

            if (casa.QtdHoteis >= configuracao.QtdMaxHoteis)
                throw new InvalidOperationException("Limite de hotéis atingido para esta propriedade.");

            var custo = Convert.ToDecimal(cidade.ValorAddHotel);

            if (!configuracao.PermitirSaldoNegativo && jogador.SaldoAtual < custo)
                throw new InvalidOperationException("Saldo insuficiente para construir um hotel.");

            jogador.SaldoAtual -= custo;
            casa.QtdHoteis += 1;

            if (configuracao.RemoverCasasAposHotel)
                casa.QtdCasas = 0;

            RecalcularAluguel(casa, cidade);

            casa.DataAtualizacao = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoConstrucao(casa.QtdCasas, casa.QtdHoteis, casa.ValorAluguelAtual ?? 0m, custo, jogador.SaldoAtual);
        }

        private async Task<(Tb_PartidaJogador jogador, PartidaTabuleiro casa, Cidade cidade, Configuracao configuracao)>
            ObterContextoConstrucaoAsync(AppDbContext context, int idPartida, int idPartidaJogador, CancellationToken cancellationToken)
        {
            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            if ((TipoCasa)casa.TipoCasaId != TipoCasa.Cidade)
                throw new InvalidOperationException("Só é possível construir em cidades.");

            if (casa.ProprietarioId != jogador.IdPartidaJogador)
                throw new InvalidOperationException("Você não é o dono desta propriedade.");

            if (casa.ReferenciaCatalogoId <= 0)
                throw new InvalidOperationException("Cidade de catálogo não vinculada a esta casa.");

            var cidade = await context.Set<Cidade>()
                .FirstOrDefaultAsync(c => c.CidadeId == casa.ReferenciaCatalogoId, cancellationToken);

            if (cidade is null)
                throw new InvalidOperationException("Cidade de catálogo não encontrada.");

            var configuracao = await context.Set<Configuracao>()
                .FirstOrDefaultAsync(c => c.IdConfiguracao ==
                    context.Set<Tb_Partida>().Where(p => p.IdPartida == idPartida).Select(p => p.IdConfiguracao).First(),
                    cancellationToken);

            if (configuracao is null)
                throw new InvalidOperationException("Configuração da partida não encontrada.");

            return (jogador, casa, cidade, configuracao);
        }

        private static void RecalcularAluguel(PartidaTabuleiro casa, Cidade cidade)
        {
            var baseAluguel = Convert.ToDecimal(cidade.ValorAluguel);
            var adicionalCasa = Convert.ToDecimal(cidade.AdicionalCasa);
            var adicionalHotel = Convert.ToDecimal(cidade.AdicionalHotel);

            casa.ValorAluguelAtual = baseAluguel
                + (casa.QtdCasas * adicionalCasa)
                + (casa.QtdHoteis * adicionalHotel);
        }

        private async Task<decimal> CalcularPotencialVendaAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, Configuracao configuracao, CancellationToken cancellationToken)
        {
            var propriedades = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida && c.ProprietarioId == idPartidaJogador)
                .ToListAsync(cancellationToken);

            if (propriedades.Count == 0)
                return 0m;

            var percentual = configuracao.PercentualDevolucaoVenda <= 0
                ? 50m
                : Convert.ToDecimal(configuracao.PercentualDevolucaoVenda);

            var fator = percentual / 100m;

            var idsCidade = propriedades
                .Where(p => p.ReferenciaCatalogoId > 0)
                .Select(p => p.ReferenciaCatalogoId)
                .Distinct()
                .ToList();

            var cidades = await context.Set<Cidade>()
                .Where(c => idsCidade.Contains(c.CidadeId))
                .ToDictionaryAsync(c => c.CidadeId, cancellationToken);

            var total = 0m;

            foreach (var p in propriedades)
            {
                total += (p.ValorCompraAtual ?? 0m);

                if (p.ReferenciaCatalogoId > 0 && cidades.TryGetValue(p.ReferenciaCatalogoId, out var cid))
                {
                    total += p.QtdCasas * Convert.ToDecimal(cid.ValorAddCasa);
                    total += p.QtdHoteis * Convert.ToDecimal(cid.ValorAddHotel);
                }
            }

            return total * fator;
        }

        private static async Task ConcluirInternoAsync(AppDbContext context, Tb_PartidaJogador jogador, CancellationToken cancellationToken)
        {
            jogador.EstadoTurno = (int)EstadoTurnoJogador.TurnoConcluido;
            jogador.PodeFinalizar = true;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task EliminarJogadorInternoAsync(
            AppDbContext context, int idPartida, Tb_PartidaJogador jogador, CancellationToken cancellationToken)
        {
            jogador.IdStatusJogador = (int)StatusJogadorPartida.Eliminado;
            jogador.PodeFinalizar = true;
            jogador.EstadoTurno = (int)EstadoTurnoJogador.TurnoConcluido;
            jogador.DataSaida = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            var propriedades = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida && c.ProprietarioId == jogador.IdPartidaJogador)
                .ToListAsync(cancellationToken);

            foreach (var p in propriedades)
            {
                p.ProprietarioId = -1; // -1 = Banco (sem dono)
                p.QtdCasas = 0;
                p.QtdHoteis = 0;
                p.DataAtualizacao = DateTime.Now;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        #endregion

        #region Companhia (normais)

        public sealed record ResultadoCompraCompanhia(
            int CompanhiaId,
            string Nome,
            decimal ValorPago,
            decimal PorcValor,
            decimal AluguelCalculado,
            decimal SaldoFinal);

        public sealed record DadosCompraCompanhia(
            int Posicao,
            int CompanhiaId,
            string Nome,
            string? Imagem,
            string? Frase,
            decimal PorcValor,
            decimal SaldoDisponivel,
            bool IsRevelada);

        public async Task<DadosCompraCompanhia> ObterDadosCompraCompanhiaAsync(
            int idPartida,
            int idPartidaJogador,
            bool debug = false,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            if ((TipoCasa)casa.TipoCasaId != TipoCasa.Companhia)
                throw new InvalidOperationException("Esta casa não é uma companhia.");

            if (casa.ProprietarioId > 0)
                throw new InvalidOperationException("Esta companhia já tem dono.");

            var companhia = await context.Set<Companhia>()
                .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

            if (companhia is null)
                throw new InvalidOperationException("Companhia de catálogo não encontrada.");

            if (companhia.RegraEspecial != (int)RegraEspecialCompanhia.Normal)
                throw new InvalidOperationException("Esta companhia tem regra especial (tratada em outro fluxo).");

            return new DadosCompraCompanhia(
                casa.Posicao,
                companhia.CompanhiaId,
                companhia.Nome,
                companhia.Imagem,
                companhia.Frase,
                companhia.PorcValor ?? 0m,
                jogador.SaldoAtual,
                casa.IsRevelada || debug);
        }

        public async Task<ResultadoCompraCompanhia> ComprarCompanhiaAsync(
            int idPartida,
            int idPartidaJogador,
            decimal valorPago,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            if ((TipoCasa)casa.TipoCasaId != TipoCasa.Companhia)
                throw new InvalidOperationException("Esta casa não é uma companhia.");

            if (casa.ProprietarioId > 0)
                throw new InvalidOperationException("Esta companhia já tem dono.");

            var companhia = await context.Set<Companhia>()
                .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

            if (companhia is null)
                throw new InvalidOperationException("Companhia de catálogo não encontrada.");

            if (companhia.RegraEspecial != (int)RegraEspecialCompanhia.Normal)
                throw new InvalidOperationException("Esta companhia tem regra especial (tratada em outro fluxo).");

            if (valorPago < 1m)
                throw new InvalidOperationException("O valor de compra deve ser ao menos R$ 1,00.");

            if (valorPago > jogador.SaldoAtual)
                throw new InvalidOperationException("Saldo insuficiente para pagar este valor.");

            var porc = companhia.PorcValor ?? 0m;
            var aluguel = Math.Round(valorPago * (porc / 100m), 2, MidpointRounding.AwayFromZero);

            jogador.SaldoAtual -= valorPago;
            casa.ProprietarioId = jogador.IdPartidaJogador;
            casa.IsRevelada = true;
            casa.ValorCompraAtual = valorPago;
            casa.ValorAluguelAtual = aluguel;
            casa.QtdCasas = 0;
            casa.QtdHoteis = 0;
            casa.DataAtualizacao = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoCompraCompanhia(
                companhia.CompanhiaId,
                companhia.Nome,
                valorPago,
                porc,
                aluguel,
                jogador.SaldoAtual);
        }

        public decimal DecidirValorCompraCompanhiaBot(decimal saldoBot)
        {
            if (saldoBot < 1m)
                return 0m;

            var teto = Math.Floor(saldoBot * 0.60m);
            if (teto < 1m)
                teto = 1m;

            var valor = Random.Shared.Next(1, (int)teto + 1);
            return valor;
        }

        #endregion

        #region Companhia Internet (RegraEspecial = 2)

        public sealed record ResultadoCompraInternet(
            bool Comprou,
            int CompanhiaId,
            string Nome,
            decimal ValorPago,
            int FatorMultiplicador,
            decimal SaldoFinal,
            string Mensagem);

        /// <summary>
        /// Compra obrigatória da Internet na posição atual do jogador.
        /// Se o saldo cobre o ValorCompraAtual, compra (debita, vira dono, revela).
        /// Se não cobre, pula (não compra, sem falência). NÃO define o aluguel ainda
        /// (isso é feito por AplicarAluguelInternetAsync após o lançamento do dado).
        /// </summary>
        public async Task<ResultadoCompraInternet> ComprarInternetAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            if ((TipoCasa)casa.TipoCasaId != TipoCasa.Companhia)
                throw new InvalidOperationException("Esta casa não é uma companhia.");

            var companhia = await context.Set<Companhia>()
                .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

            if (companhia is null)
                throw new InvalidOperationException("Companhia de catálogo não encontrada.");

            if (companhia.RegraEspecial != (int)RegraEspecialCompanhia.Internet)
                throw new InvalidOperationException("Esta companhia não é a Internet.");

            var preco = casa.ValorCompraAtual ?? 0m;
            var fator = companhia.FatorMultiplicador ?? 0;

            // Saldo não cobre: pula (sem falência).
            if (jogador.SaldoAtual < preco)
            {
                return new ResultadoCompraInternet(
                    Comprou: false,
                    companhia.CompanhiaId, companhia.Nome, preco, fator, jogador.SaldoAtual,
                    $"{jogador.NomeJogador} não tinha saldo para a compra obrigatória de {companhia.Nome} (R$ {preco:N2}). Compra pulada.");
            }

            // Compra obrigatória.
            jogador.SaldoAtual -= preco;
            casa.ProprietarioId = jogador.IdPartidaJogador;
            casa.IsRevelada = true;
            casa.ValorCompraAtual = preco;
            casa.ValorAluguelAtual = 0m; // será definido pelo dado
            casa.QtdCasas = 0;
            casa.QtdHoteis = 0;
            casa.DataAtualizacao = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoCompraInternet(
                Comprou: true,
                companhia.CompanhiaId, companhia.Nome, preco, fator, jogador.SaldoAtual,
                $"{jogador.NomeJogador} comprou {companhia.Nome} por R$ {preco:N2}. Role o dado para definir o aluguel!");
        }

        /// <summary>
        /// Aplica o aluguel da Internet com base no valor do dado lançado:
        /// ValorAluguelAtual = max(0, valorDado) x FatorMultiplicador.
        /// Se o dado for 0 (ou negativo), o aluguel fica 0 (sem reembolso da compra).
        /// </summary>
        public async Task<decimal> AplicarAluguelInternetAsync(
            int idPartida,
            int idPartidaJogador,
            int valorDado,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var companhia = await context.Set<Companhia>()
                .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

            if (companhia is null)
                throw new InvalidOperationException("Companhia de catálogo não encontrada.");

            var fator = companhia.FatorMultiplicador ?? 0;
            var dadoPositivo = valorDado < 0 ? 0 : valorDado; // só positivos (inclui 0)

            var aluguel = dadoPositivo * (decimal)fator;

            casa.ValorAluguelAtual = aluguel;
            casa.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return aluguel;
        }

        #endregion

        #region Companhia Business (RegraEspecial = 1)

        public sealed record ResultadoBusiness(
            bool PodeComprar,
            bool Comprou,
            int CompanhiaId,
            string Nome,
            decimal AluguelSomado,
            string Mensagem);

        /// <summary>
        /// Verifica se o jogador é dono de TODAS as outras companhias (todas as
        /// casas tipo Companhia da partida, exceto a própria Business).
        /// </summary>
        private async Task<bool> JogadorTemTodasOutrasCompanhiasAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, int companhiaBusinessId, CancellationToken cancellationToken)
        {
            // Todas as casas de companhia da partida, exceto a própria Business.
            var casasCompanhia = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                            && c.TipoCasaId == (byte)TipoCasa.Companhia
                            && c.ReferenciaCatalogoId != companhiaBusinessId)
                .ToListAsync(cancellationToken);

            if (casasCompanhia.Count == 0)
                return false;

            // Todas precisam ter o jogador como dono.
            return casasCompanhia.All(c => c.ProprietarioId == idPartidaJogador);
        }

        /// <summary>
        /// Soma o ValorAluguelAtual de todas as outras companhias (exceto a Business).
        /// </summary>
        private async Task<decimal> SomarAluguelOutrasCompanhiasAsync(
            AppDbContext context, int idPartida, int companhiaBusinessId, CancellationToken cancellationToken)
        {
            var casasCompanhia = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                            && c.TipoCasaId == (byte)TipoCasa.Companhia
                            && c.ReferenciaCatalogoId != companhiaBusinessId)
                .ToListAsync(cancellationToken);

            return casasCompanhia.Sum(c => c.ValorAluguelAtual ?? 0m);
        }

        /// <summary>
        /// Avalia a Business ao cair: informa se o jogador pode comprá-la
        /// (é dono de todas as outras companhias). Não compra ainda.
        /// </summary>
        public async Task<ResultadoBusiness> AvaliarBusinessAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var companhia = await context.Set<Companhia>()
                .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

            if (companhia is null)
                throw new InvalidOperationException("Companhia de catálogo não encontrada.");

            var temTodas = await JogadorTemTodasOutrasCompanhiasAsync(
                context, idPartida, jogador.IdPartidaJogador, companhia.CompanhiaId, cancellationToken);

            if (!temTodas)
            {
                return new ResultadoBusiness(
                    PodeComprar: false, Comprou: false,
                    companhia.CompanhiaId, companhia.Nome, 0m,
                    "Você só pode adquirir esta companhia sendo dono de todas as outras.");
            }

            var soma = await SomarAluguelOutrasCompanhiasAsync(context, idPartida, companhia.CompanhiaId, cancellationToken);

            return new ResultadoBusiness(
                PodeComprar: true, Comprou: false,
                companhia.CompanhiaId, companhia.Nome, soma,
                $"Você pode adquirir {companhia.Nome}! Aluguel = soma das outras companhias (R$ {soma:N2}).");
        }

        /// <summary>
        /// Adquire a Business (grátis). Pré-requisito: ser dono de todas as
        /// outras companhias. Aluguel = soma do aluguel das outras, fixado agora.
        /// </summary>
        public async Task<ResultadoBusiness> ComprarBusinessAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var companhia = await context.Set<Companhia>()
                .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

            if (companhia is null)
                throw new InvalidOperationException("Companhia de catálogo não encontrada.");

            if (companhia.RegraEspecial != (int)RegraEspecialCompanhia.Business)
                throw new InvalidOperationException("Esta companhia não é a Business.");

            if (casa.ProprietarioId > 0)
                throw new InvalidOperationException("Esta companhia já tem dono.");

            var temTodas = await JogadorTemTodasOutrasCompanhiasAsync(
                context, idPartida, jogador.IdPartidaJogador, companhia.CompanhiaId, cancellationToken);

            if (!temTodas)
                throw new InvalidOperationException("Você precisa ser dono de todas as outras companhias.");

            var soma = await SomarAluguelOutrasCompanhiasAsync(context, idPartida, companhia.CompanhiaId, cancellationToken);

            // Aquisição grátis.
            casa.ProprietarioId = jogador.IdPartidaJogador;
            casa.IsRevelada = true;
            casa.ValorCompraAtual = 0m;
            casa.ValorAluguelAtual = soma;   // fixado no momento da compra
            casa.QtdCasas = 0;
            casa.QtdHoteis = 0;
            casa.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoBusiness(
                PodeComprar: true, Comprou: true,
                companhia.CompanhiaId, companhia.Nome, soma,
                $"{jogador.NomeJogador} adquiriu {companhia.Nome}! Aluguel fixado em R$ {soma:N2}.");
        }

        /// <summary>
        /// Verifica todas as Business da partida: se o dono não for mais dono de
        /// todas as outras companhias, a Business volta ao banco (revogada).
        /// Retorna mensagens de aviso para cada revogação ocorrida.
        /// Deve ser chamado no início da vez do jogador.
        /// </summary>
        public async Task<List<string>> RevalidarBusinessAsync(
            int idPartida,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var avisos = new List<string>();

            // Acha as casas Business da partida (RegraEspecial = Business).
            var idsBusiness = await context.Set<Companhia>()
                .Where(c => c.RegraEspecial == (int)RegraEspecialCompanhia.Business)
                .Select(c => c.CompanhiaId)
                .ToListAsync(cancellationToken);

            if (idsBusiness.Count == 0)
                return avisos;

            var casasBusiness = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                            && c.TipoCasaId == (byte)TipoCasa.Companhia
                            && idsBusiness.Contains(c.ReferenciaCatalogoId)
                            && c.ProprietarioId > 0)
                .ToListAsync(cancellationToken);

            if (casasBusiness.Count == 0)
                return avisos;

            var houveRevogacao = false;

            foreach (var biz in casasBusiness)
            {
                var donoId = biz.ProprietarioId;

                var temTodas = await JogadorTemTodasOutrasCompanhiasAsync(
                    context, idPartida, donoId, biz.ReferenciaCatalogoId, cancellationToken);

                if (!temTodas)
                {
                    // Revoga: volta ao banco.
                    var dono = await context.Set<Tb_PartidaJogador>()
                        .FirstOrDefaultAsync(j => j.IdPartidaJogador == donoId && j.IdPartida == idPartida, cancellationToken);

                    biz.ProprietarioId = -1;
                    biz.ValorAluguelAtual = 0m;
                    biz.ValorCompraAtual = 0m;
                    biz.QtdCasas = 0;
                    biz.QtdHoteis = 0;
                    biz.DataAtualizacao = DateTime.Now;
                    houveRevogacao = true;

                    var nome = dono?.NomeJogador ?? "Jogador";
                    avisos.Add($"{nome} perdeu {biz.Nome}: não é mais dono de todas as companhias.");
                }
            }

            if (houveRevogacao)
                await context.SaveChangesAsync(cancellationToken);

            return avisos;
        }

        #endregion

        #region Companhia Xerox (RegraEspecial = 3)

        public sealed record ResultadoCompraXerox(
            bool Comprou,
            int CompanhiaId,
            string Nome,
            decimal ValorPago,
            decimal SaldoFinal,
            string Mensagem);

        /// <summary>
        /// Lista as propriedades que a Xerox pode copiar: qualquer Cidade ou
        /// Companhia COM dono e aluguel &gt; 0, exceto a própria Xerox.
        /// </summary>
        public async Task<List<PropriedadeSelecionavelDTO>> ListarCopiaveisXeroxAsync(
            int idPartida,
            int posicaoXerox,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var casas = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                            && c.Posicao != posicaoXerox
                            && (c.TipoCasaId == (byte)TipoCasa.Cidade || c.TipoCasaId == (byte)TipoCasa.Companhia)
                            && c.ProprietarioId > 0
                            && (c.ValorAluguelAtual ?? 0m) > 0m)
                .ToListAsync(cancellationToken);

            // nomes dos donos
            var idsDonos = casas.Select(c => c.ProprietarioId).Distinct().ToList();
            var donos = await context.Set<Tb_PartidaJogador>()
                .Where(j => j.IdPartida == idPartida && idsDonos.Contains(j.IdPartidaJogador))
                .ToDictionaryAsync(j => j.IdPartidaJogador, j => j.NomeJogador, cancellationToken);

            return casas
                .Select(c => new PropriedadeSelecionavelDTO
                {
                    Posicao = c.Posicao,
                    PartidaTabuleiroId = c.PartidaTabuleiroId,
                    Nome = c.Nome,
                    Imagem = c.Imagem,
                    TipoCasaId = c.TipoCasaId,
                    ProprietarioId = c.ProprietarioId,
                    NomeProprietario = donos.TryGetValue(c.ProprietarioId, out var n) ? n : "Banco",
                    ValorCompra = c.ValorCompraAtual ?? 0m,
                    ValorAluguel = c.ValorAluguelAtual ?? 0m,
                    QtdCasas = c.QtdCasas,
                    QtdHoteis = c.QtdHoteis,
                    CorHexadecimal = c.CorHexadecimal
                })
                .OrderByDescending(p => p.ValorAluguel)
                .ToList();
        }

        /// <summary>
        /// Compra a Xerox pelo valor dela (opcional). Pré-requisito: existir ao
        /// menos uma propriedade copiável. Debita, vira dono, revela.
        /// O aluguel NÃO é definido aqui (vem da escolha em AplicarCopiaXeroxAsync).
        /// </summary>
        public async Task<ResultadoCompraXerox> ComprarXeroxAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var companhia = await context.Set<Companhia>()
                .FirstOrDefaultAsync(c => c.CompanhiaId == casa.ReferenciaCatalogoId, cancellationToken);

            if (companhia is null)
                throw new InvalidOperationException("Companhia de catálogo não encontrada.");

            if (companhia.RegraEspecial != (int)RegraEspecialCompanhia.Xerox)
                throw new InvalidOperationException("Esta companhia não é a Xerox.");

            if (casa.ProprietarioId > 0)
                throw new InvalidOperationException("Esta companhia já tem dono.");

            var preco = casa.ValorCompraAtual ?? 0m;

            if (jogador.SaldoAtual < preco)
                throw new InvalidOperationException("Saldo insuficiente para comprar a Xerox.");

            // precisa existir algo copiável
            var copiaveis = await ListarCopiaveisXeroxAsync(idPartida, casa.Posicao, cancellationToken);
            if (copiaveis.Count == 0)
                throw new InvalidOperationException("Não há propriedades para copiar.");

            jogador.SaldoAtual -= preco;
            casa.ProprietarioId = jogador.IdPartidaJogador;
            casa.IsRevelada = true;
            casa.ValorCompraAtual = preco;
            casa.ValorAluguelAtual = 0m; // definido pela cópia
            casa.QtdCasas = 0;
            casa.QtdHoteis = 0;
            casa.DataAtualizacao = DateTime.Now;
            jogador.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoCompraXerox(
                Comprou: true,
                companhia.CompanhiaId, companhia.Nome, preco, jogador.SaldoAtual,
                $"{jogador.NomeJogador} comprou {companhia.Nome} por R$ {preco:N2}. Escolha a propriedade para copiar o aluguel!");
        }

        /// <summary>
        /// Aplica a cópia: o ValorAluguelAtual da Xerox passa a ser o aluguel
        /// (fixo) da propriedade escolhida.
        /// </summary>
        public async Task<decimal> AplicarCopiaXeroxAsync(
            int idPartida,
            int idPartidaJogador,
            int partidaTabuleiroIdAlvo,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var xerox = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);

            if (xerox is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var alvo = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.PartidaTabuleiroId == partidaTabuleiroIdAlvo, cancellationToken);

            if (alvo is null)
                throw new InvalidOperationException("Propriedade escolhida não encontrada.");

            var aluguelCopiado = alvo.ValorAluguelAtual ?? 0m;

            xerox.ValorAluguelAtual = aluguelCopiado;
            xerox.DataAtualizacao = DateTime.Now;

            await context.SaveChangesAsync(cancellationToken);

            return aluguelCopiado;
        }

        #endregion

        #region Sorteio de catálogo (Imposto / Prisao / Efeito) com "não repetir"

        // TipoCatalogo: 1=Imposto, 2=Prisao, 3=Efeito
        private const byte CAT_IMPOSTO = 1;
        private const byte CAT_PRISAO = 2;
        private const byte CAT_EFEITO = 3;

        /// <summary>
        /// Escolhe um id de catálogo (imposto/prisão/efeito) para a casa, respeitando
        /// as configurações da partida:
        /// - EfeitosAleatorios = 0  -> usa o id fixo da casa (referenciaFixa).
        /// - EfeitosAleatorios = 1  -> sorteia entre todos os ids do tipo.
        ///   - NaoRepetirEfeitos = 1 -> sorteia só entre os ainda não usados nesta
        ///     partida; quando todos saíram, zera o controle e recomeça.
        /// Registra o uso quando há sorteio com "não repetir".
        /// </summary>
        private async Task<int> SortearCatalogoAsync(
            AppDbContext context,
            int idPartida,
            byte tipoCatalogo,
            int referenciaFixa,
            List<int> todosIds,
            bool efeitosAleatorios,
            bool naoRepetir,
            CancellationToken cancellationToken)
        {
            // Sem aleatório: usa o fixo da casa.
            if (!efeitosAleatorios)
                return referenciaFixa;

            if (todosIds.Count == 0)
                return referenciaFixa; // fallback de segurança

            // Aleatório SEM "não repetir": sorteia livre.
            if (!naoRepetir)
                return todosIds[Random.Shared.Next(todosIds.Count)];

            // Aleatório COM "não repetir": só entre os que ainda não saíram.
            var usados = await context.Set<PartidaCatalogoUsado>()
                .Where(u => u.IdPartida == idPartida && u.TipoCatalogo == tipoCatalogo)
                .Select(u => u.CatalogoId)
                .ToListAsync(cancellationToken);

            var disponiveis = todosIds.Where(id => !usados.Contains(id)).ToList();

            // Todos já foram usados: zera o controle deste tipo e recomeça.
            if (disponiveis.Count == 0)
            {
                var registros = await context.Set<PartidaCatalogoUsado>()
                    .Where(u => u.IdPartida == idPartida && u.TipoCatalogo == tipoCatalogo)
                    .ToListAsync(cancellationToken);

                context.Set<PartidaCatalogoUsado>().RemoveRange(registros);
                await context.SaveChangesAsync(cancellationToken);

                disponiveis = new List<int>(todosIds);
            }

            var escolhido = disponiveis[Random.Shared.Next(disponiveis.Count)];

            // Registra o uso.
            context.Set<PartidaCatalogoUsado>().Add(new PartidaCatalogoUsado
            {
                IdPartida = idPartida,
                TipoCatalogo = tipoCatalogo,
                CatalogoId = escolhido,
                DataUso = DateTime.Now
            });
            await context.SaveChangesAsync(cancellationToken);

            return escolhido;
        }

        private static (bool aleatorios, bool naoRepetir) LerConfigEfeitos(Configuracao cfg)
        {
            // ajuste os nomes se diferirem na sua entidade Configuracao
            var aleatorios = cfg.EfeitosAleatorios;
            var naoRepetir = cfg.NaoRepetirEfeitos;
            return (aleatorios, naoRepetir);
        }

        #endregion

        #region Imposto

        public sealed record ResultadoImposto(
            int ImpostoId,
            string Nome,
            string? Frase,
            string? Imagem,
            bool DependeDado,
            int FatorMultiplicador,
            decimal Valor,         // valor já calculado (quando não depende de dado)
            string Mensagem,
            bool Imune = false);   // imunidade a imposto consumida: não paga

        /// <summary>
        /// Resolve a casa de imposto ao cair: escolhe o imposto (fixo ou sorteado),
        /// e calcula o valor a pagar — exceto quando depende de dado (aí a tela
        /// rola o dado e chama AplicarImpostoDadoAsync).
        /// </summary>
        public async Task<ResultadoImposto> ResolverImpostoAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);
            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var configuracao = await ObterConfiguracaoAsync(context, idPartida, cancellationToken);
            var (aleatorios, naoRepetir) = LerConfigEfeitos(configuracao);

            // Imunidade a imposto: consome o efeito e não paga (nem sorteia).
            if (await ConsumirEfeitoInternoAsync(context, idPartida, idPartidaJogador, EF_IMUNIDADE_IMPOSTOS, cancellationToken))
            {
                casa.IsRevelada = true;
                casa.DataAtualizacao = DateTime.Now;
                await context.SaveChangesAsync(cancellationToken);

                return new ResultadoImposto(
                    0, "Imune a impostos", "Você é milionário e milionários não pagam impostos!", null,
                    false, 0, 0m, "Você estava imune e não pagou o imposto.", Imune: true);
            }

            var todosIds = await context.Set<Imposto>()
                .Select(i => i.ImpostoId)
                .ToListAsync(cancellationToken);

            var impostoId = await SortearCatalogoAsync(
                context, idPartida, CAT_IMPOSTO, casa.ReferenciaCatalogoId, todosIds,
                aleatorios, naoRepetir, cancellationToken);

            var imposto = await context.Set<Imposto>()
                .FirstOrDefaultAsync(i => i.ImpostoId == impostoId, cancellationToken);
            if (imposto is null)
                throw new InvalidOperationException("Imposto de catálogo não encontrado.");

            // revela a casa
            casa.IsRevelada = true;
            casa.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            // Depende de dado: a tela vai rolar e chamar AplicarImpostoDadoAsync.
            if (imposto.ValorDependeDado)
            {
                return new ResultadoImposto(
                    imposto.ImpostoId, imposto.Nome, imposto.Frase, imposto.Imagem,
                    DependeDado: true, imposto.FatorMultiplicador, 0m,
                    imposto.Frase ?? "Role o dado para definir o imposto!");
            }

            // Calcula o valor: TipoValorId 2 = fixo; 1 = percentual do saldo.
            decimal valor;
            switch (imposto.TipoValorId)
            {
                case 1:
                    valor = Math.Round(jogador.SaldoAtual * (imposto.Valor / 100m), 2, MidpointRounding.AwayFromZero);
                    break;

                case 2:
                default:
                    valor = imposto.Valor;
                    break;
            }

            return new ResultadoImposto(
                imposto.ImpostoId, imposto.Nome, imposto.Frase, imposto.Imagem,
                DependeDado: false, imposto.FatorMultiplicador, valor,
                $"{imposto.Frase} Valor: R$ {valor:N2}.");
        }

        /// <summary>Imposto que depende do dado: valor = dado x FatorMultiplicador.</summary>
        public decimal CalcularImpostoDado(int valorDado, int fatorMultiplicador)
        {
            var d = valorDado < 0 ? 0 : valorDado;
            return d * (decimal)fatorMultiplicador;
        }

        /// <summary>
        /// Cobra o imposto do jogador. Mesma regra de falência do aluguel:
        /// se não cobre nem vendendo, elimina. Retorna o pagamento.
        /// </summary>
        public async Task<ResultadoPagamentoAluguel> PagarImpostoAsync(
            int idPartida,
            int idPartidaJogador,
            decimal valorImposto,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var configuracao = await ObterConfiguracaoAsync(context, idPartida, cancellationToken);

            // O imposto some do jogo (vai para o banco).
            if (jogador.SaldoAtual >= valorImposto || configuracao.PermitirSaldoNegativo)
            {
                jogador.SaldoAtual -= valorImposto;
                await ConcluirInternoAsync(context, jogador, cancellationToken);
                return new ResultadoPagamentoAluguel(
                    Pago: true, Eliminado: false, ValorPago: valorImposto, jogador.SaldoAtual);
            }

            var potencial = await CalcularPotencialVendaAsync(context, idPartida, jogador.IdPartidaJogador, configuracao, cancellationToken);
            if (jogador.SaldoAtual + potencial >= valorImposto)
            {
                return new ResultadoPagamentoAluguel(
                    Pago: false, Eliminado: false, ValorPago: 0m, jogador.SaldoAtual,
                    PrecisaVender: true, AluguelDevido: valorImposto, PotencialVenda: potencial);
            }

            jogador.SaldoAtual = 0m;
            await EliminarJogadorInternoAsync(context, idPartida, jogador, cancellationToken);
            return new ResultadoPagamentoAluguel(
                Pago: false, Eliminado: true, ValorPago: 0m, 0m, AluguelDevido: valorImposto);
        }

        #endregion

        #region Prisão

        public sealed record ResultadoPrisao(
            int PrisaoId,
            string Nome,
            string? Frase,
            string? Imagem,
            bool DependeDado,
            int Rodadas,           // quando não depende de dado
            string Mensagem,
            bool Imune = false);   // imunidade a prisão consumida: não prende

        /// <summary>
        /// Resolve a casa de prisão ao cair: escolhe a prisão (fixa ou sorteada).
        /// Se depende de dado, a tela rola e chama AplicarPrisaoDadoAsync;
        /// senão, já prende por QtdRodadas.
        /// </summary>
        public async Task<ResultadoPrisao> ResolverPrisaoAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);
            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var configuracao = await ObterConfiguracaoAsync(context, idPartida, cancellationToken);
            var (aleatorios, naoRepetir) = LerConfigEfeitos(configuracao);

            // Imunidade a prisão: consome o efeito e não prende (nem sorteia).
            if (await ConsumirEfeitoInternoAsync(context, idPartida, idPartidaJogador, EF_IMUNIDADE_PRISAO, cancellationToken))
            {
                casa.IsRevelada = true;
                casa.DataAtualizacao = DateTime.Now;
                await context.SaveChangesAsync(cancellationToken);

                return new ResultadoPrisao(
                    0, "Imune à prisão", "Você subornou os juízes e está imune à prisão, até parece um político!", null,
                    DependeDado: false, 0, "Você estava imune e escapou da prisão!", Imune: true);
            }

            var todosIds = await context.Set<Prisao>()
                .Select(p => p.PrisaoId)
                .ToListAsync(cancellationToken);

            var prisaoId = await SortearCatalogoAsync(
                context, idPartida, CAT_PRISAO, casa.ReferenciaCatalogoId, todosIds,
                aleatorios, naoRepetir, cancellationToken);

            var prisao = await context.Set<Prisao>()
                .FirstOrDefaultAsync(p => p.PrisaoId == prisaoId, cancellationToken);
            if (prisao is null)
                throw new InvalidOperationException("Prisão de catálogo não encontrada.");

            casa.IsRevelada = true;
            casa.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            // Depende de dado: a tela rola e chama AplicarPrisaoDadoAsync.
            if (prisao.ValorDependeDado)
            {
                return new ResultadoPrisao(
                    prisao.PrisaoId, prisao.Nome, prisao.Frase, prisao.Imagem,
                    DependeDado: true, 0, prisao.Frase ?? "Role o dado para ver quantas rodadas ficará preso!");
            }

            // Prende por QtdRodadas.
            jogador.TurnosPreso = prisao.QtdRodadas;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return new ResultadoPrisao(
                prisao.PrisaoId, prisao.Nome, prisao.Frase, prisao.Imagem,
                DependeDado: false, prisao.QtdRodadas,
                $"{prisao.Frase} Preso por {prisao.QtdRodadas} rodada(s).");
        }

        /// <summary>Prisão que depende do dado: rodadas = valor do dado.</summary>
        public async Task<int> AplicarPrisaoDadoAsync(
            int idPartida,
            int idPartidaJogador,
            int valorDado,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var rodadas = valorDado < 0 ? 0 : valorDado;
            jogador.TurnosPreso = rodadas;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return rodadas;
        }

        /// <summary>
        /// Chamado no início da vez: se o jogador está preso, decrementa o
        /// contador e indica que ele NÃO joga nesta rodada (só anda quando o
        /// contador iniciar a vez já zerado). Retorna true se ainda está preso.
        /// </summary>
        public async Task<bool> ProcessarInicioVezPrisaoAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            if (jogador.TurnosPreso <= 0)
                return false; // não está preso, joga normal

            // Está preso: consome uma rodada e não joga nesta.
            jogador.TurnosPreso -= 1;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return true; // ainda estava preso nesta rodada (não anda)
        }

        #endregion

        #region Helper de configuração

        private async Task<Configuracao> ObterConfiguracaoAsync(
            AppDbContext context, int idPartida, CancellationToken cancellationToken)
        {
            var configuracao = await context.Set<Configuracao>()
                .FirstOrDefaultAsync(c => c.IdConfiguracao ==
                    context.Set<Tb_Partida>().Where(p => p.IdPartida == idPartida).Select(p => p.IdConfiguracao).First(),
                    cancellationToken);

            if (configuracao is null)
                throw new InvalidOperationException("Configuração da partida não encontrada.");

            return configuracao;
        }

        #endregion

        #region Efeito (Fatia 1: saldo e movimento)

        // EfeitoAcao (do enum do usuário)
        private const byte EFACAO_ALEATORIO = 1;
        private const byte EFACAO_ALUGUEL = 2;
        private const byte EFACAO_APOSTAS = 3;
        private const byte EFACAO_AVANCE = 4;
        private const byte EFACAO_BLOQUEIO = 5;
        private const byte EFACAO_CASA_ESPECIFICA = 6;
        private const byte EFACAO_ESCOLHER = 8;
        private const byte EFACAO_GANHE = 9;
        private const byte EFACAO_GRUPO = 10;
        private const byte EFACAO_IMPARES = 11;
        private const byte EFACAO_IMUNIDADE = 12;
        private const byte EFACAO_INVERTER = 13;
        private const byte EFACAO_PARES = 15;
        private const byte EFACAO_PERCA = 16;
        private const byte EFACAO_PROPRIEDADE = 17;
        private const byte EFACAO_TROCAR = 18;
        private const byte EFACAO_VOLTE = 20;

        // EfeitoSubAlvo
        private const byte EFSUB_TABULEIRO = 13;

        /// <summary>
        /// Indica se a ação do efeito é "guardada" (vira status do jogador até
        /// ser consumida por um gatilho ou expirar por volta), em vez de aplicada
        /// na hora. Baseado na ação (não em id específico).
        /// </summary>
        private static bool EhEfeitoGuardado(byte acaoEfeitoId)
        {
            switch (acaoEfeitoId)
            {
                case EFACAO_IMUNIDADE: // imune a imposto/prisão/efeito
                case EFACAO_ALUGUEL:   // evitar próximo aluguel
                case EFACAO_BLOQUEIO:  // bloqueio de compra / sem bônus de volta
                case EFACAO_IMPARES:   // dado só ímpar
                case EFACAO_PARES:     // dado só par
                case EFACAO_APOSTAS:   // apostas sem mínimo
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// O que a tela deve fazer após resolver a casa de efeito.
        /// </summary>
        public enum AcaoTelaEfeito
        {
            Nada,             // já aplicado (ex.: ganhe/perca) — só exibir mensagem
            RolarDado,        // precisa rolar o dado (avançar/voltar N casas)
            EscolherCasa,     // precisa escolher uma casa do tabuleiro (CasaEspecifica)
            PegarBanco,       // escolher uma cidade sem dono e pegá-la de graça
            Roubar,           // escolher uma propriedade de outro jogador e roubá-la
            TrocarJogador,    // trocar de posição com outro jogador
            TrocarGrupo,      // trocar o grupo de uma propriedade
            NaoImplementado   // efeito normal ainda não tratado nesta fatia
        }

        public sealed record ResultadoEfeito(
            int EfeitoId,
            string Nome,
            string? Frase,
            string? Imagem,
            AcaoTelaEfeito AcaoTela,
            byte AcaoEfeitoId,
            decimal FatorMultiplicador,
            bool AplicarCasaAposEfeito,
            string Mensagem,
            bool Imune = false,        // imunidade a efeito consumida: não aplica
            string Tom = "neutro");    // "bom" | "ruim" | "neutro" — para o som

        /// <summary>
        /// Resolve a casa de efeito ao cair: escolhe o efeito (fixo ou sorteado entre
        /// os TipoEfeitoId=1 ainda não usados) e aplica os instantâneos de saldo.
        /// Movimentos (avançar/voltar/teleporte/escolher) são sinalizados para a tela.
        /// </summary>
        public async Task<ResultadoEfeito> ResolverEfeitoAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaId == idPartida && c.Posicao == jogador.PosicaoAtual, cancellationToken);
            if (casa is null)
                throw new InvalidOperationException("Casa atual não encontrada.");

            var configuracao = await ObterConfiguracaoAsync(context, idPartida, cancellationToken);
            var (aleatorios, naoRepetir) = LerConfigEfeitos(configuracao);

            // Imunidade a efeito: consome e nem sorteia o efeito.
            if (await ConsumirEfeitoInternoAsync(context, idPartida, idPartidaJogador, EF_IMUNIDADE_EFEITOS, cancellationToken))
            {
                casa.IsRevelada = true;
                casa.DataAtualizacao = DateTime.Now;
                await context.SaveChangesAsync(cancellationToken);

                return new ResultadoEfeito(
                    0, "Imune a efeitos", "Você não foi afetado por este efeito!", null,
                    AcaoTelaEfeito.Nada, 0, 0m, false,
                    "Você estava imune e não foi afetado pelo efeito!", Imune: true, Tom: "bom");
            }

            // Só efeitos NORMAIS (TipoEfeitoId = 1) entram no sorteio da casa de efeito.
            var idsNormais = await context.Set<Efeito>()
                .Where(e => e.TipoEfeitoId == (byte)EfeitoTipo.Normal)
                .Select(e => e.EfeitoId)
                .ToListAsync(cancellationToken);

            var efeitoId = await SortearCatalogoAsync(
                context, idPartida, CAT_EFEITO, casa.ReferenciaCatalogoId, idsNormais,
                aleatorios, naoRepetir, cancellationToken);

            var efeito = await context.Set<Efeito>()
                .FirstOrDefaultAsync(e => e.EfeitoId == efeitoId, cancellationToken);
            if (efeito is null)
                throw new InvalidOperationException("Efeito de catálogo não encontrado.");

            casa.IsRevelada = true;
            casa.DataAtualizacao = DateTime.Now;

            var nome = "Efeito";
            var acaoTela = AcaoTelaEfeito.Nada;
            var mensagem = efeito.Frase ?? "Efeito aplicado.";

            var acaoId = efeito.AcaoEfeitoId ?? 0;
            var valorEfeito = efeito.ValorEfeito ?? 0m;
            var aplicaCasaApos = efeito.AplicarEfeitoCasaAposEfeito ?? false;

            switch (acaoId)
            {
                case EFACAO_GANHE:
                    // crédito direto no saldo
                    jogador.SaldoAtual += valorEfeito;
                    jogador.DataAtualizacao = DateTime.Now;
                    mensagem = $"{efeito.Frase} (+R$ {valorEfeito:N2})";
                    acaoTela = AcaoTelaEfeito.Nada;
                    break;

                case EFACAO_PERCA:
                    // débito; sem derrota por saldo negativo nos efeitos.
                    // Se não permite negativo, não deixa o saldo passar de 0.
                    if (configuracao.PermitirSaldoNegativo)
                        jogador.SaldoAtual -= valorEfeito;
                    else
                        jogador.SaldoAtual = Math.Max(0m, jogador.SaldoAtual - valorEfeito);

                    jogador.DataAtualizacao = DateTime.Now;
                    mensagem = $"{efeito.Frase} (-R$ {valorEfeito:N2})";
                    acaoTela = AcaoTelaEfeito.Nada;
                    break;

                case EFACAO_AVANCE:
                case EFACAO_VOLTE:
                    // precisa rolar o dado para saber quantas casas
                    acaoTela = AcaoTelaEfeito.RolarDado;
                    break;

                case EFACAO_CASA_ESPECIFICA:
                    // jogador escolhe a casa de destino (não continua a jogada)
                    acaoTela = AcaoTelaEfeito.EscolherCasa;
                    break;

                case EFACAO_ESCOLHER:
                    // pegar uma cidade sem dono do banco (escolha no modal)
                    acaoTela = AcaoTelaEfeito.PegarBanco;
                    break;

                case EFACAO_PROPRIEDADE:
                    // roubar uma propriedade de outro jogador (escolha no modal)
                    acaoTela = AcaoTelaEfeito.Roubar;
                    break;

                case EFACAO_TROCAR:
                    // trocar de posição com outro jogador (escolha/confirma no modal)
                    acaoTela = AcaoTelaEfeito.TrocarJogador;
                    break;

                case EFACAO_GRUPO:
                    // trocar o grupo de uma propriedade (escolha cidade + grupo no modal)
                    acaoTela = AcaoTelaEfeito.TrocarGrupo;
                    break;

                case EFACAO_INVERTER:
                    // embaralha as posições das casas do tabuleiro (peões ficam parados)
                    await EmbaralharTabuleiroAsync(context, idPartida, cancellationToken);
                    mensagem = efeito.Frase ?? "A ordem do tabuleiro foi alterada!";
                    acaoTela = AcaoTelaEfeito.Nada;
                    break;

                case EFACAO_ALEATORIO:
                    // teleporte para casa aleatória (sem resolver o destino)
                    {
                        var totalCasas = await context.Set<PartidaTabuleiro>()
                            .CountAsync(c => c.PartidaId == idPartida, cancellationToken);
                        if (totalCasas > 0)
                        {
                            var destino = Random.Shared.Next(1, totalCasas + 1);
                            jogador.PosicaoAtual = destino;
                            jogador.DataAtualizacao = DateTime.Now;
                            mensagem = $"{efeito.Frase} (foi para a casa {destino})";
                        }
                        acaoTela = AcaoTelaEfeito.Nada;
                    }
                    break;

                default:
                    // Efeitos guardados (imunidade, bloqueio, dado par/ímpar, apostas):
                    // viram status do jogador. Identificados pela ação (genérico).
                    if (EhEfeitoGuardado(acaoId))
                    {
                        // Remove efeito conflitante (ex.: dado só par <-> dado só ímpar).
                        await RemoverEfeitoConflitanteAsync(
                            context, idPartida, idPartidaJogador, efeito.ColunaEfeito, cancellationToken);

                        // evita duplicar o mesmo efeito ativo
                        var jaTem = await context.Set<PartidaJogadorEfeito>()
                            .AnyAsync(e => e.IdPartida == idPartida
                                        && e.IdPartidaJogador == idPartidaJogador
                                        && e.EfeitoId == efeito.EfeitoId
                                        && e.Ativo, cancellationToken);

                        if (!jaTem)
                        {
                            context.Set<PartidaJogadorEfeito>().Add(new PartidaJogadorEfeito
                            {
                                IdPartida = idPartida,
                                IdPartidaJogador = idPartidaJogador,
                                EfeitoId = efeito.EfeitoId,
                                ColunaEfeito = efeito.ColunaEfeito,
                                Ativo = true,
                                DataAquisicao = DateTime.Now
                            });
                        }

                        acaoTela = AcaoTelaEfeito.Nada; // só exibe a frase; status guardado
                    }
                    else
                    {
                        // demais efeitos normais entram nas próximas fatias
                        acaoTela = AcaoTelaEfeito.NaoImplementado;
                    }
                    break;
            }

            await context.SaveChangesAsync(cancellationToken);

            // tom para o som: ganhe/imunidade = bom; perca/bloqueio = ruim; resto = neutro
            var tom = "neutro";
            switch (acaoId)
            {
                case EFACAO_GANHE:
                case EFACAO_IMUNIDADE:
                    tom = "bom";
                    break;
                case EFACAO_PERCA:
                case EFACAO_BLOQUEIO:
                case EFACAO_INVERTER:
                    tom = "ruim";
                    break;
            }

            return new ResultadoEfeito(
                efeito.EfeitoId, nome, efeito.Frase, efeito.Imagem,
                acaoTela, acaoId, efeito.FatorMultiplicador,
                aplicaCasaApos, mensagem, Imune: false, Tom: tom);
        }

        /// <summary>
        /// Move o jogador N casas (N = dado x fator) para frente (Avance) ou
        /// para trás (Volte), com volta circular. Ao cruzar o fim, conta volta e
        /// concede o bônus (se houver). Retorna a nova posição.
        /// </summary>
        public async Task<int> AplicarMovimentoEfeitoAsync(
            int idPartida,
            int idPartidaJogador,
            int valorDado,
            int fator,
            bool avancar,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            var configuracao = await ObterConfiguracaoAsync(context, idPartida, cancellationToken);

            var totalCasas = await context.Set<PartidaTabuleiro>()
                .CountAsync(c => c.PartidaId == idPartida, cancellationToken);
            if (totalCasas <= 0)
                throw new InvalidOperationException("Tabuleiro da partida sem casas.");

            var passos = Math.Max(0, valorDado) * Math.Max(1, fator);
            if (!avancar) passos = -passos;

            var atual = jogador.PosicaoAtual <= 0 ? 1 : jogador.PosicaoAtual;

            // posição em base 1 circular
            var zeroBased = ((atual - 1 + passos) % totalCasas);
            if (zeroBased < 0) zeroBased += totalCasas;
            var novaPos = zeroBased + 1;

            // Detecta cruzamento do fim (volta) para frente ou para trás.
            var completouVolta = false;
            if (avancar && (atual + passos) > totalCasas)
                completouVolta = true;
            else if (!avancar && (atual + passos) < 1)
                completouVolta = true; // voltou antes da casa 1 -> conta como volta

            if (completouVolta)
            {
                jogador.VoltasCompletadas += 1;

                var semBonus = await TemEfeitoAtivoInternoAsync(
                    context, idPartida, jogador.IdPartidaJogador, EF_SEM_BONUS_VOLTA, cancellationToken);

                if (!semBonus)
                {
                    var ateVolta = configuracao.BonusAteVolta;
                    if (ateVolta <= 0 || jogador.VoltasCompletadas <= ateVolta)
                        jogador.SaldoAtual += Convert.ToDecimal(configuracao.BonusVolta);
                }

                await ExpirarEfeitosPorVoltaAsync(context, idPartida, jogador.IdPartidaJogador, cancellationToken);
            }

            jogador.PosicaoAtual = novaPos;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return novaPos;
        }

        /// <summary>
        /// Move o jogador para uma casa específica escolhida (efeito CasaEspecifica).
        /// Não resolve a casa de destino nem concede bônus (a jogada encerra).
        /// </summary>
        public async Task MoverParaCasaAsync(
            int idPartida,
            int idPartidaJogador,
            int posicaoDestino,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            jogador.PosicaoAtual = posicaoDestino;
            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Lista todas as casas do tabuleiro para o jogador escolher um destino
        /// (efeito CasaEspecifica). Usa o DTO genérico de seleção.
        /// </summary>
        public async Task<List<PropriedadeSelecionavelDTO>> ListarCasasTabuleiroAsync(
            int idPartida,
            int posicaoExcluir,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var casas = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida && c.Posicao != posicaoExcluir)
                .OrderBy(c => c.Posicao)
                .ToListAsync(cancellationToken);

            return casas.Select(c => new PropriedadeSelecionavelDTO
            {
                Posicao = c.Posicao,
                PartidaTabuleiroId = c.PartidaTabuleiroId,
                Nome = c.IsRevelada ? c.Nome : $"Casa {c.Posicao}",
                Imagem = c.IsRevelada ? c.Imagem : null,
                TipoCasaId = c.TipoCasaId,
                ProprietarioId = c.ProprietarioId,
                NomeProprietario = c.ProprietarioId > 0 ? "Jogador" : "Banco",
                ValorCompra = c.ValorCompraAtual ?? 0m,
                ValorAluguel = c.ValorAluguelAtual ?? 0m,
                QtdCasas = c.QtdCasas,
                QtdHoteis = c.QtdHoteis,
                CorHexadecimal = c.CorHexadecimal
            }).ToList();
        }

        /// <summary>
        /// Embaralha as posições das casas do tabuleiro (efeito Inverter).
        /// Apenas reordena a coluna Posicao entre os registros; os peões ficam
        /// nas mesmas posições numéricas e a tela re-renderiza pelas novas posições.
        /// </summary>
        private static async Task EmbaralharTabuleiroAsync(
            AppDbContext context, int idPartida, CancellationToken cancellationToken)
        {
            var casas = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida)
                .ToListAsync(cancellationToken);

            if (casas.Count <= 1)
                return;

            // posições atuais, embaralhadas
            var posicoes = casas.Select(c => c.Posicao).ToList();
            for (int i = posicoes.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (posicoes[i], posicoes[j]) = (posicoes[j], posicoes[i]);
            }

            // Etapa 1: move todas para posições temporárias negativas (únicas),
            // evitando colisão caso a coluna Posicao tenha índice/constraint UNIQUE.
            for (int i = 0; i < casas.Count; i++)
            {
                casas[i].Posicao = -(i + 1);
                casas[i].DataAtualizacao = DateTime.Now;
            }
            await context.SaveChangesAsync(cancellationToken);

            // Etapa 2: aplica as posições finais embaralhadas.
            for (int i = 0; i < casas.Count; i++)
            {
                casas[i].Posicao = posicoes[i];
                casas[i].DataAtualizacao = DateTime.Now;
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Lista as CIDADES sem dono (do banco) para o efeito "pegar do banco".
        /// Cidades ocultas aparecem como misteriosas ("Casa X", sem valores).
        /// </summary>
        public async Task<List<PropriedadeSelecionavelDTO>> ListarCidadesBancoAsync(
            int idPartida,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var cidades = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                         && c.TipoCasaId == (int)TipoCasa.Cidade
                         && c.ProprietarioId <= 0)
                .OrderBy(c => c.Posicao)
                .ToListAsync(cancellationToken);

            return cidades.Select(c => new PropriedadeSelecionavelDTO
            {
                Posicao = c.Posicao,
                PartidaTabuleiroId = c.PartidaTabuleiroId,
                Nome = c.IsRevelada ? c.Nome : $"Casa {c.Posicao} (misteriosa)",
                Imagem = c.IsRevelada ? c.Imagem : null,
                TipoCasaId = c.TipoCasaId,
                ProprietarioId = c.ProprietarioId,
                NomeProprietario = "Banco",
                ValorCompra = c.IsRevelada ? (c.ValorCompraAtual ?? 0m) : 0m,
                ValorAluguel = c.IsRevelada ? (c.ValorAluguelAtual ?? 0m) : 0m,
                QtdCasas = c.QtdCasas,
                QtdHoteis = c.QtdHoteis,
                CorHexadecimal = c.IsRevelada ? c.CorHexadecimal : null
            }).ToList();
        }

        /// <summary>
        /// Lista as propriedades de OUTROS jogadores (para o efeito "roubar").
        /// Mostra o nome do dono atual.
        /// </summary>
        public async Task<List<PropriedadeSelecionavelDTO>> ListarPropriedadesJogadoresAsync(
            int idPartida,
            int idJogadorAtual,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var props = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                         && c.ProprietarioId > 0
                         && c.ProprietarioId != idJogadorAtual)
                .OrderBy(c => c.Posicao)
                .ToListAsync(cancellationToken);

            var donos = await context.Set<Tb_PartidaJogador>()
                .Where(j => j.IdPartida == idPartida)
                .ToListAsync(cancellationToken);

            return props.Select(c => new PropriedadeSelecionavelDTO
            {
                Posicao = c.Posicao,
                PartidaTabuleiroId = c.PartidaTabuleiroId,
                Nome = c.Nome,
                Imagem = c.Imagem,
                TipoCasaId = c.TipoCasaId,
                ProprietarioId = c.ProprietarioId,
                NomeProprietario = donos.FirstOrDefault(d => d.IdPartidaJogador == c.ProprietarioId)?.NomeJogador ?? "Jogador",
                ValorCompra = c.ValorCompraAtual ?? 0m,
                ValorAluguel = c.ValorAluguelAtual ?? 0m,
                QtdCasas = c.QtdCasas,
                QtdHoteis = c.QtdHoteis,
                CorHexadecimal = c.CorHexadecimal
            }).ToList();
        }

        /// <summary>
        /// Pega uma cidade sem dono do banco (de graça). Muda o dono e revela.
        /// Retorna o nome (revelado) da cidade.
        /// </summary>
        public async Task<string> PegarPropriedadeBancoAsync(
            int idPartida,
            int idPartidaJogador,
            int partidaTabuleiroId,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaTabuleiroId == partidaTabuleiroId && c.PartidaId == idPartida, cancellationToken);
            if (casa is null)
                throw new InvalidOperationException("Propriedade não encontrada.");

            if (casa.TipoCasaId != (int)TipoCasa.Cidade || casa.ProprietarioId > 0)
                throw new InvalidOperationException("Esta propriedade não está disponível no banco.");

            casa.ProprietarioId = idPartidaJogador;
            casa.IsRevelada = true;
            casa.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return casa.Nome;
        }

        /// <summary>
        /// Rouba uma propriedade de outro jogador (de graça). Apenas muda o dono;
        /// casas/hotéis e valores acompanham a propriedade.
        /// Retorna o nome da propriedade roubada.
        /// </summary>
        public async Task<string> RoubarPropriedadeAsync(
            int idPartida,
            int idPartidaJogador,
            int partidaTabuleiroId,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaTabuleiroId == partidaTabuleiroId && c.PartidaId == idPartida, cancellationToken);
            if (casa is null)
                throw new InvalidOperationException("Propriedade não encontrada.");

            if (casa.ProprietarioId <= 0 || casa.ProprietarioId == idPartidaJogador)
                throw new InvalidOperationException("Esta propriedade não pode ser roubada.");

            casa.ProprietarioId = idPartidaJogador;
            casa.IsRevelada = true;
            casa.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return casa.Nome;
        }

        /// <summary>
        /// Pega uma cidade aleatória do banco (uso do bot). Retorna o nome ou null
        /// se não havia cidade disponível.
        /// </summary>
        public async Task<string?> PegarPropriedadeBancoAleatorioAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var cidades = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                         && c.TipoCasaId == (int)TipoCasa.Cidade
                         && c.ProprietarioId <= 0)
                .ToListAsync(cancellationToken);

            if (cidades.Count == 0)
                return null;

            var escolhida = cidades[Random.Shared.Next(cidades.Count)];
            escolhida.ProprietarioId = idPartidaJogador;
            escolhida.IsRevelada = true;
            escolhida.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return escolhida.Nome;
        }

        /// <summary>
        /// Rouba uma propriedade aleatória de outro jogador (uso do bot). Retorna o
        /// nome ou null se não havia o que roubar.
        /// </summary>
        public async Task<string?> RoubarPropriedadeAleatorioAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var props = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                         && c.ProprietarioId > 0
                         && c.ProprietarioId != idPartidaJogador)
                .ToListAsync(cancellationToken);

            if (props.Count == 0)
                return null;

            var escolhida = props[Random.Shared.Next(props.Count)];
            escolhida.ProprietarioId = idPartidaJogador;
            escolhida.IsRevelada = true;
            escolhida.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return escolhida.Nome;
        }

        /// <summary>
        /// Jogador disponível para o efeito "trocar de casa" (outros jogadores ativos).
        /// </summary>
        public sealed record JogadorTrocaDTO(
            int IdPartidaJogador,
            string NomeJogador,
            int PosicaoAtual,
            bool EhBot);

        /// <summary>
        /// Lista os outros jogadores ativos (humanos e bots) para o efeito "trocar
        /// de casa". Exclui o próprio jogador e os eliminados/removidos.
        /// </summary>
        public async Task<List<JogadorTrocaDTO>> ListarJogadoresParaTrocaAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogadores = await context.Set<Tb_PartidaJogador>()
                .Where(j => j.IdPartida == idPartida
                         && j.IdPartidaJogador != idPartidaJogador
                         && j.IdStatusJogador != (int)StatusJogadorPartida.Eliminado
                         && j.IdStatusJogador != (int)StatusJogadorPartida.Removido)
                .OrderBy(j => j.OrdemTurno)
                .ToListAsync(cancellationToken);

            return jogadores.Select(j => new JogadorTrocaDTO(
                j.IdPartidaJogador,
                j.NomeJogador,
                j.PosicaoAtual,
                j.TipoJogador == (int)TipoJogadorPartida.Bot)).ToList();
        }

        /// <summary>
        /// Troca a POSIÇÃO entre dois jogadores (efeito "trocar de casa").
        /// Apenas as posições mudam; nada de propriedades/saldo. Retorna a nova
        /// posição do jogador que usou o efeito.
        /// </summary>
        public async Task<int> TrocarDeCasaAsync(
            int idPartida,
            int idPartidaJogador,
            int idPartidaJogadorAlvo,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var eu = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            var alvo = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogadorAlvo && j.IdPartida == idPartida, cancellationToken);

            if (eu is null || alvo is null)
                throw new InvalidOperationException("Jogador não encontrado para a troca.");

            var minhaPos = eu.PosicaoAtual;
            eu.PosicaoAtual = alvo.PosicaoAtual;
            alvo.PosicaoAtual = minhaPos;

            eu.DataAtualizacao = DateTime.Now;
            alvo.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return eu.PosicaoAtual;
        }

        /// <summary>
        /// Troca de posição com um jogador aleatório (uso do bot). Retorna o nome
        /// do jogador trocado ou null se não havia com quem trocar.
        /// </summary>
        public async Task<string?> TrocarDeCasaAleatorioAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var outros = await context.Set<Tb_PartidaJogador>()
                .Where(j => j.IdPartida == idPartida
                         && j.IdPartidaJogador != idPartidaJogador
                         && j.IdStatusJogador != (int)StatusJogadorPartida.Eliminado
                         && j.IdStatusJogador != (int)StatusJogadorPartida.Removido)
                .ToListAsync(cancellationToken);

            if (outros.Count == 0)
                return null;

            var eu = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
            if (eu is null)
                return null;

            var alvo = outros[Random.Shared.Next(outros.Count)];
            var minhaPos = eu.PosicaoAtual;
            eu.PosicaoAtual = alvo.PosicaoAtual;
            alvo.PosicaoAtual = minhaPos;

            eu.DataAtualizacao = DateTime.Now;
            alvo.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);

            return alvo.NomeJogador;
        }

        // ===== Trocar grupo de propriedade (efeito 17) =====

        /// <summary>Grupo do tabuleiro: id + frase (Efeito.Frase referenciado pelo GrupoId).</summary>
        public sealed record GrupoTabuleiroDTO(int GrupoId, string? Frase, string? Cor);

        /// <summary>
        /// Lista as CIDADES com grupo (qualquer dono). Casas não reveladas aparecem
        /// como misteriosas (sem dados). Usa o DTO genérico de seleção.
        /// </summary>
        public async Task<List<PropriedadeSelecionavelDTO>> ListarCidadesComGrupoAsync(
            int idPartida,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var cidades = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida
                         && c.TipoCasaId == (int)TipoCasa.Cidade
                         && c.GrupoId != null)
                .OrderBy(c => c.Posicao)
                .ToListAsync(cancellationToken);

            return cidades.Select(c => new PropriedadeSelecionavelDTO
            {
                Posicao = c.Posicao,
                PartidaTabuleiroId = c.PartidaTabuleiroId,
                Nome = c.IsRevelada ? c.Nome : $"Casa {c.Posicao} (misteriosa)",
                Imagem = c.IsRevelada ? c.Imagem : null,
                TipoCasaId = c.TipoCasaId,
                ProprietarioId = c.ProprietarioId,
                NomeProprietario = c.ProprietarioId > 0 ? "Jogador" : "Banco",
                ValorCompra = c.IsRevelada ? (c.ValorCompraAtual ?? 0m) : 0m,
                ValorAluguel = c.IsRevelada ? (c.ValorAluguelAtual ?? 0m) : 0m,
                QtdCasas = c.QtdCasas,
                QtdHoteis = c.QtdHoteis,
                CorHexadecimal = c.IsRevelada ? c.CorHexadecimal : null,
                GrupoId = c.GrupoId
            }).ToList();
        }

        /// <summary>
        /// Lista os grupos distintos do tabuleiro, com a frase do efeito de grupo
        /// (Efeito referenciado pelo GrupoId) e uma cor representativa.
        /// </summary>
        public async Task<List<GrupoTabuleiroDTO>> ListarGruposTabuleiroAsync(
            int idPartida,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var casasComGrupo = await context.Set<PartidaTabuleiro>()
                .Where(c => c.PartidaId == idPartida && c.GrupoId != null)
                .ToListAsync(cancellationToken);

            var gruposIds = casasComGrupo
                .Select(c => c.GrupoId!.Value)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            if (gruposIds.Count == 0)
                return new List<GrupoTabuleiroDTO>();

            // frase de cada grupo = Frase do efeito referenciado pelo GrupoId
            var efeitos = await context.Set<Efeito>()
                .Where(e => gruposIds.Contains(e.EfeitoId))
                .ToListAsync(cancellationToken);

            var lista = new List<GrupoTabuleiroDTO>();
            foreach (var g in gruposIds)
            {
                var frase = efeitos.FirstOrDefault(e => e.EfeitoId == g)?.Frase;
                // cor: pega a cor de uma casa revelada do grupo (se houver)
                var cor = casasComGrupo
                    .Where(c => c.GrupoId == g && c.IsRevelada && !string.IsNullOrWhiteSpace(c.CorHexadecimal))
                    .Select(c => c.CorHexadecimal)
                    .FirstOrDefault();

                lista.Add(new GrupoTabuleiroDTO(g, frase, cor));
            }

            return lista;
        }

        /// <summary>
        /// Atribui um novo grupo a uma cidade (efeito "trocar grupo"). Os grupos
        /// completos se recalculam naturalmente na tela após recarregar.
        /// </summary>
        public async Task TrocarGrupoPropriedadeAsync(
            int idPartida,
            int partidaTabuleiroId,
            int novoGrupoId,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var casa = await context.Set<PartidaTabuleiro>()
                .FirstOrDefaultAsync(c => c.PartidaTabuleiroId == partidaTabuleiroId && c.PartidaId == idPartida, cancellationToken);
            if (casa is null)
                throw new InvalidOperationException("Propriedade não encontrada.");

            if (casa.TipoCasaId != (int)TipoCasa.Cidade)
                throw new InvalidOperationException("Só cidades possuem grupo.");

            casa.GrupoId = novoGrupoId;
            casa.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Efeito guardado ativo do jogador, para exibição no painel.
        /// </summary>
        public sealed record EfeitoAtivoDTO(
            int IdPartidaJogadorEfeito,
            int EfeitoId,
            string? ColunaEfeito,
            string? Frase,
            string? Imagem,
            byte AcaoEfeitoId,
            byte? SubAlvoEfeitoId,
            bool RemoverAposVolta,
            string Rotulo,
            string Icone);

        /// <summary>
        /// Lista os efeitos guardados ativos de um jogador (para o painel).
        /// </summary>
        public async Task<List<EfeitoAtivoDTO>> ListarEfeitosAtivosAsync(
            int idPartida,
            int idPartidaJogador,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var ativos = await context.Set<PartidaJogadorEfeito>()
                .Where(e => e.IdPartida == idPartida
                         && e.IdPartidaJogador == idPartidaJogador
                         && e.Ativo)
                .OrderBy(e => e.IdPartidaJogadorEfeito)
                .ToListAsync(cancellationToken);

            if (ativos.Count == 0)
                return new List<EfeitoAtivoDTO>();

            var ids = ativos.Select(a => a.EfeitoId).Distinct().ToList();
            var catalogo = await context.Set<Efeito>()
                .Where(e => ids.Contains(e.EfeitoId))
                .ToListAsync(cancellationToken);

            var lista = new List<EfeitoAtivoDTO>();
            foreach (var a in ativos)
            {
                var ef = catalogo.FirstOrDefault(c => c.EfeitoId == a.EfeitoId);
                var acao = ef?.AcaoEfeitoId ?? 0;
                var sub = ef?.SubAlvoEfeitoId;
                var removeVolta = ef?.RemoverAposVolta ?? false;

                lista.Add(new EfeitoAtivoDTO(
                    a.IdPartidaJogadorEfeito,
                    a.EfeitoId,
                    a.ColunaEfeito,
                    ef?.Frase,
                    ef?.Imagem,
                    acao,
                    sub,
                    removeVolta,
                    RotuloEfeito(a.ColunaEfeito, acao, sub),
                    IconeEfeito(acao, sub)));
            }

            return lista;
        }

        /// <summary>
        /// Rótulo curto do efeito guardado, a partir da ColunaEfeito (texto semântico)
        /// com fallback pela ação/subalvo. Genérico, sem id fixo.
        /// </summary>
        private static string RotuloEfeito(string? colunaEfeito, byte acao, byte? sub)
        {
            var chave = (colunaEfeito ?? "").Trim().ToLowerInvariant();

            switch (chave)
            {
                case "imunidade_prisao": return "Imune à prisão";
                case "imunidade_impostos": return "Imune a impostos";
                case "imunidade_efeitos": return "Imune a efeitos";
                case "evitaraluguel": return "Imune ao próximo aluguel";
                case "bloqueiocompra": return "Compra bloqueada";
                case "sem_bonus_volta": return "Sem bônus de volta";
                case "dado_so_impar": return "Dado só ímpar";
                case "dado_so_par": return "Dado só par";
                case "usarrecursosapostas": return "Apostas sem mínimo";
            }

            // fallback genérico pela ação
            switch (acao)
            {
                case EFACAO_IMUNIDADE: return "Imunidade";
                case EFACAO_ALUGUEL: return "Imune ao aluguel";
                case EFACAO_BLOQUEIO: return "Bloqueio";
                case EFACAO_IMPARES: return "Dado só ímpar";
                case EFACAO_PARES: return "Dado só par";
                case EFACAO_APOSTAS: return "Apostas livres";
                default: return "Efeito";
            }
        }

        /// <summary>Ícone Bootstrap para o efeito guardado, pela ação/subalvo.</summary>
        private static string IconeEfeito(byte acao, byte? sub)
        {
            switch (acao)
            {
                case EFACAO_IMUNIDADE:
                    switch (sub)
                    {
                        case 9: return "bi-shield-lock";      // prisão
                        case 8: return "bi-shield-check";     // impostos
                        case 7: return "bi-shield-shaded";    // efeitos
                        default: return "bi-shield";
                    }
                case EFACAO_ALUGUEL: return "bi-house-slash";
                case EFACAO_BLOQUEIO: return "bi-slash-circle";
                case EFACAO_IMPARES: return "bi-dice-3";
                case EFACAO_PARES: return "bi-dice-2";
                case EFACAO_APOSTAS: return "bi-coin";
                default: return "bi-stars";
            }
        }

        /// <summary>
        /// Verifica se o jogador tem um efeito guardado ativo com a ColunaEfeito
        /// informada (comparação case-insensitive). Não consome.
        /// </summary>
        public async Task<bool> TemEfeitoAtivoAsync(
            int idPartida,
            int idPartidaJogador,
            string colunaEfeito,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await TemEfeitoAtivoInternoAsync(context, idPartida, idPartidaJogador, colunaEfeito, cancellationToken);
        }

        private static async Task<bool> TemEfeitoAtivoInternoAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, string colunaEfeito, CancellationToken cancellationToken)
        {
            var alvo = (colunaEfeito ?? "").Trim().ToLower();
            return await context.Set<PartidaJogadorEfeito>()
                .AnyAsync(e => e.IdPartida == idPartida
                            && e.IdPartidaJogador == idPartidaJogador
                            && e.Ativo
                            && e.ColunaEfeito != null
                            && e.ColunaEfeito.ToLower() == alvo, cancellationToken);
        }

        /// <summary>
        /// Consome (desativa) o primeiro efeito guardado ativo do jogador com a
        /// ColunaEfeito informada. Retorna true se havia um efeito e foi consumido.
        /// Genérico: usado por bloqueio de compra e pelas imunidades.
        /// </summary>
        public async Task<bool> ConsumirEfeitoSeAtivoAsync(
            int idPartida,
            int idPartidaJogador,
            string colunaEfeito,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var alvo = (colunaEfeito ?? "").Trim().ToLower();
            var efeito = await context.Set<PartidaJogadorEfeito>()
                .Where(e => e.IdPartida == idPartida
                         && e.IdPartidaJogador == idPartidaJogador
                         && e.Ativo
                         && e.ColunaEfeito != null
                         && e.ColunaEfeito.ToLower() == alvo)
                .OrderBy(e => e.IdPartidaJogadorEfeito)
                .FirstOrDefaultAsync(cancellationToken);

            if (efeito is null)
                return false;

            efeito.Ativo = false;
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }

        // Códigos de regra (ColunaEfeito) usados pelos gatilhos. Texto semântico,
        // não id numérico — alinhado ao cadastro do catálogo.
        public const string EF_BLOQUEIO_COMPRA = "BloqueioCompra";
        public const string EF_IMUNIDADE_PRISAO = "Imunidade_prisao";
        public const string EF_IMUNIDADE_IMPOSTOS = "Imunidade_impostos";
        public const string EF_IMUNIDADE_EFEITOS = "Imunidade_efeitos";
        public const string EF_EVITAR_ALUGUEL = "EvitarAluguel";
        public const string EF_SEM_BONUS_VOLTA = "Sem_bonus_volta";
        public const string EF_DADO_SO_IMPAR = "Dado_so_impar";
        public const string EF_DADO_SO_PAR = "dado_so_Par";
        public const string EF_USAR_APOSTAS = "UsarRecursosApostas";

        /// <summary>
        /// Remove (desativa) o efeito guardado que conflita com o que está sendo
        /// adquirido. Hoje trata dado só par <-> dado só ímpar (substituição).
        /// Genérico: novos conflitos podem ser adicionados ao mapa.
        /// </summary>
        private static async Task RemoverEfeitoConflitanteAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, string? colunaEfeito, CancellationToken cancellationToken)
        {
            var chave = (colunaEfeito ?? "").Trim().ToLower();

            string? oposto = null;
            if (chave == EF_DADO_SO_PAR.ToLower())
                oposto = EF_DADO_SO_IMPAR;
            else if (chave == EF_DADO_SO_IMPAR.ToLower())
                oposto = EF_DADO_SO_PAR;

            if (oposto is null)
                return;

            var alvo = oposto.ToLower();
            var conflitantes = await context.Set<PartidaJogadorEfeito>()
                .Where(e => e.IdPartida == idPartida
                         && e.IdPartidaJogador == idPartidaJogador
                         && e.Ativo
                         && e.ColunaEfeito != null
                         && e.ColunaEfeito.ToLower() == alvo)
                .ToListAsync(cancellationToken);

            foreach (var c in conflitantes)
                c.Ativo = false;
        }

        /// <summary>
        /// Descobre a paridade exigida no dado do jogador por efeito guardado:
        /// 1 = só ímpar, 2 = só par, 0 = sem restrição. Se ambos estiverem ativos
        /// (caso raro), a restrição se anula (0).
        /// </summary>
        private static async Task<int> ObterParidadeDadoAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, CancellationToken cancellationToken)
        {
            var soImpar = await TemEfeitoAtivoInternoAsync(context, idPartida, idPartidaJogador, EF_DADO_SO_IMPAR, cancellationToken);
            var soPar = await TemEfeitoAtivoInternoAsync(context, idPartida, idPartidaJogador, EF_DADO_SO_PAR, cancellationToken);

            if (soImpar && !soPar) return 1;
            if (soPar && !soImpar) return 2;
            return 0;
        }

        /// <summary>
        /// Remove (desativa) todos os efeitos guardados do jogador cujo catálogo
        /// tem RemoverAposVolta = 1. Chamado quando o jogador completa uma volta.
        /// </summary>
        private static async Task ExpirarEfeitosPorVoltaAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, CancellationToken cancellationToken)
        {
            var ativos = await context.Set<PartidaJogadorEfeito>()
                .Where(e => e.IdPartida == idPartida
                         && e.IdPartidaJogador == idPartidaJogador
                         && e.Ativo)
                .ToListAsync(cancellationToken);

            if (ativos.Count == 0)
                return;

            var ids = ativos.Select(a => a.EfeitoId).Distinct().ToList();
            var catalogo = await context.Set<Efeito>()
                .Where(e => ids.Contains(e.EfeitoId))
                .ToListAsync(cancellationToken);

            var expirou = false;
            foreach (var a in ativos)
            {
                var ef = catalogo.FirstOrDefault(c => c.EfeitoId == a.EfeitoId);
                if (ef != null && (ef.RemoverAposVolta ?? false))
                {
                    a.Ativo = false;
                    expirou = true;
                }
            }

            if (expirou)
                await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Consome o bloqueio de compra usando um context já aberto (usado pelo bot).
        /// </summary>
        private static async Task<bool> ConsumirEfeitoBloqueioInternoAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, CancellationToken cancellationToken)
        {
            return await ConsumirEfeitoInternoAsync(context, idPartida, idPartidaJogador, EF_BLOQUEIO_COMPRA, cancellationToken);
        }

        /// <summary>
        /// Consome (desativa) o primeiro efeito guardado ativo com a ColunaEfeito
        /// informada, usando um context já aberto. Retorna true se consumiu.
        /// </summary>
        private static async Task<bool> ConsumirEfeitoInternoAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, string colunaEfeito, CancellationToken cancellationToken)
        {
            var alvo = (colunaEfeito ?? "").Trim().ToLower();
            var efeito = await context.Set<PartidaJogadorEfeito>()
                .Where(e => e.IdPartida == idPartida
                         && e.IdPartidaJogador == idPartidaJogador
                         && e.Ativo
                         && e.ColunaEfeito != null
                         && e.ColunaEfeito.ToLower() == alvo)
                .OrderBy(e => e.IdPartidaJogadorEfeito)
                .FirstOrDefaultAsync(cancellationToken);

            if (efeito is null)
                return false;

            efeito.Ativo = false;
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// Verifica imunidade com context já aberto (sem consumir).
        /// </summary>
        private static async Task<bool> TemImunidadeInternoAsync(
            AppDbContext context, int idPartida, int idPartidaJogador, string colunaEfeito, CancellationToken cancellationToken)
        {
            return await TemEfeitoAtivoInternoAsync(context, idPartida, idPartidaJogador, colunaEfeito, cancellationToken);
        }

        #endregion

        #region Resultados auxiliares

        public sealed record ResultadoPagamentoAluguel(
            bool Pago,
            bool Eliminado,
            decimal ValorPago,
            decimal SaldoFinal,
            bool PrecisaVender = false,
            decimal AluguelDevido = 0m,
            decimal PotencialVenda = 0m,
            bool Imune = false);   // imunidade ao aluguel consumida: não paga

        public sealed record ResultadoConstrucao(
            int QtdCasas,
            int QtdHoteis,
            decimal NovoAluguel,
            decimal CustoPago,
            decimal SaldoFinal);

        #endregion

    }

}
