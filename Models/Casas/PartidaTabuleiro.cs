using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_PartidaTabuleiro", Schema = "Tabuleiro")]
    public class PartidaTabuleiro
    {
        [Key]
        public int PartidaTabuleiroId { get; set; }

        public int PartidaId { get; set; }

        public int Posicao { get; set; }

        /// <summary>
        /// 1=Cidade, 2=Companhia, 3=Imposto, 4=Prisao, 5=Efeito, 6=Especial
        /// </summary>
        public byte TipoCasaId { get; set; }

        public int ReferenciaCatalogoId { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Imagem { get; set; }

        [StringLength(7)]
        public string? CorHexadecimal { get; set; }

        public int? GrupoId { get; set; }

        public int ProprietarioId { get; set; }

        public int QtdCasas { get; set; } = 0;

        public int QtdHoteis { get; set; } = 0;

        public bool IsRevelada { get; set; } = true;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorCompraAtual { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ValorAluguelAtual { get; set; }

        public DateTime DataAtualizacao { get; set; } = DateTime.Now;

    }
}