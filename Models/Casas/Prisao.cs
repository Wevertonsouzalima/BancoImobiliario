using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Prisao", Schema = "Casas")]
    public class Prisao
    {
        [Key]
        public int PrisaoId { get; set; }

        public string Nome { get; set; } = string.Empty;

        /// <summary>Quantidade de rodadas preso (0 = depende do dado).</summary>
        public int QtdRodadas { get; set; }

        public string? Frase { get; set; }
        public string? Imagem { get; set; }

        public bool ValorDependeDado { get; set; }
        public int FatorMultiplicador { get; set; }
        public string? FatorDadoAplicaEm { get; set; }

        public DateTime DataCadastro { get; set; }
        public DateTime DataAtualizacao { get; set; }
    }
}