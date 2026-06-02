namespace BancoImobiliario.Models.DTOs.Jogo;

public class CriarPartidaResultadoDTO
{
    public int IdPartida { get; set; }

    public int IdConfiguracao { get; set; }

    public string CodigoSala { get; set; } = string.Empty;

    public int TipoJogo { get; set; }

    public int QtdMaxJogadores { get; set; }
}