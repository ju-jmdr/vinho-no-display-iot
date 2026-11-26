using System.Text.Json;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.DAO
{
    public class GarrafaDAO : PadraoDAO<GarrafaViewModel>
    {
        protected override string Tipo => "Garrafa";

        protected override Dictionary<string, object> MontaAtributos(GarrafaViewModel g) => new()
        {
            ["nome"] = g.Nome, ["produtor"] = g.Produtor, ["tipo"] = g.Tipo, ["safra"] = g.Safra,
            ["quantidade"] = g.Quantidade, ["posicao"] = g.Posicao, ["perfilId"] = g.PerfilId
        };

        protected override GarrafaViewModel MontaModel(JsonElement e) => new()
        {
            Id = IdDe(e), Nome = JsonHelper.Texto(e, "nome"), Produtor = JsonHelper.Texto(e, "produtor"),
            Tipo = JsonHelper.Texto(e, "tipo"), Safra = JsonHelper.Inteiro(e, "safra"),
            Quantidade = JsonHelper.Inteiro(e, "quantidade"), Posicao = JsonHelper.Texto(e, "posicao"),
            PerfilId = JsonHelper.Inteiro(e, "perfilId")
        };

        /// <summary>Listagem com o nome do perfil preenchido (join feito em memória).</summary>
        public override List<GarrafaViewModel> Listagem()
        {
            var lista = base.Listagem();
            var perfis = new PerfilDAO().Listagem().ToDictionary(p => p.Id, p => p.Nome);
            foreach (var g in lista) g.NomePerfil = perfis.TryGetValue(g.PerfilId, out var n) ? n : "(perfil removido)";
            return lista;
        }
    }
}
