using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Jogo
{
    /// <summary>
    /// Efeito guardado (status) que um jogador carrega durante a partida.
    /// A regra é identificada por ColunaEfeito; a expiração por volta vem do
    /// catálogo (Tb_Efeitos.RemoverAposVolta).
    /// </summary>
    [Table("Tb_PartidaJogadorEfeito", Schema = "Jogo")]
    public class PartidaJogadorEfeito
    {
        [Key]
        public int IdPartidaJogadorEfeito { get; set; }

        public int IdPartida { get; set; }

        public int IdPartidaJogador { get; set; }

        public int EfeitoId { get; set; }

        public string? ColunaEfeito { get; set; }

        public bool Ativo { get; set; }

        public DateTime DataAquisicao { get; set; }
    }
}