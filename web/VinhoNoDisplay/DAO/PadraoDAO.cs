using System.Text.Json;
using VinhoNoDisplay.Models;

namespace VinhoNoDisplay.DAO
{
    /// <summary>
    /// DAO genérico (padrão da apostila), mas em vez de SQL cada operação é uma chamada
    /// à API NGSI-v2 do Orion. Entidade = urn:ngsi-ld:{Tipo}:{Id com 3 dígitos}.
    /// </summary>
    public abstract class PadraoDAO<T> where T : PadraoViewModel
    {
        protected abstract string Tipo { get; }
        protected abstract Dictionary<string, object> MontaAtributos(T model);
        protected abstract T MontaModel(JsonElement e);

        protected string IdEntidade(int id) => $"urn:ngsi-ld:{Tipo}:{id:000}";
        private string Base => ConfiguracaoFiware.OrionUrl + "/v2/entities";

        public virtual void Insert(T model)
        {
            var corpo = MontaAtributos(model);
            corpo["id"] = IdEntidade(model.Id);
            corpo["type"] = Tipo;
            HelperFiware.Executa(HttpMethod.Post, Base + "?options=keyValues", corpo);
        }

        public virtual void Update(T model)
        {
            HelperFiware.Executa(HttpMethod.Post,
                $"{Base}/{IdEntidade(model.Id)}/attrs?options=keyValues", MontaAtributos(model));
        }

        public virtual void Delete(int id)
        {
            HelperFiware.Executa(HttpMethod.Delete, $"{Base}/{IdEntidade(id)}");
        }

        public virtual T Consulta(int id)
        {
            var json = HelperFiware.Get($"{Base}/{IdEntidade(id)}?options=keyValues", true);
            if (json == null) return null;
            using var doc = JsonDocument.Parse(json);
            return MontaModel(doc.RootElement);
        }

        public virtual List<T> Listagem()
        {
            var json = HelperFiware.Get($"{Base}?type={Tipo}&options=keyValues&limit=1000");
            var lista = new List<T>();
            using var doc = JsonDocument.Parse(json);
            foreach (var e in doc.RootElement.EnumerateArray()) lista.Add(MontaModel(e));
            return lista.OrderBy(m => m.Id).ToList();
        }

        public virtual int ProximoId()
        {
            var ids = Listagem().Select(m => m.Id).ToList();
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        /// <summary>Extrai o número do final de urn:ngsi-ld:Tipo:001.</summary>
        protected static int IdDe(JsonElement e)
        {
            var id = JsonHelper.Texto(e, "id");
            return int.TryParse(id[(id.LastIndexOf(':') + 1)..], out var n) ? n : 0;
        }
    }
}
