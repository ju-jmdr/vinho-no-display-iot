namespace VinhoNoDisplay.DAO
{
    /// <summary>Lê a seção "Fiware" do appsettings.json (ou variáveis Fiware__Host etc.).</summary>
    public static class ConfiguracaoFiware
    {
        public static string Host { get; private set; }
        public static string ArquivoHost { get; private set; }   // guarda o IP escolhido na tela Dispositivo
        public static int PortaOrion { get; private set; }
        public static int PortaSth { get; private set; }
        public static string Service { get; private set; }
        public static string ServicePath { get; private set; }
        public static string TipoDispositivo { get; private set; }
        public static string EntidadeDispositivo { get; private set; }
        public static string DeviceId { get; private set; }

        public static string OrionUrl => $"http://{Host}:{PortaOrion}";
        public static string SthUrl => $"http://{Host}:{PortaSth}";

        /// <summary>
        /// Troca o IP do FIWARE em tempo de execução (Learner Lab: o IP público muda a cada sessão)
        /// e guarda em fiware-host.txt, que tem prioridade sobre o appsettings.json ao reiniciar.
        /// </summary>
        public static void DefineHost(string host, bool grava)
        {
            Host = host;
            if (grava) { try { File.WriteAllText(ArquivoHost, host); } catch { } }
        }

        public static void Iniciar(IConfiguration cfg, string pastaDoApp)
        {
            ArquivoHost = Path.Combine(pastaDoApp, "fiware-host.txt");
            var s = cfg.GetSection("Fiware");
            Host = s["Host"];
            try { if (File.Exists(ArquivoHost)) { var h = File.ReadAllText(ArquivoHost).Trim(); if (h.Length > 0) Host = h; } } catch { }
            PortaOrion = int.Parse(s["PortaOrion"] ?? "1026");
            PortaSth = int.Parse(s["PortaSth"] ?? "8666");
            Service = s["Service"] ?? "smart";
            ServicePath = s["ServicePath"] ?? "/";
            TipoDispositivo = s["TipoDispositivo"] ?? "Vinho";
            EntidadeDispositivo = s["EntidadeDispositivo"] ?? "urn:ngsi-ld:Vinho:001";
            DeviceId = s["DeviceId"] ?? "vinho001";
        }
    }
}
