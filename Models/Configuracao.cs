using System.ComponentModel.DataAnnotations;

namespace BancoImobiliario.Models;

public class Configuracao
{
    public int IdConfiguracao { get; set; }

    [Required]
    [MaxLength(25)]
    public string CodigoSala { get; set; } = string.Empty;

    public int TipoJogo { get; set; }
    public int QtdJogadores { get; set; }

    public decimal ValorInicial { get; set; }
    public decimal BonusVolta { get; set; }
    public int BonusAteVolta { get; set; }

    public int Dificuldade { get; set; }

    public int ValMinDado { get; set; }
    public int ValMaxDado { get; set; }

    [MaxLength(350)]
    public string? RestricaoDado { get; set; }

    public int QtdMaxCasas { get; set; }
    public int MinCasasParaHotel { get; set; }
    public int QtdMaxHoteis { get; set; }

    public bool RemoverCasasAposHotel { get; set; }
    public bool TabuleiroOculto { get; set; }

    public int RegraFronteira { get; set; }

    public bool EfeitosAleatorios { get; set; }
    public bool NaoRepetirEfeitos { get; set; }

    public int TipoFinalizacao { get; set; }
    public int? SubCriterioFinalizacao { get; set; }
    public int VenceQuem { get; set; }

    public int? PonderacaoPropriedades { get; set; }
    public int? PonderacaoVoltas { get; set; }
    public int? PonderacaoSaldoFinal { get; set; }
    public int? PonderacaoSaldoTotal { get; set; }

    public bool HabilitarApostas { get; set; }
    public bool ValorAluguelAleatorio { get; set; }
    public bool PermitirSaldoNegativo { get; set; }
    public bool HabilitarNegociacoes { get; set; }
    public bool ValorCompraAleatorio { get; set; }

    public bool PresoBloqueiaAluguel { get; set; }

    public int PercentualDevolucaoVenda { get; set; }

    public decimal TotalAcumuladoImpostos { get; set; }

    [MaxLength(145)]
    public string? Host { get; set; }

    public int StatusJogo { get; set; }
    public int IdTabuleiro { get; set; } = 70;
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
}