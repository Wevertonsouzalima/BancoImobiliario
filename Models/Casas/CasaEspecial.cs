using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_CasasEspeciais", Schema = "Casas")]
    public class CasaEspecial
    {
        [Key]
        public int CasasEspeciaisID { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        public int Tipo { get; set; }

        [StringLength(255)]
        public string? Imagem { get; set; }

        [Column(TypeName = "text")]
        public string? Frase { get; set; }

        public int? RegraEspecifica { get; set; }

        public int? ValorDependeDado { get; set; }

        public double? FatorMultiplicador { get; set; }

        public int? FatorDadoAplicaEm { get; set; }
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        public DateTime DataAtualizacao { get; set; } = DateTime.Now;
    }
}