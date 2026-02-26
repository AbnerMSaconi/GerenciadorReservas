using System;

namespace GerenciadorReservas.Models
{
    public class Reserva
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; } // Propriedade de navegação
        
        public int SalaId { get; set; }
        public Sala? Sala { get; set; } // Propriedade de navegação

        public string TituloReserva { get; set; } = string.Empty;
        public DateTime DataInicio { get; set; }
        public DateTime DataFim { get; set; }
        public int ParticipantesPrevistos { get; set; }
        
        public decimal ValorHora { get; set; }
        public decimal Desconto { get; set; }
        public decimal ValorTotal { get; private set; } // Set privado para forçar o uso do método de cálculo
        
        public string StatusPagamento { get; set; } = "Pendente";

        // Aplicação direta da regra de negócio exigida
        public void CalcularValores()
        {
            if (DataFim <= DataInicio)
                throw new ArgumentException("A data final deve ser posterior à data inicial.");

            TimeSpan diferenca = DataFim - DataInicio;
            decimal horas = (decimal)diferenca.TotalHours;
            
            decimal valorBruto = horas * ValorHora;

            // Regra: Desconto só para reservas futuras (início > agora) e limite de 30%
            if (DataInicio > DateTime.Now && Desconto > 0 && Desconto <= 30)
            {
                decimal valorDesconto = valorBruto * (Desconto / 100);
                ValorTotal = valorBruto - valorDesconto;
            }
            else
            {
                Desconto = 0; // Zera o desconto se não cumprir as regras
                ValorTotal = valorBruto;
            }
        }
    }
}