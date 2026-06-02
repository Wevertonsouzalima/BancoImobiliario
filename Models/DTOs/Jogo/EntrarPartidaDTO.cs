namespace BancoImobiliario.Models.DTOs.Jogo;

public class EntrarPartidaDTO
{
    public string CodigoSala { get; set; } = string.Empty;

    public string NomeJogador { get; set; } = string.Empty;

    public string? IdentificadorJogador { get; set; }
}