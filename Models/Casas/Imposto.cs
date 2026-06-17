using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Impostos", Schema = "Casas")]
    public class Imposto
    {
        [Key]
        public int ImpostoId { get; set; }

        public string Nome { get; set; } = string.Empty;

        /// <summary>1 = percentual do saldo; 2 = valor fixo.</summary>
        public int TipoValorId { get; set; }

        public decimal Valor { get; set; }

        public string? Imagem { get; set; }
        public string? Frase { get; set; }

        public bool ValorDependeDado { get; set; }
        public int FatorMultiplicador { get; set; }
        public string? FatorDadoAplicaEm { get; set; }

        public DateTime DataCadastro { get; set; }
        public DateTime DataAtualizacao { get; set; }
    }
}