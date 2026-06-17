using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Companhias", Schema = "Casas")]
    public class Companhia
    {
        [Key]
        public int CompanhiaId { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorPago { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorCompra { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? PorcValor { get; set; }

        /// <summary>
        /// 0 = normal (sem regra especial). Acima de zero identifica a regra:
        /// 1 = Business (soma dos aluguéis das outras companhias),
        /// 2 = Internet (aluguel = dado x FatorMultiplicador),
        /// 3 = Xerox (copia o aluguel de outra propriedade).
        /// Ver enum RegraEspecialCompanhia.
        /// </summary>
        public int RegraEspecial { get; set; }

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