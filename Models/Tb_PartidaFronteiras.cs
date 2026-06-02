using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models
{
    [Table("Tb_PartidaFronteiras", Schema = "Jogo")]
    public class PartidaFronteira
    {
        [Key]
        public int IdPartidaFronteira { get; set; }

        public int IdPartida { get; set; }

        public int PosicaoOrigem { get; set; }

        public int PosicaoDestino { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorTravessia { get; set; }

        public int? EfeitoRequeridoId { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;
    }
}