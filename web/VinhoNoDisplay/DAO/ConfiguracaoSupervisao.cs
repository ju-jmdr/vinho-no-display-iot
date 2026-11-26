namespace VinhoNoDisplay.DAO
{
    /// <summary>Parâmetros do supervisor (seção "Supervisao" do appsettings.json).</summary>
    public static class ConfiguracaoSupervisao
    {
        public static bool Ativa { get; private set; }
        /// <summary>Quantas leituras entram na média (o ESP32 envia a cada 5 s: 6 leituras = 30 s).</summary>
        public static int JanelaLeituras { get; private set; }
        public static int IntervaloSegundos { get; private set; }
        /// <summary>Se o ESP32 ainda não refletiu o comando, reenvia depois desse tempo.</summary>
        public static int ReenvioSegundos { get; private set; }
        /// <summary>Leituras mais velhas que isso são ignoradas (dispositivo offline: não há o que supervisionar).</summary>
        public static int DadosVelhosSegundos { get; private set; }

        public static void Iniciar(IConfiguration cfg)
        {
            var s = cfg.GetSection("Supervisao");
            Ativa = !string.Equals(s["Ativa"], "false", StringComparison.OrdinalIgnoreCase);
            JanelaLeituras = Math.Clamp(int.Parse(s["JanelaLeituras"] ?? "6"), 1, 100);
            IntervaloSegundos = Math.Clamp(int.Parse(s["IntervaloSegundos"] ?? "10"), 2, 600);
            ReenvioSegundos = Math.Clamp(int.Parse(s["ReenvioSegundos"] ?? "30"), 5, 3600);
            DadosVelhosSegundos = Math.Clamp(int.Parse(s["DadosVelhosSegundos"] ?? "60"), 10, 3600);
        }
    }
}
