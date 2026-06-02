namespace BancoImobiliario.Models.DTOs.Jogo;

/// <summary>
/// Representa uma casa do tabuleiro de uma partida, para exibicao na tela de jogo.
/// </summary>
public class TabuleiroCasaDTO
{
    public int PartidaTabuleiroId { get; set; }

    public int Posicao { get; set; }

    public byte TipoCasaId { get; set; }

    /// <summary>
    /// Descricao do tipo: Cidade, Companhia, Imposto, Prisao, Efeito, Especial.
    /// </summary>
    public string TipoCasaDescricao { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    /// <summary>Nome do arquivo de imagem da casa (ex: "Genebra.jpg").</summary>
    public string? Imagem { get; set; }

    /// <summary>Cor do grupo da casa, usada como fundo do numero da posicao.</summary>
    public string? CorHexadecimal { get; set; }

    public int? GrupoId { get; set; }

    public int? ProprietarioId { get; set; }

    public int QtdCasas { get; set; }

    public int QtdHoteis { get; set; }

    public bool IsRevelada { get; set; }

    public decimal? ValorCompraAtual { get; set; }

    public decimal? ValorAluguelAtual { get; set; }
}
