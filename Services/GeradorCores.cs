namespace BancoImobiliario.Services
{
    public static class GeradorCores
    {
        /// <summary>
        /// Gera uma cor aleatória (#RRGGBB) visualmente distinta das cores já usadas.
        /// </summary>
        public static string GerarCorDistinta(IEnumerable<string> coresExistentes,
                                              double distanciaMinima = 60.0)
        {
            var existentes = coresExistentes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(HexParaRgb)
                .ToList();

            const int maxTentativas = 200;

            for (int tentativa = 0; tentativa < maxTentativas; tentativa++)
            {
                // HSL: matiz livre, saturação e luminância em faixa "agradável"
                double h = Random.Shared.NextDouble() * 360.0;
                double s = 0.55 + Random.Shared.NextDouble() * 0.30; // 55%-85%
                double l = 0.40 + Random.Shared.NextDouble() * 0.25; // 40%-65%

                var (r, g, b) = HslParaRgb(h, s, l);

                bool conflita = existentes.Any(e => DistanciaRgb(e, (r, g, b)) < distanciaMinima);
                if (!conflita)
                    return RgbParaHex(r, g, b);
            }

            // Fallback: se não achou após muitas tentativas, relaxa e devolve a última
            double hf = Random.Shared.NextDouble() * 360.0;
            var (rf, gf, bf) = HslParaRgb(hf, 0.70, 0.50);
            return RgbParaHex(rf, gf, bf);
        }

        private static double DistanciaRgb((int r, int g, int b) a, (int r, int g, int b) b)
        {
            // Distância euclidiana ponderada (aproximação perceptual)
            double dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Math.Sqrt(2 * dr * dr + 4 * dg * dg + 3 * db * db);
        }

        private static (int r, int g, int b) HexParaRgb(string hex)
        {
            hex = hex.TrimStart('#');
            return (
                Convert.ToInt32(hex.Substring(0, 2), 16),
                Convert.ToInt32(hex.Substring(2, 2), 16),
                Convert.ToInt32(hex.Substring(4, 2), 16)
            );
        }

        private static string RgbParaHex(int r, int g, int b)
            => $"#{r:X2}{g:X2}{b:X2}";

        private static (int r, int g, int b) HslParaRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;

            double r1, g1, b1;
            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            return (
                (int)Math.Round((r1 + m) * 255),
                (int)Math.Round((g1 + m) * 255),
                (int)Math.Round((b1 + m) * 255)
            );
        }
    }
}
