using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Jogo;

[Table("Tb_BotPerfis", Schema = "Jogo")]
public class Tb_BotPerfil
{
    [Key]
    public int IdPerfilBot { get; set; }

    [Required, MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Descricao { get; set; }

    public int DificuldadePadrao { get; set; } = 2;

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; }

    public DateTime? DataAtualizacao { get; set; }
}