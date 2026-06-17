namespace BancoImobiliario.Models.Enums
{
    public enum TipoAcaoCasa
    {
        Nada = 0,
        PodeComprar = 1,
        PagouAluguel = 2,
        PodeConstruir = 3,
        PagouImposto = 4,
        FoiPreso = 5,
        Efeito = 6,
        Especial = 7,
        PodeComprarCompanhia = 8,   // <-- adicione esta linha
        PodeComprarInternet = 9,
        PodeComprarBusiness  = 10,   // Business (aquisição grátis)
        PodeComprarXerox     = 11,   // Xerox (compra + copia aluguel de outra)

        ADefinir = 99
    }
}
