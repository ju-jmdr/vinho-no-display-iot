using System.Net;
using System.Text;
using System.Text.Json;

namespace VinhoNoDisplay.DAO
{
    public class FiwareException : Exception
    {
        public FiwareException(string mensagem) : base(mensagem) { }
    }

    /// <summary>Chamadas HTTP ao Orion e ao STH-Comet com os cabeçalhos fiware-service/servicepath.</summary>
    public static class HelperFiware
    {
        private static readonly HttpClient cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Executa a requisição. Retorna o corpo (string vazia se não houver).
        /// Retorna null se aceitaNaoEncontrado e o servidor responder 404.
        /// </summary>
        public static string Executa(HttpMethod metodo, string url, object corpo = null, bool aceitaNaoEncontrado = false)
        {
            var req = new HttpRequestMessage(metodo, url);
            req.Headers.Add("fiware-service", ConfiguracaoFiware.Service);
            req.Headers.Add("fiware-servicepath", ConfiguracaoFiware.ServicePath);
            if (corpo != null)
                req.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try { resp = cliente.Send(req); }
            catch (Exception ex)
            {
                throw new FiwareException("Não foi possível falar com o FIWARE em " + new Uri(url).Authority +
                    ". Verifique o IP no appsettings.json e as portas do Security Group. (" + ex.Message + ")");
            }

            string texto = new StreamReader(resp.Content.ReadAsStream()).ReadToEnd();
            if (resp.StatusCode == HttpStatusCode.NotFound && aceitaNaoEncontrado) return null;
            if (!resp.IsSuccessStatusCode)
                throw new FiwareException($"FIWARE respondeu {(int)resp.StatusCode}: {texto}");
            return texto;
        }

        public static string Get(string url, bool aceitaNaoEncontrado = false)
            => Executa(HttpMethod.Get, url, null, aceitaNaoEncontrado);
    }
}
