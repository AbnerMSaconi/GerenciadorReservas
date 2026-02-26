using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GerenciadorReservas.Data;
using GerenciadorReservas.Models;

namespace GerenciadorReservas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReservasController(AppDbContext context)
        {
            _context = context;
        }

        // POST: Cria a reserva validando horários e calculando valores
        [HttpPost]
        public async Task<IActionResult> CriarReserva([FromBody] Reserva reserva)
        {
            if (reserva.DataFim <= reserva.DataInicio)
                return BadRequest("A data final deve ser posterior à data inicial.");

            bool sobreposicao = await _context.Reservas.AnyAsync(r => 
                r.SalaId == reserva.SalaId && 
                reserva.DataInicio < r.DataFim && 
                reserva.DataFim > r.DataInicio);

            if (sobreposicao)
                return Conflict("Choque de horários: Já existe uma reserva ativa para este período.");

            try
            {
                reserva.CalcularValores(); 
                _context.Reservas.Add(reserva);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(ListarReservas), new { id = reserva.Id }, reserva);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: Listagem com filtros avançados e paginação
        [HttpGet]
        public async Task<IActionResult> ListarReservas(
            [FromQuery] string? status, 
            [FromQuery] DateTime? dataInicio, 
            [FromQuery] DateTime? dataFim,
            [FromQuery] int pagina = 1, 
            [FromQuery] int tamanhoPagina = 10)
        {
            var query = _context.Reservas.AsQueryable();

            if (dataInicio.HasValue)
                query = query.Where(r => r.DataInicio >= dataInicio.Value);
            if (dataFim.HasValue)
                query = query.Where(r => r.DataFim <= dataFim.Value);

            var agora = DateTime.Now;

            if (!string.IsNullOrEmpty(status))
            {
                status = status.ToLower();
                if (status == "em andamento")
                    query = query.Where(r => r.DataInicio <= agora && r.DataFim >= agora);
                else if (status == "futuras proximas")
                    query = query.Where(r => r.DataInicio > agora && r.DataInicio <= agora.AddHours(24));
                else if (status == "futuras normais")
                    query = query.Where(r => r.DataInicio > agora.AddHours(24));
                else if (status == "encerradas")
                    query = query.Where(r => r.DataFim < agora);
            }

            var totalItems = await query.CountAsync();

            var reservas = await query
                .OrderBy(r => r.DataInicio)
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .Select(r => new {
                    r.Id,
                    r.TituloReserva,
                    r.DataInicio,
                    r.DataFim,
                    r.ParticipantesPrevistos,
                    r.ValorHora,
                    r.Desconto,
                    r.ValorTotal,
                    r.StatusPagamento,
                    StatusCalculado = (r.DataInicio <= agora && r.DataFim >= agora) ? "Em andamento" :
                                      (r.DataFim < agora) ? "Encerradas" :
                                      (r.DataInicio > agora && r.DataInicio <= agora.AddHours(24)) ? "Futuras proximas" : "Futuras normais"
                })
                .ToListAsync();

            return Ok(new { 
                Total = totalItems, 
                PaginaAtual = pagina, 
                TotalPaginas = (int)Math.Ceiling(totalItems / (double)tamanhoPagina),
                Dados = reservas 
            });
        }

        // PATCH: Atualização inline do desconto
        [HttpPatch("{id}/desconto")]
        public async Task<IActionResult> AtualizarDescontoInline(int id, [FromBody] decimal novoDesconto)
        {
            var reserva = await _context.Reservas.FindAsync(id);

            if (reserva == null)
                return NotFound("Reserva não encontrada no banco de dados.");

            if (novoDesconto < 0 || novoDesconto > 30)
                return BadRequest("O desconto deve ser um percentual de 0% até 30%.");

            reserva.Desconto = novoDesconto;

            try
            {
                reserva.CalcularValores(); 
                await _context.SaveChangesAsync();
                
                return Ok(new { 
                    Id = reserva.Id,
                    NovoDesconto = reserva.Desconto, 
                    NovoValorTotal = reserva.ValorTotal 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}