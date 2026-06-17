using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Jogo
{
    /// <summary>
    /// Controle de "não repetir efeitos" por partida: registra quais
    /// Imposto/Prisao/Efeito já foram sorteados.
    /// TipoCatalogo: 1 = Imposto, 2 = Prisao, 3 = Efeito.
    /// </summary>
    [Table("Tb_PartidaCatalogoUsado", Schema = "Jogo")]
    public class PartidaCatalogoUsado
    {
        [Key]
        public int IdPartidaCatalogoUsado { get; set; }

        public int IdPartida { get; set; }

        public byte TipoCatalogo { get; set; }

        public int CatalogoId { get; set; }

        public DateTime DataUso { get; set; }
    }
}