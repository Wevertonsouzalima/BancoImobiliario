namespace BancoImobiliario.Models.Enums;

public enum TipoEventoPartida
{
    SalaCriada = 1,
    JogadorEntrou = 2,
    JogadorSaiu = 3,
    BotAdicionado = 4,
    BotRemovido = 5,
    JogadorPronto = 6,
    PartidaIniciada = 7,
    PartidaCancelada = 8,
    PartidaFinalizada = 9,
    ConfiguracaoAlterada = 10,

    DefinicaoOrdemIniciada = 11,
    DadoOrdemRolado = 12,
    EmpateOrdemDetectado = 13,
    OrdemTurnoDefinida = 14
}