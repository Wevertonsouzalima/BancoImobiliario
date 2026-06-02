namespace BancoImobiliario.Models.DTOs.Jogo;

public class AdicionarBotDTO
{
    public int IdPartida { get; set; }

    public int IdPerfilBot { get; set; }

    public string? NomeBot { get; set; }

    public int? DificuldadeBot { get; set; }
}