using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Cidades", Schema = "Casas")]
    public class Cidade
    {
        [Key]
        public int CidadeId { get; set; }

        [Required(ErrorMessage = "O nome da cidade é obrigatório.")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ValorVenda { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ValorAluguel { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ValorAddCasa { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ValorAddHotel { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AdicionalCasa { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AdicionalHotel { get; set; }

        [StringLength(250)]
        public string? Imagem { get; set; }

        public int? ValorDependeDado { get; set; }

        public int? FatorMultiplicador { get; set; }

        public DateTime? DataCadastro { get; set; } = DateTime.Now;

        public DateTime? DataAtualizacao { get; set; } = DateTime.Now;
    }
}