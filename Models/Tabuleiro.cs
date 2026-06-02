using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models
{
    [Table("Tb_Tabuleiros", Schema = "Tabuleiro")]
    public class Tabuleiros
    {
        [Key]
        public int TabuleiroId { get; set; }

        [Required]
        [MaxLength(100)] // Ajuste o tamanho máximo conforme a definição real do seu banco
        public string Nome { get; set; } = string.Empty;

        public int QtdCasas { get; set; }
        public int LarguraGrade { get; set; }
        public int AlturaGrade { get; set; }
        public int TamanhoCasaPx { get; set; }

        public DateTime DataCriacao { get; set; }
    }
}