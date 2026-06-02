namespace BancoImobiliario.Models.DTOs.Jogo;

public class LobbyJogadorDTO
{
    public int IdPartidaJogador { get; set; }

    public int IdPartida { get; set; }

    public string NomeJogador { get; set; } = string.Empty;

    public int TipoJogador { get; set; }

    public string TipoJogadorDescricao { get; set; } = string.Empty;

    public int IdStatusJogador { get; set; }

    public string StatusJogadorDescricao { get; set; } = string.Empty;

    public bool EhHost { get; set; }

    public bool EhBot { get; set; }

    public int OrdemJogador { get; set; }

    public int? OrdemTurno { get; set; }

    public int? UltimoResultadoOrdem { get; set; }

    public bool ParticipaDesempateOrdem { get; set; }

    public bool PodeRolarDadoOrdem { get; set; }

    public decimal SaldoAtual { get; set; }

    public int? IdPerfilBot { get; set; }

    public string? NomePerfilBot { get; set; }

    public int? DificuldadeBot { get; set; }

    public string? DificuldadeBotDescricao { get; set; }

    public string? CorHex { get; set; }

    public string? UrlAvatar { get; set; }
}