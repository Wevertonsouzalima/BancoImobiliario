using BancoImobiliario.Data;
using BancoImobiliario.Models;
using Microsoft.EntityFrameworkCore;

namespace BancoImobiliario.Services.Tabuleiro
{
    public class GerenciamentoTabuleiroService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GerenciamentoTabuleiroService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Models.Tabuleiros>> ObterTodosAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Set<Models.Tabuleiros>()
                .OrderByDescending(t => t.DataCriacao)
                .ToListAsync(cancellationToken);
        }

        public async Task<Models.Tabuleiros?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Set<Models.Tabuleiros>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TabuleiroId == id, cancellationToken);
        }

        public async Task<bool> VerificarPossuiLayoutAsync(int idTabuleiro, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Set<LayoutTabuleiro>()
                .AnyAsync(l => l.TabuleiroId == idTabuleiro, cancellationToken);
        }

        public async Task<Models.Tabuleiros> SalvarAsync(Models.Tabuleiros tabuleiro, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            if (tabuleiro.TabuleiroId > 0)
            {
                var tabBanco = await context.Set<Models.Tabuleiros>()
                    .FindAsync(new object[] { tabuleiro.TabuleiroId }, cancellationToken);

                if (tabBanco == null)
                    throw new InvalidOperationException("Tabuleiro não encontrado na base de dados.");

                var possuiLayout = await VerificarPossuiLayoutAsync(tabuleiro.TabuleiroId, cancellationToken);

                // Validação de segurança: Impede alteração de estrutura se o layout já existe
                if (possuiLayout &&
                    (tabBanco.QtdCasas != tabuleiro.QtdCasas ||
                     tabBanco.LarguraGrade != tabuleiro.LarguraGrade ||
                     tabBanco.AlturaGrade != tabuleiro.AlturaGrade))
                {
                    throw new InvalidOperationException("Ação bloqueada! As dimensões e quantidade de casas não podem ser modificadas pois este tabuleiro já possui um layout desenhado.");
                }

                tabBanco.Nome = tabuleiro.Nome;
                tabBanco.TamanhoCasaPx = tabuleiro.TamanhoCasaPx;

                // Se não tem layout, permite atualizar as proporções normalmente
                if (!possuiLayout)
                {
                    tabBanco.QtdCasas = tabuleiro.QtdCasas;
                    tabBanco.LarguraGrade = tabuleiro.LarguraGrade;
                    tabBanco.AlturaGrade = tabuleiro.AlturaGrade;
                }

                context.Set<Models.Tabuleiros>().Update(tabBanco);
                await context.SaveChangesAsync(cancellationToken);
                return tabBanco;
            }
            else
            {
                tabuleiro.DataCriacao = DateTime.Now;
                context.Set<Models.Tabuleiros>().Add(tabuleiro);
                await context.SaveChangesAsync(cancellationToken);
                return tabuleiro;
            }
        }
        public async Task SalvarLayoutAsync(int idTabuleiro, List<LayoutTabuleiro> novoLayout, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Remove as posições antigas deste tabuleiro
                var layoutAntigo = await context.Set<LayoutTabuleiro>()
                    .Where(l => l.TabuleiroId == idTabuleiro)
                    .ToListAsync(cancellationToken);

                if (layoutAntigo.Any())
                {
                    context.Set<LayoutTabuleiro>().RemoveRange(layoutAntigo);
                }

                // 2. Insere as novas posições
                context.Set<LayoutTabuleiro>().AddRange(novoLayout);

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        // ---- MÉTODOS PARA GESTÃO DE FRONTEIRAS ----

        public async Task<List<TabuleiroFronteira>> ObterFronteirasAsync(int idTabuleiro, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Set<TabuleiroFronteira>()
                .Where(f => f.TabuleiroId == idTabuleiro)
                .OrderBy(f => f.PosicaoOrigem)
                .ToListAsync(cancellationToken);
        }

        public async Task SalvarFronteiraAsync(TabuleiroFronteira fronteira, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Evita duplicidade da mesma rota
            var existe = await context.Set<TabuleiroFronteira>()
                .AnyAsync(f => f.TabuleiroId == fronteira.TabuleiroId &&
                               f.PosicaoOrigem == fronteira.PosicaoOrigem &&
                               f.PosicaoDestino == fronteira.PosicaoDestino, cancellationToken);

            if (existe) throw new InvalidOperationException("Essa rota já existe neste tabuleiro.");

            fronteira.DataCriacao = DateTime.Now;
            context.Set<TabuleiroFronteira>().Add(fronteira);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ExcluirFronteiraAsync(int idFronteira, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var fronteira = await context.Set<TabuleiroFronteira>().FindAsync(new object[] { idFronteira }, cancellationToken);

            if (fronteira != null)
            {
                context.Set<TabuleiroFronteira>().Remove(fronteira);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        public async Task SortearESalvarFronteirasPartidaAsync(int idTabuleiro, int idPartida, int quantidadeDesejada, CancellationToken cancellationToken = default)
        {
            if (quantidadeDesejada <= 0) return;

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // 1. Busca o catálogo de fronteiras que desenhamos no Editor Visual
            var catalogoFronteiras = await context.Set<TabuleiroFronteira>()
                .Where(f => f.TabuleiroId == idTabuleiro)
                .ToListAsync(cancellationToken);

            if (!catalogoFronteiras.Any()) return;

            // 2. Embaralha as fronteiras para dar aleatoriedade e pega apenas a quantidade definida no slider
            var random = new Random();
            var fronteirasSorteadas = catalogoFronteiras
                .OrderBy(x => random.Next())
                .Take(quantidadeDesejada)
                .ToList();

            // 3. Converte para o modelo da partida
            var fronteirasDaPartida = fronteirasSorteadas.Select(f => new PartidaFronteira
            {
                IdPartida = idPartida,
                PosicaoOrigem = f.PosicaoOrigem,
                PosicaoDestino = f.PosicaoDestino,
                ValorTravessia = f.ValorTravessia,
                EfeitoRequeridoId = f.EfeitoRequeridoId,
                DataCriacao = DateTime.Now
            }).ToList();

            // 4. Salva no banco as fronteiras oficias deste jogo
            context.Set<PartidaFronteira>().AddRange(fronteirasDaPartida);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}