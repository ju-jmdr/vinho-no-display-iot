using VinhoNoDisplay.DAO;

namespace VinhoNoDisplay.Services
{
    /// <summary>Foto do último ciclo do supervisor (mostrada no painel).</summary>
    public class EstadoSupervisao
    {
        public bool Ativa { get; set; }
        public int Janela { get; set; }
        public int Leituras { get; set; }           // quantas leituras entraram na média
        public double? MediaTemperatura { get; set; }
        public double? MediaUmidade { get; set; }
        public double? MediaLuminosidade { get; set; }
        public int Mascara { get; set; }            // alerta que a média exige (1 temp, 2 umid, 4 luz)
        public List<string> Causas { get; set; } = new();
        public string UltimoComando { get; set; }   // ex.: "alerta|5 às 21:03:10"
        public string Mensagem { get; set; }        // situação / erro
        public string AtualizadoEm { get; set; }
    }

    /// <summary>
    /// SUPERVISÃO (critério H5): a cada ciclo lê as últimas N leituras no STH-Comet, calcula a MÉDIA de
    /// temperatura, umidade e luminosidade, compara com os limites do dispositivo (Orion) e, se o alerta
    /// exigido pela média for diferente do que o ESP32 informa (atributo "alarm"), envia o comando
    /// "alerta|máscara" ao Orion, que o repassa ao IoT Agent e ao ESP32 por MQTT.
    /// O app nunca fala direto com o ESP32.
    /// </summary>
    public class SupervisorService : BackgroundService
    {
        private static readonly object trava = new();
        private static EstadoSupervisao estado = new() { Mensagem = "Aguardando o primeiro ciclo…" };
        public static EstadoSupervisao Estado { get { lock (trava) return estado; } }

        private int ultimaMascaraEnviada = -1;
        private DateTime ultimoEnvioUtc = DateTime.MinValue;

        protected override async Task ExecuteAsync(CancellationToken parar)
        {
            while (!parar.IsCancellationRequested)
            {
                if (ConfiguracaoSupervisao.Ativa) Ciclo();
                try { await Task.Delay(TimeSpan.FromSeconds(ConfiguracaoSupervisao.IntervaloSegundos), parar); }
                catch (TaskCanceledException) { }
            }
        }

        private void Ciclo()
        {
            var e = new EstadoSupervisao
            {
                Ativa = true, Janela = ConfiguracaoSupervisao.JanelaLeituras,
                AtualizadoEm = DateTime.Now.ToString("HH:mm:ss"), UltimoComando = Estado.UltimoComando
            };
            try
            {
                var dao = new DispositivoDAO();
                var disp = dao.Obtem();
                if (!disp.Cadastrado) { e.Mensagem = "Dispositivo não cadastrado no Orion."; Publica(e); return; }
                if (disp.TempMax <= disp.TempMin)
                { e.Mensagem = "Aguardando o ESP32 informar os limites (ou envie a configuração)."; Publica(e); return; }

                var leituras = new HistoricoDAO().Consulta(null, null, e.Janela);
                var maisNova = leituras.FirstOrDefault();
                if (maisNova == null ||
                    (DateTime.UtcNow - HistoricoDAO.ParaUtc(maisNova.DataHora)).TotalSeconds > ConfiguracaoSupervisao.DadosVelhosSegundos)
                { e.Mensagem = "Sem leituras recentes: dispositivo offline?"; Publica(e); return; }

                // ---- MÉDIAS da janela ----
                e.Leituras = leituras.Count;
                e.MediaTemperatura = Media(leituras.Select(l => l.Temperatura));
                e.MediaUmidade = Media(leituras.Select(l => l.Umidade));
                e.MediaLuminosidade = Media(leituras.Select(l => l.Luminosidade));

                // ---- MÉDIAS x LIMITES ----
                int mascara = 0;
                if (e.MediaTemperatura is double t && (t < disp.TempMin || t > disp.TempMax)) mascara |= 1;
                if (e.MediaUmidade is double u && (u < disp.UmidMin || u > disp.UmidMax)) mascara |= 2;
                if (e.MediaLuminosidade is double l && l > disp.LuzMax) mascara |= 4;
                e.Mascara = mascara;
                if ((mascara & 1) != 0) e.Causas.Add("Temperatura");
                if ((mascara & 2) != 0) e.Causas.Add("Umidade");
                if ((mascara & 4) != 0) e.Causas.Add("Luz");

                // ---- COMANDO ao ESP32 (só se ele ainda não está nesse estado) ----
                int noDispositivo = disp.Alarme & 7;
                bool diferente = mascara != noDispositivo;
                bool mudouPedido = mascara != ultimaMascaraEnviada;
                bool vencido = (DateTime.UtcNow - ultimoEnvioUtc).TotalSeconds >= ConfiguracaoSupervisao.ReenvioSegundos;
                if (diferente && (mudouPedido || vencido))
                {
                    dao.EnviaAlerta(mascara);
                    ultimaMascaraEnviada = mascara;
                    ultimoEnvioUtc = DateTime.UtcNow;
                    e.UltimoComando = $"alerta|{mascara} às {DateTime.Now:HH:mm:ss}";
                }
                e.Mensagem = mascara == 0 ? "Tudo dentro dos limites." : "Fora dos limites: " + string.Join(", ", e.Causas);
            }
            catch (FiwareException ex) { e.Mensagem = ex.Message; }
            catch (Exception ex) { e.Mensagem = "Erro no supervisor: " + ex.Message; }
            Publica(e);
        }

        private static double? Media(IEnumerable<double?> valores)
        {
            var v = valores.Where(x => x.HasValue).Select(x => x.Value).ToList();
            return v.Count == 0 ? null : Math.Round(v.Average(), 1);
        }

        private static void Publica(EstadoSupervisao e) { lock (trava) estado = e; }
    }
}
