using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GerenciadorReservas.Models
{
    public class Reserva
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Cliente é obrigatório")]
        public int ClienteId { get; set; }
        
        [ForeignKey(nameof(ClienteId))] // 🔹 GUIA O EF CORE AQUI
        public Cliente? Cliente { get; set; }
        
        [Required(ErrorMessage = "Sala é obrigatória")]
        public int SalaId { get; set; }
        
        [ForeignKey(nameof(SalaId))] // 🔹 GUIA O EF CORE AQUI
        public Sala? Sala { get; set; }

        [Required, StringLength(150, MinimumLength = 3)]
        public string TituloReserva { get; set; } = string.Empty;
        
        [Required, StringLength(100)]
        public string Responsavel { get; set; } = string.Empty; 
        
        [Required]
        public DateTime DataInicio { get; set; }
        
        [Required]
        public DateTime DataFim { get; set; }
        
        [Range(1, 100)]
        public int ParticipantesPrevistos { get; set; }
        
        [Range(0.01, 9999.99)]
        public decimal ValorHora { get; set; }
        
        [Range(0, 30)]
        public decimal Desconto { get; set; }
        
        public decimal ValorTotal { get; private set; }
        
        [StringLength(50)]
        public string? StatusPagamento { get; set; } = "Pendente";

        public void CalcularValores(DateTime agora)
        {
            if (DataFim <= DataInicio)
                throw new ArgumentException("Data final deve ser posterior à inicial.");
            
            var horas = (decimal)(DataFim - DataInicio).TotalHours;
            var valorBruto = horas * ValorHora;

            if (DataInicio > agora && Desconto > 0 && Desconto <= 30)
            {
                ValorTotal = valorBruto - (valorBruto * Desconto / 100);
            }
            else
            {
                Desconto = 0;
                ValorTotal = valorBruto;
            }
        }
    }
}