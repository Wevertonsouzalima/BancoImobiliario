using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Jogo;

[Table("Tb_PartidaOrdemRolagens", Schema = "Jogo")]
public class Tb_PartidaOrdemRolagem
{
    [Key]
    public int IdOrdemRolagem { get; set; }

    public int IdPartida { get; set; }

    public int IdPartidaJogador { get; set; }

    public int RodadaDesempate { get; set; } = 1;

    public int GrupoDesempate { get; set; } = 1;

    public int ResultadoDado { get; set; }

    public bool Automatico { get; set; }

    public DateTime DataRolagem { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}