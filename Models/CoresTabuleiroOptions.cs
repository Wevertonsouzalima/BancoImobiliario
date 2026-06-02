using BancoImobiliario.Models.Enums;

namespace BancoImobiliario.Models
{
    public class CoresTabuleiroOptions
    {
        public const string SectionName = "CoresTabuleiro";

        public string Cidade { get; set; } = "#3B82F6";
        public string Companhia { get; set; } = "#F59E0B";
        public string Imposto { get; set; } = "#EF4444";
        public string Prisao { get; set; } = "#6B7280";
        public string Efeito { get; set; } = "#8B5CF6";
        public string Especial { get; set; } = "#10B981";

        public string CorPorTipo(TipoCasa tipo) => tipo switch
        {
            TipoCasa.Cidade => Cidade,
            TipoCasa.Companhia => Companhia,
            TipoCasa.Imposto => Imposto,
            TipoCasa.Prisao => Prisao,
            TipoCasa.Efeito => Efeito,
            TipoCasa.Especial => Especial,
            _ => "#000000"
        };
    }
}
