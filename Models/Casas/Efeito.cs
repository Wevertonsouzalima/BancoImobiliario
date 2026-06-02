using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Efeitos", Schema = "Casas")]
    public class Efeito
    {
        [Key]
        public int EfeitoId { get; set; }
        public byte TipoEfeitoId { get; set; }

        public byte AlvoEfeitoId { get; set; }

        public byte? SubAlvoEfeitoId { get; set; }

        public byte AcaoEfeitoId { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ValorEfeito { get; set; }

        [StringLength(500)]
        public string? Frase { get; set; }

        [StringLength(250)]
        public string? Imagem { get; set; }

        public bool AplicaAposVolta { get; set; }

        public bool RemoverAposVolta { get; set; }

        public bool ValorDependeDado { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal FatorMultiplicador { get; set; }

        public bool RegraEspecial { get; set; }

        public bool AplicarEfeitoCasaAposEfeito { get; set; }

        [StringLength(100)]
        public string? ColunaEfeito { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        [Column(TypeName = "datetime2(0)")]
        public DateTime? DataAtualizacao { get; set; }
    }
}