namespace BancoImobiliario.Models.DTOs.Jogo
{
    public class AdicionarJogadorLocalDTO
    {
        public int IdPartida { get; set; }

        public string? Nome { get; set; }

        public string? CorHex { get; set; }

        public string? UrlAvatar { get; set; }
    }
}
