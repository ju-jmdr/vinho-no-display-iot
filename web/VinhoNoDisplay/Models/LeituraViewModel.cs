namespace VinhoNoDisplay.Models
{
    /// <summary>Uma mensagem IoT do histórico (STH-Comet).</summary>
    public class LeituraViewModel
    {
        public DateTime DataHora { get; set; }
        public double? Temperatura { get; set; }
        public double? Umidade { get; set; }
        public double? Luminosidade { get; set; }
        public int Alarme { get; set; }          // bits: 1 temp, 2 umid, 4 luz, 8 sensor
        public bool TemAlarme => Alarme != 0;

        public List<string> Causas()
        {
            var l = new List<string>();
            if ((Alarme & 1) != 0) l.Add("Temperatura");
            if ((Alarme & 2) != 0) l.Add("Umidade");
            if ((Alarme & 4) != 0) l.Add("Luz");
            if ((Alarme & 8) != 0) l.Add("Sensor");
            return l;
        }
    }
}
