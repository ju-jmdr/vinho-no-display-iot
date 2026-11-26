namespace VinhoNoDisplay.Models
{
    /// <summary>Perfil de armazenamento: faixas ideais de temperatura, umidade e luz.</summary>
    public class PerfilViewModel : PadraoViewModel
    {
        public string Nome { get; set; }
        public double TempMin { get; set; }
        public double TempMax { get; set; }
        public double UmidMin { get; set; }
        public double UmidMax { get; set; }
        public double LuzMax { get; set; }

        /// <summary>Retorna pares (campo, mensagem) para cada regra violada.</summary>
        public List<KeyValuePair<string, string>> ValidaLimites()
        {
            var e = new List<KeyValuePair<string, string>>();
            void Add(string c, string m) => e.Add(new KeyValuePair<string, string>(c, m));
            if (TempMin < -10 || TempMin > 40) Add("TempMin", "Temperatura mínima deve estar entre -10 e 40 °C.");
            if (TempMax < -10 || TempMax > 40) Add("TempMax", "Temperatura máxima deve estar entre -10 e 40 °C.");
            if (TempMin >= TempMax) Add("TempMax", "A temperatura máxima deve ser maior que a mínima.");
            if (UmidMin < 0 || UmidMin > 100) Add("UmidMin", "Umidade mínima deve estar entre 0 e 100 %.");
            if (UmidMax < 0 || UmidMax > 100) Add("UmidMax", "Umidade máxima deve estar entre 0 e 100 %.");
            if (UmidMin >= UmidMax) Add("UmidMax", "A umidade máxima deve ser maior que a mínima.");
            if (LuzMax < 0 || LuzMax > 100) Add("LuzMax", "A luminosidade máxima deve estar entre 0 e 100 %.");
            return e;
        }
    }
}
