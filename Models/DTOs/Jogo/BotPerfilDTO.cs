namespace BancoImobiliario.Models.DTOs.Jogo;

public class BotPerfilDTO
{
    public int IdPerfilBot { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public int DificuldadePadrao { get; set; }

    public string DificuldadePadraoDescricao { get; set; } = string.Empty;
}