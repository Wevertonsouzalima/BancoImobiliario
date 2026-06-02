using BancoImobiliario.Data;
using BancoImobiliario.Models;
using DllTeste.Banco.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BancoImobiliario.Services;

public class ConfiguracaoService : ManipulacaoService<AppDbContext, Configuracao>
{
    public ConfiguracaoService(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<ConfiguracaoService> logger)
        : base(contextFactory, logger)
    {
    }

    public async Task<List<Configuracao>> ListarConfiguracoesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ListarAsync(
            ordenarPor: query => query.OrderByDescending(x => x.DataCriacao),
            cancellationToken: cancellationToken);
    }

    public async Task<Configuracao?> BuscarPorIdAsync(
        int idConfiguracao,
        CancellationToken cancellationToken = default)
    {
        if (idConfiguracao <= 0)
            return null;

        return await PrimeiroOuPadraoAsync(
            filtro: x => x.IdConfiguracao == idConfiguracao,
            cancellationToken: cancellationToken);
    }

    public async Task<Configuracao?> BuscarPorCodigoSalaAsync(
        string codigoSala,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSala))
            return null;

        var codigoNormalizado = NormalizarCodigoSala(codigoSala);

        return await PrimeiroOuPadraoAsync(
            filtro: x => x.CodigoSala == codigoNormalizado,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> CodigoSalaExisteAsync(
        string codigoSala,
        int? ignorarIdConfiguracao = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSala))
            return false;

        var codigoNormalizado = NormalizarCodigoSala(codigoSala);

        return await ExisteAsync(
            filtro: x =>
                x.CodigoSala == codigoNormalizado &&
                (!ignorarIdConfiguracao.HasValue || x.IdConfiguracao != ignorarIdConfiguracao.Value),
            cancellationToken: cancellationToken);
    }

    public async Task<Configuracao> CadastrarAsync(
        Configuracao configuracao,
        CancellationToken cancellationToken = default)
    {
        if (configuracao == null)
            throw new ArgumentNullException(nameof(configuracao));

        await PrepararParaCadastroAsync(configuracao, cancellationToken);
        ValidarConfiguracao(configuracao, validarId: false);

        return await ExecutarNoContextoAsync(
            async context =>
            {
                var codigoJaExiste = await context.Configuracoes
                    .AsNoTracking()
                    .AnyAsync(x => x.CodigoSala == configuracao.CodigoSala, cancellationToken);

                if (codigoJaExiste)
                    throw new InvalidOperationException($"Já existe uma configuração com o código de sala '{configuracao.CodigoSala}'.");

                await context.Configuracoes.AddAsync(configuracao, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                return configuracao;
            },
            cancellationToken);
    }

    public async Task<bool> AtualizarConfiguracaoAsync(
        Configuracao configuracao,
        CancellationToken cancellationToken = default)
    {
        if (configuracao == null)
            throw new ArgumentNullException(nameof(configuracao));

        await PrepararParaCadastroAsync(configuracao, cancellationToken);
        ValidarConfiguracao(configuracao, validarId: true);

        return await ExecutarNoContextoAsync(
            async context =>
            {
                var existente = await context.Configuracoes
                    .FirstOrDefaultAsync(x => x.IdConfiguracao == configuracao.IdConfiguracao, cancellationToken);

                if (existente == null)
                    return false;

                var codigoJaExisteEmOutroRegistro = await context.Configuracoes
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.CodigoSala == configuracao.CodigoSala &&
                        x.IdConfiguracao != configuracao.IdConfiguracao,
                        cancellationToken);

                if (codigoJaExisteEmOutroRegistro)
                    throw new InvalidOperationException($"Já existe outra configuração com o código de sala '{configuracao.CodigoSala}'.");

                var dataCriacaoOriginal = existente.DataCriacao;

                context.Entry(existente).CurrentValues.SetValues(configuracao);

                existente.IdConfiguracao = configuracao.IdConfiguracao;
                existente.DataCriacao = dataCriacaoOriginal;
                existente.DataAtualizacao = DateTime.Now;

                var result = await context.SaveChangesAsync(cancellationToken);

                return result > 0;
            },
            cancellationToken);
    }

    public async Task<bool> ExcluirAsync(
        int idConfiguracao,
        CancellationToken cancellationToken = default)
    {
        if (idConfiguracao <= 0)
            throw new ArgumentException("Id da configuração inválido.", nameof(idConfiguracao));

        return await DeletarPorIdAsync(idConfiguracao, cancellationToken);
    }

    public async Task<Configuracao> CriarConfiguracaoPadraoAsync(
        CancellationToken cancellationToken = default)
    {

        return new Configuracao
        {
            CodigoSala = string.Empty,

            TipoJogo = 1,
            QtdJogadores = 4,
            ValorInicial = 1500.00m,

            BonusVolta = 200.00m,
            BonusAteVolta = 0,

            Dificuldade = 1,

            ValMinDado = 1,
            ValMaxDado = 6,
            RestricaoDado = null,

            QtdMaxCasas = 4,
            MinCasasParaHotel = 4,
            QtdMaxHoteis = 1,

            RemoverCasasAposHotel = true,
            TabuleiroOculto = false,

            RegraFronteira = 1,

            EfeitosAleatorios = false,
            NaoRepetirEfeitos = false,

            TipoFinalizacao = 1,
            SubCriterioFinalizacao = null,
            VenceQuem = 1,

            PonderacaoPropriedades = null,
            PonderacaoVoltas = null,
            PonderacaoSaldoFinal = null,
            PonderacaoSaldoTotal = null,

            HabilitarApostas = false,
            ValorAluguelAleatorio = false,
            PermitirSaldoNegativo = false,
            HabilitarNegociacoes = true,
            ValorCompraAleatorio = false,

            PresoBloqueiaAluguel = true,

            PercentualDevolucaoVenda = 50,

            TotalAcumuladoImpostos = 0.00m,

            Host = null,

            StatusJogo = 1,

            DataCriacao = DateTime.Now,
            DataAtualizacao = null
        };
    }

    public async Task<string> GerarCodigoSalaUnicoAsync(
        CancellationToken cancellationToken = default)
    {
        const int tentativasMaximas = 30;

        for (var tentativa = 1; tentativa <= tentativasMaximas; tentativa++)
        {
            var codigo = GerarCodigoSala();

            var existe = await ExisteAsync(
                filtro: x => x.CodigoSala == codigo,
                cancellationToken: cancellationToken);

            if (!existe)
                return codigo;
        }

        throw new InvalidOperationException("Não foi possível gerar um código de sala único.");
    }

    private static string GerarCodigoSala()
    {
        var numero = RandomNumberGenerator.GetInt32(100000, 999999);
        return $"SALA-{numero}";
    }

    private async Task PrepararParaCadastroAsync(
        Configuracao configuracao,
        CancellationToken cancellationToken = default)
    {
        configuracao.IdConfiguracao = 0;

        if (string.IsNullOrWhiteSpace(configuracao.CodigoSala))
            configuracao.CodigoSala = await GerarCodigoSalaUnicoAsync(cancellationToken);
        else
            configuracao.CodigoSala = NormalizarCodigoSala(configuracao.CodigoSala);

        if (configuracao.DataCriacao == default)
            configuracao.DataCriacao = DateTime.Now;

        configuracao.DataAtualizacao = null;
    }

    private static void PrepararParaAtualizacao(Configuracao configuracao)
    {
        configuracao.CodigoSala = NormalizarCodigoSala(configuracao.CodigoSala);
        configuracao.DataAtualizacao = DateTime.Now;
    }

    private static string NormalizarCodigoSala(string? codigoSala)
    {
        return (codigoSala ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
    }

    private static void ValidarConfiguracao(
        Configuracao configuracao,
        bool validarId)
    {
        if (validarId && configuracao.IdConfiguracao <= 0)
            throw new InvalidOperationException("Id da configuração inválido.");

        if (string.IsNullOrWhiteSpace(configuracao.CodigoSala))
            throw new InvalidOperationException("O código da sala é obrigatório.");

        if (configuracao.CodigoSala.Length > 25)
            throw new InvalidOperationException("O código da sala deve ter no máximo 25 caracteres.");

        if (configuracao.TipoJogo <= 0)
            throw new InvalidOperationException("O tipo de jogo é obrigatório.");

        if (configuracao.QtdJogadores <= 0)
            throw new InvalidOperationException("A quantidade de jogadores deve ser maior que zero.");

        if (configuracao.ValorInicial < 0)
            throw new InvalidOperationException("O valor inicial não pode ser negativo.");

        if (configuracao.BonusVolta < 0)
            throw new InvalidOperationException("O bônus por volta não pode ser negativo.");

        if (configuracao.BonusAteVolta < 0)
            throw new InvalidOperationException("O bônus até a volta não pode ser negativo.");

        if (configuracao.Dificuldade <= 0)
            throw new InvalidOperationException("A dificuldade é obrigatória.");


        if (configuracao.ValMaxDado < configuracao.ValMinDado)
            throw new InvalidOperationException("O valor máximo do dado deve ser maior ou igual ao valor mínimo.");

        if (!string.IsNullOrWhiteSpace(configuracao.RestricaoDado) && configuracao.RestricaoDado.Length > 350)
            throw new InvalidOperationException("A restrição do dado deve ter no máximo 350 caracteres.");

        if (configuracao.QtdMaxCasas < 0)
            throw new InvalidOperationException("A quantidade máxima de casas não pode ser negativa.");

        if (configuracao.MinCasasParaHotel < 0)
            throw new InvalidOperationException("A quantidade mínima de casas para hotel não pode ser negativa.");

        if (configuracao.QtdMaxHoteis < 0)
            throw new InvalidOperationException("A quantidade máxima de hotéis não pode ser negativa.");

        if (configuracao.RegraFronteira <= 0)
            throw new InvalidOperationException("A regra de fronteira é obrigatória.");

        if (configuracao.TipoFinalizacao <= 0)
            throw new InvalidOperationException("O tipo de finalização é obrigatório.");

        if (configuracao.VenceQuem <= 0)
            throw new InvalidOperationException("A regra de vitória é obrigatória.");

        if (configuracao.PercentualDevolucaoVenda < 0 || configuracao.PercentualDevolucaoVenda > 100)
            throw new InvalidOperationException("O percentual de devolução da venda deve estar entre 0 e 100.");

        if (configuracao.TotalAcumuladoImpostos < 0)
            throw new InvalidOperationException("O total acumulado de impostos não pode ser negativo.");

        if (!string.IsNullOrWhiteSpace(configuracao.Host) && configuracao.Host.Length > 145)
            throw new InvalidOperationException("O host deve ter no máximo 145 caracteres.");

        ValidarPonderacao(configuracao.PonderacaoPropriedades, "ponderação de propriedades");
        ValidarPonderacao(configuracao.PonderacaoVoltas, "ponderação de voltas");
        ValidarPonderacao(configuracao.PonderacaoSaldoFinal, "ponderação de saldo final");
        ValidarPonderacao(configuracao.PonderacaoSaldoTotal, "ponderação de saldo total");

        if (configuracao.StatusJogo <= 0)
            throw new InvalidOperationException("O status do jogo é obrigatório.");
    }

    private static void ValidarPonderacao(
        int? valor,
        string nomeCampo)
    {
        if (valor.HasValue && valor.Value < 0)
            throw new InvalidOperationException($"A {nomeCampo} não pode ser negativa.");
    }
}