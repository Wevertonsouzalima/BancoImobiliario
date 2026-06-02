using BancoImobiliario.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BancoImobiliario.Models.Jogo;

[Table("Tb_PartidaJogadores", Schema = "Jogo")]
public class Tb_PartidaJogador
{
    [Key]
    public int IdPartidaJogador { get; set; }

    public int IdPartida { get; set; }

    [Required, MaxLength(120)]
    public string NomeJogador { get; set; } = string.Empty;

    public int TipoJogador { get; set; }

    public int IdStatusJogador { get; set; } = 1;

    public bool EhHost { get; set; }

    public int OrdemJogador { get; set; }

    public int? OrdemTurno { get; set; }

    public int? UltimoResultadoOrdem { get; set; }

    public bool ParticipaDesempateOrdem { get; set; }

    public decimal SaldoAtual { get; set; }

    [MaxLength(150)]
    public string? IdentificadorJogador { get; set; }

    [MaxLength(150)]
    public string? ConnectionIdSignalR { get; set; }

    public int? IdPerfilBot { get; set; }

    public int? DificuldadeBot { get; set; }

    [MaxLength(20)]
    public string? CorHex { get; set; }

    [MaxLength(500)]
    public string? UrlAvatar { get; set; }

    public DateTime DataEntrada { get; set; }

    public DateTime? DataSaida { get; set; }

    public DateTime? DataAtualizacao { get; set; }
    // ===== Estado de jogo no tabuleiro =====

    /// <summary>
    /// Número exato da casa onde o peão está (índice Posicao da Tb_PartidaTabuleiro).
    /// Começa sempre em 0 (casa inicial).
    /// </summary>
    public int PosicaoAtual { get; set; }

    /// <summary>
    /// Quantidade de voltas completas que o jogador já deu no tabuleiro.
    /// </summary>
    public int VoltasCompletadas { get; set; }

    /// <summary>
    /// Turnos restantes de prisão. Quando maior que zero, o jogador está preso.
    /// A cada turno cumprido, decrementa até chegar a zero (livre).
    /// </summary>
    public int TurnosPreso { get; set; }

    /// <summary>
    /// Indica se o jogador está preso no momento. Não é mapeado no banco —
    /// é derivado de TurnosPreso.
    /// </summary>
    [NotMapped]
    public bool EstaPreso => TurnosPreso > 0;
    /// <summary>
    /// Estado do jogador no turno (aguardando, jogando dado, resolvendo casa, concluído).
    /// Persistido como int; controla a visibilidade das ações e pode ser transmitido via rede.
    /// </summary>
    public int EstadoTurno { get; set; }

    /// <summary>
    /// Indica se o jogador já pode finalizar a jogada. Controlado manualmente conforme
    /// os cenários (pagamento, efeitos a aplicar etc.) — não derivado do EstadoTurno.
    /// </summary>
    public bool PodeFinalizar { get; set; }

    /// <summary>
    /// Acesso tipado ao EstadoTurno. Não mapeado: usa a coluna EstadoTurno (int).
    /// </summary>
    [NotMapped]
    public EstadoTurnoJogador EstadoTurnoEnum
    {
        get => (EstadoTurnoJogador)EstadoTurno;
        set => EstadoTurno = (int)value;
    }
}