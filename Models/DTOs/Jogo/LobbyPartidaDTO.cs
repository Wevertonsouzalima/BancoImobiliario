namespace BancoImobiliario.Models.DTOs.Jogo;

public class LobbyPartidaDTO
{
    public int IdPartida { get; set; }

    public int IdConfiguracao { get; set; }

    public string CodigoSala { get; set; } = string.Empty;

    public int TipoJogo { get; set; }

    public string TipoJogoDescricao { get; set; } = string.Empty;

    public int IdStatusPartida { get; set; }

    public string StatusPartidaDescricao { get; set; } = string.Empty;

    public int QtdMaxJogadores { get; set; }

    public int QtdJogadoresAtivos { get; set; }

    public string? HostNome { get; set; }

    public bool UsuarioAtualEhHost { get; set; }

    public int? UsuarioAtualIdPartidaJogador { get; set; }

    public string? UsuarioAtualNome { get; set; }

    public bool UsuarioAtualEstaNaSala => UsuarioAtualIdPartidaJogador.HasValue;

    public bool PodeAdicionarJogadorOuBot { get; set; }

    public bool PodeIniciarDefinicaoOrdem { get; set; }

    public bool PodeIniciarPartida { get; set; }

    public bool ExisteEmpatePendente { get; set; }

    public bool TodosJogadoresComOrdemDefinida { get; set; }

    public DateTime DataCriacao { get; set; }
    // ---- Configuração do dado (vem da Configuracao da partida) ----
    // Usado pelo modal de dado para sortear/exibir dentro do intervalo real.
    public int ValMinDado { get; set; } = 1;

    public int ValMaxDado { get; set; } = 6;

    public List<LobbyJogadorDTO> Jogadores { get; set; } = new();
}