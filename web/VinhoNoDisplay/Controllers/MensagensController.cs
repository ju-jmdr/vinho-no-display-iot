using Microsoft.AspNetCore.Mvc;
using VinhoNoDisplay.DAO;

namespace VinhoNoDisplay.Controllers
{
    public class MensagensController : Controller
    {
        public IActionResult Index() => View();

        /// <summary>Devolve só a tabela (partial view), injetada via jQuery $.ajax.</summary>
        public IActionResult ObtemDados(int maximo = 50, DateTime? dataInicial = null, DateTime? dataFinal = null, bool somenteAnomalias = false)
        {
            maximo = Math.Clamp(maximo, 10, 500);
            if (dataInicial != null && dataFinal != null && dataInicial > dataFinal)
                return Content("<div class='aviso-vazio'>A data inicial não pode ser maior que a final.</div>", "text/html");
            try
            {
                var lista = new HistoricoDAO().Consulta(dataInicial, dataFinal, maximo);
                if (somenteAnomalias) lista = lista.Where(l => l.TemAlarme).ToList();
                return PartialView("pvGridMensagens", lista);
            }
            catch (FiwareException ex)
            {
                return Content("<div class='aviso-vazio erro'>" + System.Net.WebUtility.HtmlEncode(ex.Message) + "</div>", "text/html");
            }
        }
    }
}
