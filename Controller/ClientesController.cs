using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GerenciadorReservas.Data;
using GerenciadorReservas.Models;

namespace GerenciadorReservas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Controlador de clientes: expõe endpoints para gerenciamento CRUD de clientes.
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Construtor: injeta o AppDbContext para acesso ao banco de dados.
        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Lista todos os clientes (retorna array vazio se não houver registros).
        [HttpGet]
        public async Task<IActionResult> GetClientes()
        {
            var clientes = await _context.Clientes.ToListAsync();
            return Ok(clientes);
        }

        // POST: Cria um novo cliente e persiste no banco.
        [HttpPost]
        public async Task<IActionResult> PostCliente([FromBody] Cliente cliente)
        {
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            return Ok(cliente);
        }

        // GET by id: Recupera um cliente específico pelo seu id.
        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound(new { message = "Cliente não encontrado." });
            return Ok(cliente);
        }

        // PUT: Atualiza um cliente existente; valida ID e trata concorrência.
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

        // DELETE: Remove um cliente se não houver reservas vinculadas; protege integridade referencial.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            // Impede exclusão quando há reservas vinculadas ao cliente.
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