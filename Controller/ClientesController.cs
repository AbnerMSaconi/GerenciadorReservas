using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GerenciadorReservas.Data;
using GerenciadorReservas.Models;

namespace GerenciadorReservas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetClientes()
        {
            // Retorna a lista (mesmo que vazia []) para o front-end
            var clientes = await _context.Clientes.ToListAsync();
            return Ok(clientes);
        }

        [HttpPost]
        public async Task<IActionResult> PostCliente([FromBody] Cliente cliente)
        {
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            return Ok(cliente);
        }

        // GET: api/clientes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound(new { message = "Cliente não encontrado." });
            return Ok(cliente);
        }

        // PUT: api/clientes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(int id, [FromBody] Cliente clienteAtualizado)
        {
            if (id != clienteAtualizado.Id)
                return BadRequest(new { message = "ID inconsistente." });

            var clienteExistente = await _context.Clientes.FindAsync(id);
            if (clienteExistente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            clienteExistente.Nome = clienteAtualizado.Nome?.Trim() ?? string.Empty;
            clienteExistente.CpfCnpj = clienteAtualizado.CpfCnpj?.Trim();
            clienteExistente.Telefone = clienteAtualizado.Telefone?.Trim();

            try
            {
                await _context.SaveChangesAsync();
                return Ok(clienteExistente);
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, new { message = "Erro de concorrência ao atualizar cliente." });
            }
        }

        // DELETE: api/clientes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            // VALIDAÇÃO ESTRATÉGICA: Impede quebra de integridade referencial do SQL Server
            bool possuiReservas = await _context.Reservas.AnyAsync(r => r.ClienteId == id);
            if (possuiReservas)
            {
                return BadRequest(new { message = "Não é possível excluir este cliente pois ele possui reservas vinculadas. Exclua as reservas primeiro." });
            }

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}