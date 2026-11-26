using Microsoft.AspNetCore.Mvc;
using VinhoNoDisplay.DAO;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.Controllers
{
    /// <summary>
    /// Controller genérico de CRUD (padrão da apostila). As telas filhas só definem
    /// a DAO, os nomes das views e as validações.
    /// </summary>
    public abstract class PadraoController<T> : Controller where T : PadraoViewModel
    {
        protected PadraoDAO<T> DAO { get; set; }
        protected string NomeViewIndex { get; set; } = "Index";
        protected string NomeViewForm { get; set; } = "Form";
        private const string CaracteresInvalidos = "<>\"'=;()";

        public virtual IActionResult Index()
        {
            try { return View(NomeViewIndex, DAO.Listagem()); }
            catch (FiwareException ex) { return View("Error", new ErrorViewModel { Mensagem = ex.Message }); }
        }

        public virtual IActionResult Create()
        {
            try
            {
                ViewBag.Operacao = "I";
                var model = Activator.CreateInstance<T>();
                model.Id = DAO.ProximoId();
                PreencheDadosParaView("I", model);
                return View(NomeViewForm, model);
            }
            catch (FiwareException ex) { return View("Error", new ErrorViewModel { Mensagem = ex.Message }); }
        }

        public virtual IActionResult Edit(int id)
        {
            try
            {
                var model = DAO.Consulta(id);
                if (model == null) return RedirectToAction(NomeViewIndex);
                ViewBag.Operacao = "A";
                PreencheDadosParaView("A", model);
                return View(NomeViewForm, model);
            }
            catch (FiwareException ex) { return View("Error", new ErrorViewModel { Mensagem = ex.Message }); }
        }

        public virtual IActionResult Save(T model, string Operacao)
        {
            try
            {
                ValidaDados(model, Operacao);
                if (!ModelState.IsValid)
                {
                    ViewBag.Operacao = Operacao;
                    PreencheDadosParaView(Operacao, model);
                    return View(NomeViewForm, model);
                }
                if (Operacao == "I") DAO.Insert(model); else DAO.Update(model);
                return RedirectToAction(NomeViewIndex);
            }
            catch (FiwareException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Operacao = Operacao;
                PreencheDadosParaView(Operacao, model);
                return View(NomeViewForm, model);
            }
        }

        public virtual IActionResult Delete(int id)
        {
            try { DAO.Delete(id); return RedirectToAction(NomeViewIndex); }
            catch (FiwareException ex) { return View("Error", new ErrorViewModel { Mensagem = ex.Message }); }
        }

        protected virtual void ValidaDados(T model, string operacao)
        {
            ModelState.Clear();
            if (operacao == "I" && DAO.Consulta(model.Id) != null)
                ModelState.AddModelError("Id", "Já existe um registro com este código.");
            if (operacao == "A" && DAO.Consulta(model.Id) == null)
                ModelState.AddModelError("Id", "Registro não encontrado.");
        }

        protected virtual void PreencheDadosParaView(string operacao, T model) { }

        /// <summary>O Orion rejeita &lt; &gt; " ' = ; ( ) em textos; barramos aqui com mensagem clara.</summary>
        protected void ValidaTexto(string campo, string valor, string rotulo, int max = 60, bool obrigatorio = true)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                if (obrigatorio) ModelState.AddModelError(campo, $"Informe {rotulo}.");
                return;
            }
            if (valor.Length > max) ModelState.AddModelError(campo, $"{rotulo} deve ter no máximo {max} caracteres.");
            if (valor.IndexOfAny(CaracteresInvalidos.ToCharArray()) >= 0)
                ModelState.AddModelError(campo, $"{rotulo} não pode conter os caracteres < > \" ' = ; ( ).");
        }
    }
}
