using System.Globalization;
using System.Text.Json;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.DAO
{
    /// <summary>Entidade do ESP32 (urn:ngsi-ld:Vinho:001): leitura dos limites e envio de comandos.</summary>
    public class DispositivoDAO
    {
        private static string Url => $"{ConfiguracaoFiware.OrionUrl}/v2/entities/{ConfiguracaoFiware.EntidadeDispositivo}";

        public DispositivoViewModel Obtem()
        {
            var json = HelperFiware.Get(Url + "?options=keyValues", true);
            if (json == null) return new DispositivoViewModel { Cadastrado = false };
            using var doc = JsonDocument.Parse(json);
            var e = doc.RootElement;
            return new DispositivoViewModel
            {
                Cadastrado = true,
                TempMin = JsonHelper.Dbl(e, "tempMin"), TempMax = JsonHelper.Dbl(e, "tempMax"),
                UmidMin = JsonHelper.Dbl(e, "umidMin"), UmidMax = JsonHelper.Dbl(e, "umidMax"),
                LuzMax = JsonHelper.Dbl(e, "luzMax"),
                Alarme = JsonHelper.Inteiro(e, "alarm"),
                StatusConfig = JsonHelper.Texto(e, "config_status"),
                StatusAlerta = JsonHelper.Texto(e, "testealerta_status")
            };
        }

        private static void Comando(string nome, string valor)
        {
            var corpo = new Dictionary<string, object> { [nome] = new { type = "command", value = valor } };
            HelperFiware.Executa(HttpMethod.Patch, Url + "/attrs", corpo);
        }

        public void EnviaConfig(double tMin, double tMax, double uMin, double uMax, double luz)
        {
            // Vírgula como separador: o Orion rejeita ';' em valores de texto.
            string F(double d) => d.ToString("0.0", CultureInfo.InvariantCulture);
            Comando("config", string.Join(",", F(tMin), F(tMax), F(uMin), F(uMax), F(luz)));
        }

        /// <summary>
        /// Horário (UTC) da última atualização do atributo "uptime", que muda a cada mensagem do ESP32.
        /// Não depende do STH: o Orion só notifica o histórico quando o valor muda.
        /// </summary>
        public DateTime? UltimoSinalUtc()
        {
            var json = HelperFiware.Get(Url + "?attrs=uptime&metadata=dateModified", true);
            if (json == null) return null;
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("uptime", out var u)) return null;
            if (!u.TryGetProperty("metadata", out var m) || !m.TryGetProperty("dateModified", out var d)) return null;
            return DateTime.Parse(d.GetProperty("value").GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        /// <summary>Online = recebeu sinal nos últimos 20 s (usa o uptime; sem ele, cai para a última leitura do histórico).</summary>
        public bool EstaOnline(LeituraViewModel ultimaLeitura)
        {
            var sinal = UltimoSinalUtc();
            if (sinal != null) return (DateTime.UtcNow - sinal.Value).TotalSeconds < 20;
            return ultimaLeitura != null && (DateTime.UtcNow - HistoricoDAO.ParaUtc(ultimaLeitura.DataHora)).TotalSeconds < 20;
        }

        /// <summary>Comando "alerta": máscara de bits (1 temp, 2 umid, 4 luz; 0 = tudo normal, desliga LED e buzzer).</summary>
        public void EnviaAlerta(int mascara) => Comando("alerta", (mascara & 7).ToString());

        public void TestaAlerta() => Comando("testealerta", "");
    }
}
