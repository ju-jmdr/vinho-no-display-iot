using Microsoft.AspNetCore.Mvc;
using VinhoNoDisplay.DAO;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.Controllers
{
    public class DispositivoController : Controller
    {
        public IActionResult Index()
        {
            try { return View(new DispositivoDAO().Obtem()); }
            catch (FiwareException ex) { return View("Error", new ErrorViewModel { Mensagem = ex.Message }); }
        }

        public IActionResult ObtemStatus()
        {
            try
            {
                var d = new DispositivoDAO().Obtem();
                var ultima = new HistoricoDAO().UltimaLeitura();
                var sinal = new DispositivoDAO().UltimoSinalUtc();
                bool online = new DispositivoDAO().EstaOnline(ultima);
                return Json(new
                {
                    ok = true, d.Cadastrado, online, d.StatusConfig, d.StatusAlerta,
                    ultima = sinal != null ? TimeZoneInfo.ConvertTimeFromUtc(sinal.Value, HistoricoDAO.FusoLocal).ToString("dd/MM/yyyy HH:mm:ss") : ultima?.DataHora.ToString("dd/MM/yyyy HH:mm:ss")
                });
            }
            catch (FiwareException ex) { return Json(new { ok = false, mensagem = ex.Message }); }
        }

        [HttpPost]
        public IActionResult EnviaConfig(double tempMin, double tempMax, double umidMin, double umidMax, double luzMax)
        {
            var p = new PerfilViewModel { TempMin = tempMin, TempMax = tempMax, UmidMin = umidMin, UmidMax = umidMax, LuzMax = luzMax };
            var erros = p.ValidaLimites();
            if (erros.Count > 0) return Json(new { ok = false, mensagem = string.Join(" ", erros.Select(e => e.Value)) });
            try
            {
                new DispositivoDAO().EnviaConfig(tempMin, tempMax, umidMin, umidMax, luzMax);
                return Json(new { ok = true, mensagem = "Configuração enviada. O display confirma em instantes." });
            }
            catch (FiwareException ex) { return Json(new { ok = false, mensagem = ex.Message }); }
        }

        /// <summary>Troca o IP do FIWARE sem reiniciar o app (Learner Lab muda o IP a cada sessão).</summary>
        [HttpPost]
        public IActionResult SalvaHost(string host)
        {
            host = (host ?? "").Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(host, @"^[A-Za-z0-9.\-]{3,100}$"))
                return Json(new { ok = false, mensagem = "Informe um IP ou nome de host válido (sem http:// e sem porta)." });
            var anterior = ConfiguracaoFiware.Host;
            ConfiguracaoFiware.DefineHost(host, false);
            try
            {
                HelperFiware.Get(ConfiguracaoFiware.OrionUrl + "/version");      // testa o Orion
                ConfiguracaoFiware.DefineHost(host, true);
                return Json(new { ok = true, mensagem = "Conectado ao Orion em " + host + ". IP salvo." });
            }
            catch (FiwareException ex)
            {
                ConfiguracaoFiware.DefineHost(anterior, false);
                return Json(new { ok = false, mensagem = "Não foi possível conectar em " + host + ": " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult TestaAlerta()
        {
            try { new DispositivoDAO().TestaAlerta(); return Json(new { ok = true, mensagem = "Alerta de teste enviado ao display." }); }
            catch (FiwareException ex) { return Json(new { ok = false, mensagem = ex.Message }); }
        }
    }
}
