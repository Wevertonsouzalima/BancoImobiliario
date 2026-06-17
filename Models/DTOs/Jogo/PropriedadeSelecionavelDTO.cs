namespace BancoImobiliario.Models.DTOs.Jogo
{
    /// <summary>
    /// Item de propriedade exibido no modal genérico de seleção
    /// (usado por Xerox/copiar, pegar do banco, roubar, etc.).
    /// </summary>
    public class PropriedadeSelecionavelDTO
    {
        public int Posicao { get; set; }
        public int PartidaTabuleiroId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Imagem { get; set; }
        public byte TipoCasaId { get; set; }

        /// <summary>≤0 = Banco; &gt;0 = id do jogador dono.</summary>
        public int ProprietarioId { get; set; }
        public string NomeProprietario { get; set; } = "Banco";

        public decimal ValorCompra { get; set; }
        public decimal ValorAluguel { get; set; }

        public int QtdCasas { get; set; }
        public int QtdHoteis { get; set; }

        /// <summary>Cor do grupo/casa, para a faixa lateral do card.</summary>
        public string? CorHexadecimal { get; set; }

        /// <summary>True se a propriedade é do banco (sem dono).</summary>
        public bool EhDoBanco => ProprietarioId <= 0;
        /// <summary>Grupo da propriedade (quando aplicável).</summary>
        public int? GrupoId { get; set; }
    }
}