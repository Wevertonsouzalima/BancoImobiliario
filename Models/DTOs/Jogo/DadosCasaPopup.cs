using System.Collections.Generic;

namespace BancoImobiliario.Models.Jogo
{
    /// <summary>
    /// Nível de visibilidade da casa no painel, derivado de IsRevelada + posição do jogador da vez.
    /// </summary>
    public enum VisibilidadeCasa
    {
        Oculta = 0,        // carta virada, nada identificável
        SomentePreco = 1,  // só o valor de compra (jogador da vez, casa sem dono)
        Completa = 2       // tudo visível
    }

    /// <summary>
    /// Tudo que o painel da casa precisa para renderizar, já resolvido pelo service
    /// respeitando a regra de IsRevelada. O componente apenas desenha.
    /// </summary>
    public sealed class DadosCasaPopup
    {
        public int PartidaTabuleiroId { get; set; }
        public int Posicao { get; set; }
        public byte TipoCasaId { get; set; }
        public VisibilidadeCasa Visibilidade { get; set; }

        public string Nome { get; set; } = string.Empty;
        public string? Imagem { get; set; }
        public string? CorHexadecimal { get; set; }

        // ----- Propriedade (Cidade / Companhia) -----
        public int? ProprietarioId { get; set; }
        public string NomeProprietario { get; set; } = "Banco";
        public decimal? ValorCompra { get; set; }
        public decimal? ValorAluguel { get; set; }
        public decimal? CustoCasa { get; set; }   // Cidade.ValorAddCasa
        public decimal? CustoHotel { get; set; }  // Cidade.ValorAddHotel
        public int QtdCasas { get; set; }
        public int QtdHoteis { get; set; }

        /// <summary>True quando a casa é Cidade ou Companhia (comprável/construível).</summary>
        public bool EhPropriedade { get; set; }
        /// <summary>True quando o dono é o próprio jogador da vez.</summary>
        public bool EhDonoJogadorDaVez { get; set; }
        /// <summary>True quando a casa pode ser comprada pelo jogador da vez (sem dono, na casa).</summary>
        public bool PodeOferecerCompra { get; set; }

        // ----- Grupo / efeito do grupo -----
        public int? GrupoId { get; set; }
        public string? CorGrupo { get; set; }
        public string? DescricaoEfeitoGrupo { get; set; }

        // ----- Fronteiras que partem desta casa -----
        public List<FronteiraCasa> Fronteiras { get; set; } = new();
    }

    public sealed class FronteiraCasa
    {
        public int PosicaoDestino { get; set; }
        public decimal ValorTravessia { get; set; }
        public int? EfeitoRequeridoId { get; set; }
        public string? DescricaoEfeitoRequerido { get; set; }
    }
}
