using Microsoft.AspNetCore.Mvc;
using VinhoNoDisplay.DAO;
using VinhoNoDisplay.Services;

namespace VinhoNoDisplay.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index() => View();

        /// <summary>JSON consumido pelo dashboard.js (AJAX, atualizado a cada 10 s).</summary>
        public IActionResult ObtemDados(int quantidade = 60)
        {
            quantidade = Math.Clamp(quantidade, 10, 100);
            try
            {
                var disp = new DispositivoDAO().Obtem();
                var leituras = new HistoricoDAO().Consulta(null, null, quantidade).OrderBy(l => l.DataHora).ToList();
                int garrafas = 0;
                try { garrafas = new GarrafaDAO().Listagem().Sum(g => g.Quantidade); } catch (FiwareException) { }

                var ultima = leituras.LastOrDefault();
                bool online = disp.Cadastrado && new DispositivoDAO().EstaOnline(ultima);
                double conformidade = leituras.Count == 0 ? 0
                    : Math.Round(100.0 * leituras.Count(l => !l.TemAlarme) / leituras.Count, 1);

                return Json(new
                {
                    erro = (string)null,
                    cadastrado = disp.Cadastrado,
                    online,
                    garrafas,
                    total = leituras.Count,
                    conformidade,
                    limites = new { tempMin = disp.TempMin, tempMax = disp.TempMax, umidMin = disp.UmidMin, umidMax = disp.UmidMax, luzMax = disp.LuzMax },
                    atual = ultima == null ? null : new
                    {
                        hora = ultima.DataHora.ToString("dd/MM HH:mm:ss"),
                        temperatura = ultima.Temperatura, umidade = ultima.Umidade,
                        luminosidade = ultima.Luminosidade, alarme = ultima.Alarme, causas = ultima.Causas()
                    },
                    serie = new
                    {
                        labels = leituras.Select(l => l.DataHora.ToString("HH:mm:ss")),
                        temperatura = leituras.Select(l => l.Temperatura),
                        umidade = leituras.Select(l => l.Umidade),
                        luminosidade = leituras.Select(l => l.Luminosidade)
                    },
                    supervisao = SupervisorService.Estado,
                    anomalias = new
                    {
                        temperatura = leituras.Count(l => (l.Alarme & 1) != 0),
                        umidade = leituras.Count(l => (l.Alarme & 2) != 0),
                        luz = leituras.Count(l => (l.Alarme & 4) != 0),
                        sensor = leituras.Count(l => (l.Alarme & 8) != 0)
                    }
                });
            }
            catch (FiwareException ex) { return Json(new { erro = ex.Message }); }
        }
    }
}
