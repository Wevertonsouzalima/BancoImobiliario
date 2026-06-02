using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Jogo;

[Table("Tb_PartidaEventos", Schema = "Jogo")]
public class Tb_PartidaEvento
{
    [Key]
    public int IdPartidaEvento { get; set; }

    public int IdPartida { get; set; }

    public int? IdPartidaJogador { get; set; }

    public int TipoEvento { get; set; }

    [MaxLength(500)]
    public string? Descricao { get; set; }

    public string? DadosJson { get; set; }

    public DateTime DataEvento { get; set; }
}