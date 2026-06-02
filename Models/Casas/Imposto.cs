using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Impostos", Schema = "Casas")]
    public class Imposto
    {
        [Key]
        public int ImpostoId { get; set; }

        [Required(ErrorMessage = "O nome do imposto é obrigatório.")]
        [StringLength(45)]
        public string Nome { get; set; } = string.Empty;

        // Se preferir usar a Enum diretamente: public RegraImpostos? TipoValorId { get; set; }
        public int? TipoValorId { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Valor { get; set; }

        [StringLength(250)]
        public string? Imagem { get; set; }

        [StringLength(250)]
        public string? Frase { get; set; }

        public bool? ValorDependeDado { get; set; }

        public int? FatorMultiplicador { get; set; }

        [StringLength(120)]
        public string? FatorDadoAplicaEm { get; set; }

        public DateTime DataCadastro { get; set; } = DateTime.Now;

        public DateTime DataAtualizacao { get; set; } = DateTime.Now;
    }
}