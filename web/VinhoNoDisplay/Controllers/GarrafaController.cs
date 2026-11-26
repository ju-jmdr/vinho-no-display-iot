using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VinhoNoDisplay.DAO;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.Controllers
{
    public class GarrafaController : PadraoController<GarrafaViewModel>
    {
        public static readonly string[] Tipos = { "Tinto", "Branco", "Rosé", "Espumante", "Sobremesa" };

        public GarrafaController() { DAO = new GarrafaDAO(); }

        protected override void PreencheDadosParaView(string operacao, GarrafaViewModel model)
        {
            ViewBag.Tipos = Tipos.Select(t => new SelectListItem(t, t, t == model.Tipo)).ToList();
            ViewBag.Perfis = new PerfilDAO().Listagem()
                .Select(p => new SelectListItem(p.Nome, p.Id.ToString(), p.Id == model.PerfilId)).ToList();
        }

        protected override void ValidaDados(GarrafaViewModel m, string operacao)
        {
            base.ValidaDados(m, operacao);
            ValidaTexto("Nome", m.Nome, "o nome do vinho");
            ValidaTexto("Produtor", m.Produtor, "o produtor");
            ValidaTexto("Posicao", m.Posicao, "a posição na adega", 30);
            if (!Tipos.Contains(m.Tipo)) ModelState.AddModelError("Tipo", "Escolha um tipo válido.");
            if (m.Safra < 1900 || m.Safra > DateTime.Now.Year)
                ModelState.AddModelError("Safra", $"A safra deve estar entre 1900 e {DateTime.Now.Year}.");
            if (m.Quantidade < 1) ModelState.AddModelError("Quantidade", "A quantidade deve ser de pelo menos 1 garrafa.");
            if (new PerfilDAO().Consulta(m.PerfilId) == null)
                ModelState.AddModelError("PerfilId", "Escolha um perfil de armazenamento existente.");
        }
    }
}
