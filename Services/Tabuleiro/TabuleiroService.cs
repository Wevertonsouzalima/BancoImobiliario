using BancoImobiliario.Data;
using BancoImobiliario.Models;
using BancoImobiliario.Models.Casas;
using BancoImobiliario.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BancoImobiliario.Services.Tabuleiro
{
    public class TabuleiroService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly CoresTabuleiroOptions _cores;

        // Fatores do sorteio de valores aleatórios (campo == -1), aplicados SOMENTE a Cidade.
        private const decimal FatorPisoCompra = 0.80m;  // 20% abaixo da cidade mais barata
        private const decimal FatorTetoCompra = 1.40m;  // 40% acima da cidade mais cara

        public TabuleiroService(IDbContextFactory<AppDbContext> contextFactory,
                                IOptions<CoresTabuleiroOptions> cores)
        {
            _contextFactory = contextFactory;
            _cores = cores.Value;
        }

        /// <summary>
        /// Retorna a quantidade total de casas cadastradas de acordo com o tipo solicitado.
        /// </summary>
        public async Task<int> ObterQuantidadeTotalPorTipoAsync(string tipoCasa, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return tipoCasa.ToLower() switch
            {
                "companhias" => await context.Set<Companhia>().CountAsync(cancellationToken),
                "efeitos" => await context.Set<Efeito>().CountAsync(e => e.TipoEfeitoId == (byte)EfeitoTipo.Normal, cancellationToken),
                "grupos_efeito" => await context.Set<Efeito>().CountAsync(e => e.TipoEfeitoId == (byte)EfeitoTipo.Grupo, cancellationToken),
                "especiais" => await context.Set<CasaEspecial>().CountAsync(cancellationToken),
                "impostos" => await context.Set<Imposto>().CountAsync(cancellationToken),
                "prisao" => await context.Set<Prisao>().CountAsync(cancellationToken),
                _ => 0
            };
        }

        /// <summary>
        /// Obtém todos os tabuleiros cadastrados na base de dados ordenados pelo nome.
        /// </summary>
        public async Task<List<Tabuleiros>> ObterTodosTabuleirosAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.Set<Models.Tabuleiros>()
                .OrderBy(t => t.Nome)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Busca os detalhes de um tabuleiro específico com base no seu identificador exclusivo.
        /// </summary>
        public async Task<Models.Tabuleiros?> ObterTabuleiroPorIdAsync(int tabuleiroId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.Set<Models.Tabuleiros>()
                .FirstOrDefaultAsync(t => t.TabuleiroId == tabuleiroId, cancellationToken);
        }

        /// <summary>
        /// Retorna o mapeamento completo de linhas, colunas e posições do grid para o tabuleiro especificado.
        /// </summary>
        public async Task<List<LayoutTabuleiro>> ObterLayoutPorTabuleiroIdAsync(int tabuleiroId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.Set<LayoutTabuleiro>()
                .Where(l => l.TabuleiroId == tabuleiroId)
                .OrderBy(l => l.Posicao)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Embaralha a lista de IDs e retorna no máximo 'limite' itens.
        /// Retorna lista vazia imediatamente se 'limite' for menor ou igual a zero.
        /// </summary>
        private static List<T> SortearIds<T>(IReadOnlyList<T> ids, int limite)
        {
            if (limite <= 0 || ids is null || ids.Count == 0)
                return new List<T>();

            var copia = new List<T>(ids);

            for (int i = copia.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (copia[i], copia[j]) = (copia[j], copia[i]);
            }

            return limite >= copia.Count
                ? copia
                : copia.GetRange(0, limite);
        }

        /// <summary>
        /// Sorteia um valor decimal dentro da faixa [min, max] com duas casas decimais.
        /// </summary>
        private static decimal SortearValorNaFaixa(decimal min, decimal max)
        {
            if (max <= min) return Math.Round(min, 2);

            var fator = (decimal)Random.Shared.NextDouble();
            var valor = min + (max - min) * fator;
            return Math.Round(valor, 2);
        }

        private readonly record struct CasaSorteada(int ReferenciaCatalogoId, TipoCasa Tipo);

        /// <summary>
        /// Ordena as casas respeitando: máximo 3 do mesmo tipo (exceto Cidade) em sequência,
        /// e pelo menos 1 Cidade a cada 6 casas consecutivas (janela deslizante).
        /// </summary>
        private static List<CasaSorteada> MontarOrdemTabuleiro(List<CasaSorteada> casas)
        {
            int total = casas.Count;
            var disponiveis = new List<CasaSorteada>(casas);
            var resultado = new List<CasaSorteada>(total);

            for (int pos = 0; pos < total; pos++)
            {
                TipoCasa? tipoBloqueado = null;
                if (resultado.Count >= 3)
                {
                    var t = resultado[^1].Tipo;
                    if (t != TipoCasa.Cidade && resultado[^2].Tipo == t && resultado[^3].Tipo == t)
                        tipoBloqueado = t;
                }

                bool exigeCidade = false;
                if (resultado.Count >= 5)
                {
                    exigeCidade = resultado[^1].Tipo != TipoCasa.Cidade
                               && resultado[^2].Tipo != TipoCasa.Cidade
                               && resultado[^3].Tipo != TipoCasa.Cidade
                               && resultado[^4].Tipo != TipoCasa.Cidade
                               && resultado[^5].Tipo != TipoCasa.Cidade;
                }

                var candidatos = disponiveis.Where(c =>
                    (!exigeCidade || c.Tipo == TipoCasa.Cidade) &&
                    (tipoBloqueado is null || c.Tipo != tipoBloqueado.Value)
                ).ToList();

                if (candidatos.Count == 0)
                    throw new InvalidOperationException(
                        $"Não foi possível montar o tabuleiro na posição {pos}: " +
                        "não há casas suficientes (provavelmente faltam cidades) para respeitar as regras.");

                var escolhida = candidatos[Random.Shared.Next(candidatos.Count)];
                resultado.Add(escolhida);
                disponiveis.Remove(escolhida);
            }

            return resultado;
        }

        public async Task<int> CriarTabuleiro(int partidaId, int qtdTotal, int qtdEfeitos, int qtdPrisao, int qtdCompanhias, int qtdEspeciais, int qtdImpostos, bool efeitosAleatorio, int qtdGrupos, bool ocultar, bool vlCompraAlt, bool vlAluguelAlt, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

                // ---- Carrega catálogos ----
                var todasCidades = await context.Set<Cidade>().ToListAsync(cancellationToken);
                var todasCompanhias = await context.Set<Companhia>().ToListAsync(cancellationToken);
                var todosImpostos = await context.Set<Imposto>().ToListAsync(cancellationToken);
                var todasPrisoes = await context.Set<Prisao>().ToListAsync(cancellationToken);
                var todasEspeciais = await context.Set<CasaEspecial>().ToListAsync(cancellationToken);
                var todosEfeitos = await context.Set<Efeito>()
                    .Where(e => e.TipoEfeitoId == (byte)EfeitoTipo.Normal)
                    .ToListAsync(cancellationToken);
                var todosGrupos = await context.Set<Efeito>()
                    .Where(e => e.TipoEfeitoId == (byte)EfeitoTipo.Grupo)
                    .ToListAsync(cancellationToken);

                int qtdCidades = qtdTotal - (qtdCompanhias + qtdEfeitos + qtdEspeciais + qtdImpostos + qtdPrisao);

                // ---- Faixas para sorteio do -1 (SOMENTE Cidade, catálogo completo) ----
                // Compra: base em Cidade.ValorVenda. Aluguel: base em Cidade.ValorAluguel.
                decimal pisoCompra = 0m, tetoCompra = 0m, pisoAluguel = 0m, tetoAluguel = 0m;

                if (todasCidades.Count > 0)
                {
                    var minVenda = todasCidades.Min(c => c.ValorVenda);
                    var maxVenda = todasCidades.Max(c => c.ValorVenda);
                    pisoCompra = Math.Round(minVenda * FatorPisoCompra, 2);
                    tetoCompra = Math.Round(maxVenda * FatorTetoCompra, 2);

                    var minAluguel = todasCidades.Min(c => c.ValorAluguel);
                    var maxAluguel = todasCidades.Max(c => c.ValorAluguel);
                    pisoAluguel = Math.Round(minAluguel * FatorPisoCompra, 2);
                    tetoAluguel = Math.Round(maxAluguel * FatorTetoCompra, 2);
                }

                // ---- Sorteio por tipo ----
                var companhias = SortearIds(todasCompanhias, qtdCompanhias);
                var efeitos = SortearIds(todosEfeitos, qtdEfeitos);
                var especiais = SortearIds(todasEspeciais, qtdEspeciais);
                var impostos = SortearIds(todosImpostos, qtdImpostos);
                var prisoes = SortearIds(todasPrisoes, qtdPrisao);
                var cidades = SortearIds(todasCidades, qtdCidades);
                var grupos = SortearIds(todosGrupos, qtdGrupos);

                // ---- Cores: gera uma cor distinta para cada grupo ----
                var coresUsadas = new List<string>
                {
                    _cores.Cidade, _cores.Companhia, _cores.Imposto,
                    _cores.Prisao, _cores.Efeito, _cores.Especial
                };

                var corPorGrupo = new Dictionary<int, string>(grupos.Count);
                foreach (var grupo in grupos)
                {
                    var cor = GeradorCores.GerarCorDistinta(coresUsadas);
                    corPorGrupo[grupo.EfeitoId] = cor;
                    coresUsadas.Add(cor);
                }

                // ---- Atribuição equilibrada de grupos às cidades ----
                var bolsaGrupos = new List<int>(cidades.Count);
                if (grupos.Count > 0)
                {
                    int baseQtd = cidades.Count / grupos.Count;
                    int resto = cidades.Count % grupos.Count;

                    for (int g = 0; g < grupos.Count; g++)
                    {
                        int repeticoes = baseQtd + (g < resto ? 1 : 0);
                        for (int r = 0; r < repeticoes; r++)
                            bolsaGrupos.Add(grupos[g].EfeitoId);
                    }
                }
                bolsaGrupos = SortearIds(bolsaGrupos, bolsaGrupos.Count);

                var grupoPorCidade = new Dictionary<int, int>(cidades.Count);
                for (int i = 0; i < cidades.Count && i < bolsaGrupos.Count; i++)
                    grupoPorCidade[cidades[i].CidadeId] = bolsaGrupos[i];

                // ---- Junta todas as casas ----
                var casas = new List<CasaSorteada>();
                casas.AddRange(cidades.Select(c => new CasaSorteada(c.CidadeId, TipoCasa.Cidade)));
                casas.AddRange(companhias.Select(c => new CasaSorteada(c.CompanhiaId, TipoCasa.Companhia)));
                casas.AddRange(impostos.Select(c => new CasaSorteada(c.ImpostoId, TipoCasa.Imposto)));
                casas.AddRange(prisoes.Select(c => new CasaSorteada(c.PrisaoId, TipoCasa.Prisao)));
                casas.AddRange(especiais.Select(c => new CasaSorteada(c.CasasEspeciaisID, TipoCasa.Especial)));
                casas.AddRange(efeitos.Select(c => new CasaSorteada(c.EfeitoId, TipoCasa.Efeito)));

                // ---- Ordena respeitando as regras ----
                var ordenadas = MontarOrdemTabuleiro(casas);

                // ---- Lookups ----
                var mapCidade = todasCidades.ToDictionary(x => x.CidadeId);
                var mapCompanhia = todasCompanhias.ToDictionary(x => x.CompanhiaId);
                var mapImposto = todosImpostos.ToDictionary(x => x.ImpostoId);
                var mapPrisao = todasPrisoes.ToDictionary(x => x.PrisaoId);
                var mapEspecial = todasEspeciais.ToDictionary(x => x.CasasEspeciaisID);
                var mapEfeito = todosEfeitos.ToDictionary(x => x.EfeitoId);

                // ---- Monta PartidaTabuleiro ----
                var tabuleiro = new List<PartidaTabuleiro>(ordenadas.Count);

                for (int i = 0; i < ordenadas.Count; i++)
                {
                    var casa = ordenadas[i];

                    string nome = string.Empty;
                    string? imagem = null;
                    decimal? valorCompra = null;
                    decimal? valorAluguel = null;
                    int? grupoId = null;
                    string? cor = _cores.CorPorTipo(casa.Tipo);

                    switch (casa.Tipo)
                    {
                        case TipoCasa.Cidade:
                            var ci = mapCidade[casa.ReferenciaCatalogoId];
                            nome = ci.Nome;
                            imagem = ci.Imagem;
                            valorCompra = ci.ValorVenda;
                            valorAluguel = ci.ValorAluguel;
                            if (grupoPorCidade.TryGetValue(ci.CidadeId, out var gId))
                            {
                                grupoId = gId;
                                if (corPorGrupo.TryGetValue(gId, out var corGrupo))
                                    cor = corGrupo;
                            }
                            break;
                        case TipoCasa.Companhia:
                            var co = mapCompanhia[casa.ReferenciaCatalogoId];
                            nome = co.Nome;
                            imagem = co.Imagem;
                            valorCompra = co.ValorCompra;
                            break;
                        case TipoCasa.Imposto:
                            var im = mapImposto[casa.ReferenciaCatalogoId];
                            nome = im.Nome;
                            imagem = im.Imagem;
                            break;
                        case TipoCasa.Prisao:
                            var pr = mapPrisao[casa.ReferenciaCatalogoId];
                            nome = pr.Nome;
                            imagem = pr.Imagem;
                            break;
                        case TipoCasa.Especial:
                            var es = mapEspecial[casa.ReferenciaCatalogoId];
                            nome = es.Nome;
                            imagem = es.Imagem;
                            break;
                        case TipoCasa.Efeito:
                            var ef = mapEfeito[casa.ReferenciaCatalogoId];
                            nome = "Efeito";
                            if (nome.Length > 100) nome = nome.Substring(0, 100);
                            imagem = ef.Imagem;
                            break;
                    }

                    // ---- Valores finais de compra/aluguel ----
                    // Se o modo aleatório está ligado, o valor inicial é o sentinela -1.
                    // O sorteio do -1 vale SOMENTE para Cidade; companhia mantém o catálogo.
                    decimal? valorCompraFinal = vlCompraAlt ? -1m : valorCompra;
                    decimal? valorAluguelFinal = vlAluguelAlt ? -1m : valorAluguel;

                    if (casa.Tipo == TipoCasa.Cidade && todasCidades.Count > 0)
                    {
                        if (valorCompraFinal == -1m)
                            valorCompraFinal = SortearValorNaFaixa(pisoCompra, tetoCompra);

                        if (valorAluguelFinal == -1m)
                            valorAluguelFinal = SortearValorNaFaixa(pisoAluguel, tetoAluguel);
                    }

                    tabuleiro.Add(new PartidaTabuleiro
                    {
                        Posicao = i + 1,
                        TipoCasaId = (byte)casa.Tipo,
                        ReferenciaCatalogoId = casa.ReferenciaCatalogoId,
                        Nome = nome,
                        Imagem = imagem,
                        ProprietarioId = -1,
                        CorHexadecimal = cor,
                        GrupoId = grupoId,
                        ValorCompraAtual = valorCompraFinal,
                        ValorAluguelAtual = valorAluguelFinal,
                        IsRevelada = !ocultar,
                        DataAtualizacao = DateTime.Now,
                        QtdCasas = 0,
                        QtdHoteis = 0,
                        PartidaId = partidaId,
                    });
                }

                context.Set<PartidaTabuleiro>().AddRange(tabuleiro);
                await context.SaveChangesAsync(cancellationToken);

                return 1;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}