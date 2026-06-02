using System.Globalization;
using Microsoft.EntityFrameworkCore;

using BancoImobiliario.Data;          // AppDbContext
using BancoImobiliario.Models.Enums;  // TipoCasa
using BancoImobiliario.Models.Jogo;   // Tb_PartidaJogador, DadosCasaPopup, VisibilidadeCasa, FronteiraCasa
using BancoImobiliario.Models.Casas;  // PartidaTabuleiro, PartidaFronteira
using BancoImobiliario.Models;        // Cidade, Efeito (ajuste se estiverem em outro namespace)

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
                    resultado = ProcessarImposto(casa);
                    break;

                case TipoCasa.Prisao:
                    casa.IsRevelada = true;
                    resultado = ProcessarPrisao(casa);
                    break;

                case TipoCasa.Efeito:
                    casa.IsRevelada = true;
                    resultado = ProcessarEfeito(casa);
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

            if (casa.ProprietarioId.HasValue && casa.ProprietarioId.Value > 0)
                throw new InvalidOperationException("Esta propriedade já possui dono.");

            var preco = casa.ValorCompraAtual ?? 0m;

            // TODO: respeitar PermitirSaldoNegativo (Configuracao) antes de aprovar a compra.
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

        /// <summary>
        /// Retorna a entidade do jogador da partida (estado real: saldo, posição, voltas, prisão).
        /// </summary>
        public async Task<Tb_PartidaJogador?> ObterJogadorAsync(int idPartida, int idPartidaJogador, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);
        }

        /// <summary>
        /// Define o estado de turno do jogador (aguardando, jogando dado, resolvendo casa, concluído).
        /// </summary>
        public async Task DefinirEstadoTurnoAsync(int idPartida, int idPartidaJogador, EstadoTurnoJogador estado, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var jogador = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == idPartidaJogador && j.IdPartida == idPartida, cancellationToken);

            if (jogador is null)
                throw new InvalidOperationException("Jogador da partida não encontrado.");

            jogador.EstadoTurno = (int)estado;

            // Ao iniciar a resolução da casa, ainda não pode finalizar; ao concluir, pode.
            if (estado == EstadoTurnoJogador.ResolvendoCasa)
                jogador.PodeFinalizar = false;
            else if (estado == EstadoTurnoJogador.TurnoConcluido)
                jogador.PodeFinalizar = true;

            jogador.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Conclui a ação pendente da casa (pagamento/efeito já aplicado) e libera a finalização.
        /// </summary>
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

            var temDono = casa.ProprietarioId.HasValue && casa.ProprietarioId.Value > 0;
            var tipo = (TipoCasa)casa.TipoCasaId;
            var ehPropriedade = tipo == TipoCasa.Cidade || tipo == TipoCasa.Companhia;
            var jogadorEstaNaCasa = jogadorDaVez is not null && jogadorDaVez.PosicaoAtual == casa.Posicao;
            var ehDonoJogadorDaVez = temDono && jogadorDaVez is not null && casa.ProprietarioId!.Value == jogadorDaVez.IdPartidaJogador;

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
                    .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId!.Value && j.IdPartida == idPartida, cancellationToken);
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
                ProprietarioId = temDono ? casa.ProprietarioId : null,
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
            var temDono = casa.ProprietarioId.HasValue && casa.ProprietarioId.Value > 0;

            if (!temDono)
            {
                return new ResultadoCairNaCasa(
                    TipoAcaoCasa.PodeComprar,
                    $"{casa.Nome} está à venda por {Moeda(casa.ValorCompraAtual)}.",
                    Valor: casa.ValorCompraAtual,
                    PartidaTabuleiroId: casa.PartidaTabuleiroId,
                    TipoCasaId: casa.TipoCasaId);
            }

            if (casa.ProprietarioId!.Value == jogador.IdPartidaJogador)
            {
                return new ResultadoCairNaCasa(
                    TipoAcaoCasa.PodeConstruir,
                    $"Você é o dono de {casa.Nome}. Pode construir casa/hotel.",
                    ProprietarioId: jogador.IdPartidaJogador,
                    PartidaTabuleiroId: casa.PartidaTabuleiroId,
                    TipoCasaId: casa.TipoCasaId);
            }

            var dono = await context.Set<Tb_PartidaJogador>()
                .FirstOrDefaultAsync(j => j.IdPartidaJogador == casa.ProprietarioId.Value
                                          && j.IdPartida == jogador.IdPartida, cancellationToken);

            // TODO: se PresoBloqueiaAluguel (Configuracao) e dono.EstaPreso => aluguel = 0.
            var aluguel = casa.ValorAluguelAtual ?? 0m;

            // TODO: respeitar PermitirSaldoNegativo / disparar falência se saldo insuficiente.
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

        private ResultadoCairNaCasa ProcessarImposto(PartidaTabuleiro casa)
            => new ResultadoCairNaCasa(TipoAcaoCasa.ADefinir,
                $"Caiu em {casa.Nome} (Imposto). Regra de cobrança a definir.",
                PartidaTabuleiroId: casa.PartidaTabuleiroId, TipoCasaId: casa.TipoCasaId);

        private ResultadoCairNaCasa ProcessarPrisao(PartidaTabuleiro casa)
            => new ResultadoCairNaCasa(TipoAcaoCasa.ADefinir,
                $"Caiu em {casa.Nome} (Prisão). Regra de prisão a definir.",
                PartidaTabuleiroId: casa.PartidaTabuleiroId, TipoCasaId: casa.TipoCasaId);

        private ResultadoCairNaCasa ProcessarEfeito(PartidaTabuleiro casa)
            => new ResultadoCairNaCasa(TipoAcaoCasa.ADefinir,
                $"Caiu em {casa.Nome} (Efeito). Sistema de efeitos a definir.",
                PartidaTabuleiroId: casa.PartidaTabuleiroId, TipoCasaId: casa.TipoCasaId);

        private ResultadoCairNaCasa ProcessarEspecial(PartidaTabuleiro casa)
            => new ResultadoCairNaCasa(TipoAcaoCasa.ADefinir,
                $"Caiu em {casa.Nome} (Especial). Regra a definir.",
                PartidaTabuleiroId: casa.PartidaTabuleiroId, TipoCasaId: casa.TipoCasaId);

        // ========================================
        // HELPERS
        // ========================================

        private static string Moeda(decimal? valor)
            => (valor ?? 0m).ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
    }
}
