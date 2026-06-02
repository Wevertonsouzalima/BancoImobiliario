using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models
{
    [Table("Tb_LayoutTabuleiro", Schema = "Tabuleiro")]
    public class LayoutTabuleiro
    {
        [Key]
        public int LayoutTabuleiroId { get; set; }

        public int TabuleiroId { get; set; }
        public int Posicao { get; set; }
        public int Coluna { get; set; }
        public int Linha { get; set; }
    }
}