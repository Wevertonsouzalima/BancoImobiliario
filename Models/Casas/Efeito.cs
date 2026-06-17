using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Casas
{
    [Table("Tb_Efeitos", Schema = "Casas")]
    public class Efeito
    {
        [Key]
        public int EfeitoId { get; set; }

        /// <summary>1 = Normal, 2 = Grupo (EfeitoTipo). tinyint NOT NULL.</summary>
        public byte TipoEfeitoId { get; set; }

        /// <summary>EfeitoAlvo. tinyint NULL.</summary>
        public byte? AlvoEfeitoId { get; set; }

        /// <summary>EfeitoSubAlvo. tinyint NULL.</summary>
        public byte? SubAlvoEfeitoId { get; set; }

        /// <summary>EfeitoAcao. tinyint NULL.</summary>
        public byte? AcaoEfeitoId { get; set; }

        /// <summary>decimal NULL.</summary>
        public decimal? ValorEfeito { get; set; }

        public string? Frase { get; set; }
        public string? Imagem { get; set; }

        /// <summary>bit NULL.</summary>
        public bool? AplicaAposVolta { get; set; }

        /// <summary>bit NULL.</summary>
        public bool? RemoverAposVolta { get; set; }

        /// <summary>bit NULL.</summary>
        public bool? ValorDependeDado { get; set; }

        /// <summary>decimal NOT NULL.</summary>
        public decimal FatorMultiplicador { get; set; }

        /// <summary>bit NOT NULL.</summary>
        public bool RegraEspecial { get; set; }

        /// <summary>bit NULL.</summary>
        public bool? AplicarEfeitoCasaAposEfeito { get; set; }

        /// <summary>Identificador textual do efeito (código de regra). varchar(100) NULL.</summary>
        public string? ColunaEfeito { get; set; }

        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }
    }
}