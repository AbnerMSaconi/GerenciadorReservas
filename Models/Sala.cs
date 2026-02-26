namespace GerenciadorReservas.Models
{
    public class Sala
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal ValorHoraPadrao { get; set; }
    }
}