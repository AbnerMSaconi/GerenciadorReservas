using System.ComponentModel.DataAnnotations;

namespace GerenciadorReservas.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 100 caracteres")]
        public string Nome { get; set; } = string.Empty;
        
        [StringLength(20, ErrorMessage = "CPF/CNPJ inválido")]
        public string? CpfCnpj { get; set; }
        
        [StringLength(20, ErrorMessage = "Telefone inválido")]
        public string? Telefone { get; set; }
        
        // 🔹 PROPRIEDADE QUE ESTAVA FALTANDO (corrige o erro CS1061)
        [StringLength(100, ErrorMessage = "Email inválido")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        public string? Email { get; set; }
    }
}