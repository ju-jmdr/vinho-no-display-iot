using Microsoft.AspNetCore.Mvc;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Error() => View("Error", new ErrorViewModel { Mensagem = "Ocorreu um erro inesperado." });
    }
}
