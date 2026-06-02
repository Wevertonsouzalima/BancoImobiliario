using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Prisao", Schema = "Casas")]
    public class Prisao
    {
        [Key]
        public int PrisaoId { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(45)]
        public string Nome { get; set; } = string.Empty;

        public int QtdRodadas { get; set; }

        [StringLength(250)]
        public string? Frase { get; set; }

        [StringLength(250)]
        public string? Imagem { get; set; }

        public bool? ValorDependeDado { get; set; }

        public int? FatorMultiplicador { get; set; }

        [StringLength(120)]
        public string? FatorDadoAplicaEm { get; set; }

        public DateTime DataCadastro { get; set; } = DateTime.Now;

        public DateTime DataAtualizacao { get; set; } = DateTime.Now;
    }
}