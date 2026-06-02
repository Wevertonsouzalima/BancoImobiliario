using BancoImobiliario.Models.Enums;

namespace BancoImobiliario.Models.Helpers;

public static class EnumDescricaoHelper
{
    public static string ObterDescricao(TipoJogoConfiguracao valor)
    {
        return valor switch
        {
            TipoJogoConfiguracao.Solo => "Solo",
            TipoJogoConfiguracao.LocalMultiplayer => "Local Multiplayer",
            TipoJogoConfiguracao.Rede => "Via Rede",
            _ => valor.ToString()
        };
    }

    public static string ObterDescricao(DificuldadeJogo valor)
    {
        return valor switch
        {
            DificuldadeJogo.Facil => "Fácil",
            DificuldadeJogo.Normal => "Normal",
            DificuldadeJogo.Dificil => "Difícil",
            DificuldadeJogo.Personalizada => "Personalizada",
            _ => valor.ToString()
        };
    }
    public static string ObterDescricao(TipoJogadorPartida valor)
    {
        return valor switch
        {
            TipoJogadorPartida.Humano => "Humano",
            TipoJogadorPartida.Bot => "Bot",
            _ => valor.ToString()
        };
    }

    public static string ObterDescricao(StatusJogadorPartida valor)
    {
        return valor switch
        {
            StatusJogadorPartida.Aguardando => "Aguardando",
            StatusJogadorPartida.Pronto => "Pronto",
            StatusJogadorPartida.Jogando => "Jogando",
            StatusJogadorPartida.Saiu => "Saiu",
            StatusJogadorPartida.Removido => "Removido",
            _ => valor.ToString()
        };
    }

    public static string ObterDescricao(DificuldadeBot valor)
    {
        return valor switch
        {
            DificuldadeBot.Facil => "Fácil",
            DificuldadeBot.Normal => "Normal",
            DificuldadeBot.Dificil => "Difícil",
            _ => valor.ToString()
        };
    }
    public static string ObterDescricao(RegraFronteiraJogo valor)
    {
        return valor switch
        {
            RegraFronteiraJogo.DonoDeApenasUma => "Dono de apenas uma",
            RegraFronteiraJogo.DonoDasDuas => "Dono das duas",
            _ => valor.ToString()
        };
    }

    public static string ObterDescricao(TipoFinalizacaoJogo valor)
    {
        return valor switch
        {
            TipoFinalizacaoJogo.TodasPropriedadesCompradas => "Todas as propriedades forem compradas",
            TipoFinalizacaoJogo.NumeroVoltas => "Número de voltas",
            TipoFinalizacaoJogo.NumeroPropriedadesCompradas => "Número de propriedades compradas",
            TipoFinalizacaoJogo.SaldoAcumulado => "Saldo acumulado",
            TipoFinalizacaoJogo.TempoMinutos => "Tempo (minutos)",
            TipoFinalizacaoJogo.Ilimitado => "Ilimitado",
            _ => valor.ToString()
        };
    }

    public static string ObterDescricao(VenceQuemJogo valor)
    {
        return valor switch
        {
            VenceQuemJogo.MaisPropriedades => "Mais propriedades",
            VenceQuemJogo.MaiorSaldoFinal => "Maior saldo final",
            VenceQuemJogo.MaiorSaldoTotal => "Maior saldo total",
            VenceQuemJogo.MaiorNumeroVoltas => "Maior número de voltas",
            VenceQuemJogo.PontuacaoIndividual => "Pontuação individual",
            _ => valor.ToString()
        };
    }
    public static string ObterDescricao(StatusPartida valor)
    {
        return valor switch
        {
            StatusPartida.Lobby => "Aguardando jogadores",
            StatusPartida.DefinindoOrdem => "Definindo ordem dos jogadores",
            StatusPartida.OrdemDefinida => "Ordem definida",
            StatusPartida.EmAndamento => "Em andamento",
            StatusPartida.Finalizada => "Finalizada",
            StatusPartida.Cancelada => "Cancelada",
            StatusPartida.Expirada => "Expirada",
            _ => valor.ToString()
        };
    }

    public static string ObterDescricao(TipoEventoPartida valor)
    {
        return valor switch
        {
            TipoEventoPartida.SalaCriada => "Sala criada",
            TipoEventoPartida.JogadorEntrou => "Jogador entrou",
            TipoEventoPartida.JogadorSaiu => "Jogador saiu",
            TipoEventoPartida.BotAdicionado => "Bot adicionado",
            TipoEventoPartida.BotRemovido => "Bot removido",
            TipoEventoPartida.JogadorPronto => "Jogador pronto",
            TipoEventoPartida.PartidaIniciada => "Partida iniciada",
            TipoEventoPartida.PartidaCancelada => "Partida cancelada",
            TipoEventoPartida.PartidaFinalizada => "Partida finalizada",
            TipoEventoPartida.ConfiguracaoAlterada => "Configuração alterada",
            TipoEventoPartida.DefinicaoOrdemIniciada => "Definição de ordem iniciada",
            TipoEventoPartida.DadoOrdemRolado => "Dado de ordem rolado",
            TipoEventoPartida.EmpateOrdemDetectado => "Empate de ordem detectado",
            TipoEventoPartida.OrdemTurnoDefinida => "Ordem de turno definida",
            _ => valor.ToString()
        };
    }
}