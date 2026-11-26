using Microsoft.AspNetCore.Mvc;
using VinhoNoDisplay.DAO;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.Controllers
{
    public class PerfilController : PadraoController<PerfilViewModel>
    {
        public PerfilController() { DAO = new PerfilDAO(); }

        protected override void ValidaDados(PerfilViewModel m, string operacao)
        {
            base.ValidaDados(m, operacao);
            ValidaTexto("Nome", m.Nome, "o nome do perfil");
            foreach (var e in m.ValidaLimites()) ModelState.AddModelError(e.Key, e.Value);
        }

        /// <summary>Cria 3 perfis prontos (só os que ainda não existem pelo nome).</summary>
        public IActionResult CriarSugestoes()
        {
            try
            {
                var dao = (PerfilDAO)DAO;
                var existentes = dao.Listagem();
                var sugestoes = new[]
                {
                    new PerfilViewModel { Nome = "Tintos de guarda", TempMin = 13, TempMax = 18, UmidMin = 60, UmidMax = 75, LuzMax = 30 },
                    new PerfilViewModel { Nome = "Brancos e rosés", TempMin = 8, TempMax = 13, UmidMin = 60, UmidMax = 75, LuzMax = 30 },
                    new PerfilViewModel { Nome = "Espumantes", TempMin = 8, TempMax = 12, UmidMin = 65, UmidMax = 75, LuzMax = 30 }
                };
                int proximo = dao.ProximoId();
                foreach (var s in sugestoes)
                {
                    if (existentes.Any(x => x.Nome == s.Nome)) continue;
                    s.Id = proximo++;
                    dao.Insert(s);
                }
                return RedirectToAction("Index");
            }
            catch (FiwareException ex) { return View("Error", new ErrorViewModel { Mensagem = ex.Message }); }
        }

        /// <summary>Envia as faixas do perfil ao ESP32 (comando config).</summary>
        [HttpPost]
        public IActionResult AplicarNoDispositivo(int id)
        {
            try
            {
                var p = DAO.Consulta(id);
                if (p == null) return Json(new { ok = false, mensagem = "Perfil não encontrado." });
                new DispositivoDAO().EnviaConfig(p.TempMin, p.TempMax, p.UmidMin, p.UmidMax, p.LuzMax);
                return Json(new { ok = true, mensagem = $"Perfil \"{p.Nome}\" enviado ao dispositivo." });
            }
            catch (FiwareException ex) { return Json(new { ok = false, mensagem = ex.Message }); }
        }
    }
}
