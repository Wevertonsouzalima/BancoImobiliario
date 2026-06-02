using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Jogo;

[Table("Tb_Partidas", Schema = "Jogo")]
public class Tb_Partida
{
    [Key]
    public int IdPartida { get; set; }
    public int RodadaDesempateAtual { get; set; } = 1;

    public int IdConfiguracao { get; set; }

    [Required, MaxLength(25)]
    public string CodigoSala { get; set; } = string.Empty;

    public int TipoJogo { get; set; }

    public int IdStatusPartida { get; set; } = 1;

    public int QtdMaxJogadores { get; set; }

    [MaxLength(120)]
    public string? HostNome { get; set; }

    [MaxLength(150)]
    public string? HostIdentificador { get; set; }

    public DateTime DataCriacao { get; set; }

    public DateTime? DataInicio { get; set; }

    public DateTime? DataFim { get; set; }

    public DateTime? DataExpiracaoLobby { get; set; }

    public DateTime? DataAtualizacao { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}