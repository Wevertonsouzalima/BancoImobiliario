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

        public bool RegraEspecial { get; set; }

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