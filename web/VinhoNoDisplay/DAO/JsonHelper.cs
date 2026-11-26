using System.Globalization;
using System.Text.Json;

namespace VinhoNoDisplay.DAO
{
    /// <summary>Leitura tolerante de propriedades de um JsonElement (formato keyValues do Orion).</summary>
    public static class JsonHelper
    {
        public static string Texto(JsonElement e, string nome)
        {
            if (!e.TryGetProperty(nome, out var p)) return "";
            return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
        }

        public static double? ParaDouble(JsonElement e, string nome)
        {
            if (!e.TryGetProperty(nome, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number) return p.GetDouble();
            if (p.ValueKind == JsonValueKind.String &&
                double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return null;
        }

        public static double Dbl(JsonElement e, string nome) => ParaDouble(e, nome) ?? 0;
        public static int Inteiro(JsonElement e, string nome) => (int)Math.Round(Dbl(e, nome));
    }
}
