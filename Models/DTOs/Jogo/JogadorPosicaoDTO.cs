namespace BancoImobiliario.Models.DTOs.Jogo
{
    /// <summary>
    /// Posição de um jogador no tabuleiro, usada para desenhar os peões/bordas.
    /// </summary>
    public class JogadorPosicaoDTO
    {
        public int IdPartidaJogador { get; set; }

        public string NomeJogador { get; set; } = string.Empty;

        public int Posicao { get; set; }

        public string? CorHex { get; set; }

        public bool EhBot { get; set; }

        public int? OrdemTurno { get; set; }

        public int EstadoTurno { get; set; }
    }
}
