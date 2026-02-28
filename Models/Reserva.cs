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
        
        [Required(ErrorMessage = "Título da reserva é obrigatório"),
        StringLength(150, MinimumLength = 3, ErrorMessage = "Título deve ter entre 3 e 150 caracteres")]
        public string TituloReserva { get; set; } = string.Empty;
        
        [Required, StringLength(100,ErrorMessage = "Responsável deve ter no máximo 100 caracteres")]
        public string Responsavel { get; set; } = string.Empty; 
        
        [Required]
        public DateTime DataInicio { get; set; }
        
        [Required]
        public DateTime DataFim { get; set; }
        
        [Required(ErrorMessage = "Participantes previstos é obrigatório")]
        [Range(1, 100, ErrorMessage = "Participantes previstos devem ser entre 1 e 100")]
        public int ParticipantesPrevistos { get; set; }
        
        [Required(ErrorMessage = "Valor por hora é obrigatório")]
        [Range(0.01, 9999.99, ErrorMessage = "Valor por hora deve estar entre R$ 0,01 e R$ 9.999,99")]
        public decimal ValorHora { get; set; }
        
        [Range(0, 30, ErrorMessage = "Desconto deve ser entre 0% e 30%")]
        public decimal Desconto { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorTotal { get; private set; }
        [MaxLength(20)]
        public string? StatusPagamento { get; set; } = "Pendente";

        public void CalcularValores(DateTime agora)
        {   
            if (DataInicio <= agora && Id == 0)
                throw new ArgumentException("Data inicial deve ser no futuro.");

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