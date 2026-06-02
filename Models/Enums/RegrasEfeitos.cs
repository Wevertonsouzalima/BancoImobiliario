namespace BancoImobiliario.Models.Enums
{
    public enum EfeitoTipo : byte
    {
        None = 0,

        Normal = 1,
        Grupo = 2
    }

    public enum EfeitoAlvo : byte
    {
        None = 0,

        Jogador = 1,
        Propriedade = 2,
        Tabuleiro = 3
    }

    public enum EfeitoSubAlvo : byte
    {
        None = 0,

        Adversario = 1,
        Aluguel = 2,
        Bonus = 3,
        CasaAtual = 4,
        CasaHotel = 5,
        Dado = 6,
        Efeitos = 7,
        Impostos = 8,
        Prisao = 9,
        Propriedade = 10,
        Recursos = 11,
        Saldo = 12,
        Tabuleiro = 13
    }

    public enum EfeitoAcao : byte
    {
        None = 0,

        Aleatorio = 1,
        Aluguel = 2,
        Apostas = 3,
        Avance = 4,
        Bloqueio = 5,
        CasaEspecifica = 6,
        Diminuir = 7,
        Escolher = 8,
        Ganhe = 9,
        Grupo = 10,
        Impares = 11,
        Imunidade = 12,
        Inverter = 13,
        JogadaExtra = 14,
        Pares = 15,
        Perca = 16,
        Propriedade = 17,
        Trocar = 18,
        Venda = 19,
        Volte = 20
    }
}