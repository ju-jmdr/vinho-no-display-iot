namespace VinhoNoDisplay.Models
{
    public class DispositivoViewModel
    {
        public bool Cadastrado { get; set; }
        public double TempMin { get; set; }
        public double TempMax { get; set; }
        public double UmidMin { get; set; }
        public double UmidMax { get; set; }
        public double LuzMax { get; set; }
        public int Alarme { get; set; }          // eco do ESP32: bits 1 temp, 2 umid, 4 luz, 8 sensor
        public string StatusConfig { get; set; }
        public string StatusAlerta { get; set; }
    }
}
