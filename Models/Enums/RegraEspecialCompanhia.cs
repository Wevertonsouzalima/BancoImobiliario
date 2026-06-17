namespace BancoImobiliario.Models.Enums
{
    public enum RegraEspecialCompanhia
    {
        /// <summary>Companhia normal (Aérea, Água, Correio, Energia, Telecom, Trem, Mineração).</summary>
        Normal = 0,

        /// <summary>Business: só compra se for dono de todas as outras; aluguel = soma dos aluguéis das outras.</summary>
        Business = 1,

        /// <summary>Internet: compra obrigatória; aluguel = dado (positivo, inclui 0) x FatorMultiplicador.</summary>
        Internet = 2,

        /// <summary>Xerox: ao comprar, copia o aluguel de qualquer outra propriedade (cidade ou companhia).</summary>
        Xerox = 3
    }
}
