namespace BancoImobiliario.Models.Enums
{
    /// <summary>
    /// Estado do jogador dentro do seu turno. Controla a visibilidade das ações
    /// e pode ser transmitido via rede para refletir o que cada jogador está fazendo.
    /// </summary>
    public enum EstadoTurnoJogador
    {
        /// <summary>Não é a vez do jogador. Ações desabilitadas.</summary>
        AguardandoVez = 0,

        /// <summary>É a vez do jogador, mas ele ainda não rolou o dado. Só "Jogar dado" disponível.</summary>
        AguardandoDado = 1,

        /// <summary>O jogador caiu numa casa e há uma ação pendente (pagar, comprar, aplicar efeito). Não pode finalizar.</summary>
        ResolvendoCasa = 2,

        /// <summary>A ação da casa foi resolvida. O jogador já pode finalizar a jogada.</summary>
        TurnoConcluido = 3
    }
}
