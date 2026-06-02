namespace BancoImobiliario.Models.DTOs.Jogo;

public class RolarDadoOrdemResultadoDTO
{
    public int IdPartida { get; set; }

    public int IdPartidaJogador { get; set; }

    public string NomeJogador { get; set; } = string.Empty;

    public int ResultadoDado { get; set; }

    public int RodadaDesempate { get; set; }

    public int GrupoDesempate { get; set; }

    public bool OrdemFoiDefinida { get; set; }

    public bool ExisteEmpatePendente { get; set; }

    public LobbyPartidaDTO? LobbyAtualizado { get; set; }
}