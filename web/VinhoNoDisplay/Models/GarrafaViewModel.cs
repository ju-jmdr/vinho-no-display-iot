namespace VinhoNoDisplay.Models
{
    public class GarrafaViewModel : PadraoViewModel
    {
        public string Nome { get; set; }
        public string Produtor { get; set; }
        public string Tipo { get; set; }
        public int Safra { get; set; }
        public int Quantidade { get; set; }
        public string Posicao { get; set; }
        public int PerfilId { get; set; }
        public string NomePerfil { get; set; }   // só para exibição (não é gravado)
    }
}
