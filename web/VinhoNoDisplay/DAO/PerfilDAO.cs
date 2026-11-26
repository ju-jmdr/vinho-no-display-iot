using System.Text.Json;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.DAO
{
    public class PerfilDAO : PadraoDAO<PerfilViewModel>
    {
        protected override string Tipo => "PerfilArmazenamento";

        protected override Dictionary<string, object> MontaAtributos(PerfilViewModel p) => new()
        {
            ["nome"] = p.Nome, ["tempMin"] = p.TempMin, ["tempMax"] = p.TempMax,
            ["umidMin"] = p.UmidMin, ["umidMax"] = p.UmidMax, ["luzMax"] = p.LuzMax
        };

        protected override PerfilViewModel MontaModel(JsonElement e) => new()
        {
            Id = IdDe(e), Nome = JsonHelper.Texto(e, "nome"),
            TempMin = JsonHelper.Dbl(e, "tempMin"), TempMax = JsonHelper.Dbl(e, "tempMax"),
            UmidMin = JsonHelper.Dbl(e, "umidMin"), UmidMax = JsonHelper.Dbl(e, "umidMax"),
            LuzMax = JsonHelper.Dbl(e, "luzMax")
        };
    }
}
