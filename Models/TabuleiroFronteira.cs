using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models
{
    [Table("Tb_TabuleiroFronteiras", Schema = "Tabuleiro")]
    public class TabuleiroFronteira
    {
        [Key]
        public int FronteiraId { get; set; }

        public int TabuleiroId { get; set; }

        public int PosicaoOrigem { get; set; }

        public int PosicaoDestino { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorTravessia { get; set; }

        public int? EfeitoRequeridoId { get; set; }

        public DateTime DataCriacao { get; set; }
    }
}