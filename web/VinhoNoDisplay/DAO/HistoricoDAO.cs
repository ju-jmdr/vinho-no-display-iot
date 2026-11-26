using System.Globalization;
using System.Text.Json;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.DAO
{
    /// <summary>
    /// Histórico no STH-Comet. O STH guarda uma série por atributo (alarm, temperature,
    /// humidity, luminosity), então juntamos as quatro pelo horário mais próximo.
    /// </summary>
    public class HistoricoDAO
    {
        private const int MaximoPorConsulta = 100;           // limite do STH-Comet
        private static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(2.5);

        private static readonly TimeZoneInfo Fuso = ObtemFuso();
        public static TimeZoneInfo FusoLocal => Fuso;
        private static TimeZoneInfo ObtemFuso()
        {
            foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
            return TimeZoneInfo.Local;
        }

        private static string UrlAtributo(string atributo) =>
            $"{ConfiguracaoFiware.SthUrl}/STH/v1/contextEntities/type/{ConfiguracaoFiware.TipoDispositivo}" +
            $"/id/{ConfiguracaoFiware.EntidadeDispositivo}/attributes/{atributo}";

        private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        /// <summary>Série (UTC) de um atributo. Sem dados o STH responde 404 → lista vazia.</summary>
        private static List<(DateTime t, double v)> Serie(string atributo, int ultimos, DateTime? deUtc, DateTime? ateUtc, int maximo)
        {
            var saida = new List<(DateTime, double)>();
            if (deUtc == null && ateUtc == null)
            {
                AcrescentaPagina(saida, UrlAtributo(atributo) + $"?lastN={Math.Min(ultimos, MaximoPorConsulta)}");
                return saida;
            }
            for (int offset = 0; saida.Count < maximo; offset += MaximoPorConsulta)
            {
                string url = UrlAtributo(atributo) + $"?hLimit={MaximoPorConsulta}&hOffset={offset}";
                if (deUtc != null) url += "&dateFrom=" + Iso(deUtc.Value);
                if (ateUtc != null) url += "&dateTo=" + Iso(ateUtc.Value);
                int antes = saida.Count;
                AcrescentaPagina(saida, url);
                if (saida.Count - antes < MaximoPorConsulta) break;
            }
            return saida;
        }

        private static void AcrescentaPagina(List<(DateTime, double)> saida, string url)
        {
            var json = HelperFiware.Get(url, true);
            if (json == null) return;
            using var doc = JsonDocument.Parse(json);
            foreach (var resp in doc.RootElement.GetProperty("contextResponses").EnumerateArray())
                foreach (var attr in resp.GetProperty("contextElement").GetProperty("attributes").EnumerateArray())
                    foreach (var v in attr.GetProperty("values").EnumerateArray())
                    {
                        var t = DateTime.Parse(v.GetProperty("recvTime").GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                        double valor = v.GetProperty("attrValue").ValueKind == JsonValueKind.Number
                            ? v.GetProperty("attrValue").GetDouble()
                            : double.Parse(v.GetProperty("attrValue").GetString(), CultureInfo.InvariantCulture);
                        saida.Add((t, valor));
                    }
        }

        private static double? MaisProximo(List<(DateTime t, double v)> serie, DateTime t)
        {
            double? melhor = null; var menor = Tolerancia;
            foreach (var (ti, vi) in serie)
            {
                var d = (ti - t).Duration();
                if (d <= menor) { menor = d; melhor = vi; }
            }
            return melhor;
        }

        /// <summary>Mensagens do período (horário local), da mais nova para a mais antiga.</summary>
        public List<LeituraViewModel> Consulta(DateTime? de, DateTime? ate, int maximo)
        {
            DateTime? deUtc = de == null ? null : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(de.Value, DateTimeKind.Unspecified), Fuso);
            DateTime? ateUtc = ate == null ? null : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(ate.Value, DateTimeKind.Unspecified), Fuso);

            var alarme = Serie("alarm", maximo, deUtc, ateUtc, maximo);
            var temp = Serie("temperature", maximo, deUtc, ateUtc, maximo);
            var umid = Serie("humidity", maximo, deUtc, ateUtc, maximo);
            var luz = Serie("luminosity", maximo, deUtc, ateUtc, maximo);

            // cada mensagem do ESP32 traz os 4 atributos; o "alarm" serve de base
            var baseSerie = alarme.Count > 0 ? alarme : temp;
            var lista = new List<LeituraViewModel>();
            foreach (var (t, v) in baseSerie)
            {
                if (ateUtc != null && t > ateUtc.Value) continue;
                lista.Add(new LeituraViewModel
                {
                    DataHora = TimeZoneInfo.ConvertTimeFromUtc(t, Fuso),
                    Alarme = alarme.Count > 0 ? (int)v : 0,
                    Temperatura = MaisProximo(temp, t),
                    Umidade = MaisProximo(umid, t),
                    Luminosidade = MaisProximo(luz, t)
                });
            }
            return lista.OrderByDescending(l => l.DataHora).Take(maximo).ToList();
        }

        public LeituraViewModel UltimaLeitura() => Consulta(null, null, 1).FirstOrDefault();

        /// <summary>Converte o horário local da leitura para UTC (para checar se o aparelho está online).</summary>
        public static DateTime ParaUtc(DateTime local) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Fuso);
    }
}
