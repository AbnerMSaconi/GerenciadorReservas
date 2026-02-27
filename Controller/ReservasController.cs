using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GerenciadorReservas.Data;
using GerenciadorReservas.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

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

        [HttpGet]
        public async Task<ActionResult<object>> GetReservas(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanhoPagina = 10,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? dataInicio = null,
            [FromQuery] DateTime? dataFim = null)
        {
            var agora = DateTime.UtcNow;

            var query = _context.Reservas
                .Include(r => r.Cliente)
                .Include(r => r.Sala)
                .AsQueryable();

            if (dataInicio.HasValue) query = query.Where(r => r.DataInicio >= dataInicio.Value);
            if (dataFim.HasValue) query = query.Where(r => r.DataFim <= dataFim.Value);
            
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Encerradas") query = query.Where(r => r.DataFim < agora);
                else if (status == "Em andamento") query = query.Where(r => r.DataInicio <= agora && r.DataFim >= agora);
                else if (status == "Futuras proximas") query = query.Where(r => r.DataInicio > agora && r.DataInicio <= agora.AddHours(24));
                else if (status == "Futuras normais") query = query.Where(r => r.DataInicio > agora.AddHours(24));
            }

            query = query.OrderBy(r => r.DataInicio);

            var totalRegistros = await query.CountAsync();
            var reservas = await query
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .ToListAsync();

            var reservasDto = reservas.Select(r => new
            {
                r.Id,
                r.ClienteId,
                ClienteNome = r.Cliente?.Nome ?? "N/A",
                r.SalaId,
                r.TituloReserva,
                r.Responsavel,
                r.DataInicio,
                r.DataFim,
                r.ParticipantesPrevistos,
                r.ValorHora,
                r.Desconto,
                r.ValorTotal,
                r.StatusPagamento,
                StatusCalculado = CalcularStatus(r.DataInicio, r.DataFim, agora)
            }).ToList();

            return Ok(new
            {
                dados = reservasDto,
                totalRegistros,
                totalPaginas = (int)Math.Ceiling(totalRegistros / (double)tamanhoPagina),
                paginaAtual = pagina,
                tamanhoPagina
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Reserva>> GetReserva(int id)
        {
            var reserva = await _context.Reservas
                .Include(r => r.Cliente)
                .Include(r => r.Sala)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reserva == null) return NotFound(new { message = "Reserva não encontrada." });
            return Ok(reserva);
        }

        [HttpPost]
        public async Task<ActionResult<Reserva>> PostReserva(Reserva reserva)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Dados inválidos", errors = ModelState });

            if (reserva.DataFim <= reserva.DataInicio)
                return BadRequest(new { message = "A data final deve ser posterior à data inicial." });

            bool temConflito = await _context.Reservas
                .AnyAsync(r => r.SalaId == reserva.SalaId && r.Id != reserva.Id && 
                               r.DataInicio < reserva.DataFim && r.DataFim > reserva.DataInicio);

            if (temConflito)
                return BadRequest(new { message = "Já existe uma reserva neste horário para esta sala." });

            try
            {
                reserva.CalcularValores(DateTime.UtcNow);
                reserva.TituloReserva = reserva.TituloReserva?.Trim() ?? string.Empty;
                reserva.Responsavel = reserva.Responsavel?.Trim() ?? string.Empty;
                reserva.StatusPagamento ??= "Pendente";

                _context.Reservas.Add(reserva);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetReserva), new { id = reserva.Id }, reserva);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno ao salvar reserva.", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutReserva(int id, [FromBody] Reserva reservaAtualizada)
        {
            if (id != reservaAtualizada.Id) return BadRequest(new { message = "ID inválido." });

            var reservaExistente = await _context.Reservas.FindAsync(id);
            if (reservaExistente == null) return NotFound(new { message = "Reserva não encontrada." });

            if (reservaAtualizada.DataFim <= reservaAtualizada.DataInicio)
                return BadRequest(new { message = "A data final deve ser posterior à data inicial." });

            bool temConflito = await _context.Reservas.AnyAsync(r =>
                r.SalaId == reservaAtualizada.SalaId && r.Id != id &&
                r.DataInicio < reservaAtualizada.DataFim && r.DataFim > reservaAtualizada.DataInicio);

            if (temConflito) return BadRequest(new { message = "Conflito de horário." });

            reservaExistente.TituloReserva = reservaAtualizada.TituloReserva?.Trim() ?? string.Empty;
            reservaExistente.Responsavel = reservaAtualizada.Responsavel?.Trim() ?? string.Empty;
            reservaExistente.ClienteId = reservaAtualizada.ClienteId;
            reservaExistente.DataInicio = reservaAtualizada.DataInicio;
            reservaExistente.DataFim = reservaAtualizada.DataFim;
            reservaExistente.ParticipantesPrevistos = reservaAtualizada.ParticipantesPrevistos;
            reservaExistente.ValorHora = reservaAtualizada.ValorHora;

            reservaExistente.CalcularValores(DateTime.Now);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(reservaExistente);
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, new { message = "Erro de concorrência ao salvar os dados." });
            }
        }

        [HttpPatch("{id}/desconto")]
        public async Task<IActionResult> PatchDesconto(int id, [FromBody] decimal novoDesconto)
        {
            if (novoDesconto < 0 || novoDesconto > 30) return BadRequest(new { message = "Desconto deve estar entre 0% e 30%." });

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound(new { message = "Reserva não encontrada." });

            var agora = DateTime.UtcNow;
            var status = CalcularStatus(reserva.DataInicio, reserva.DataFim, agora);

            if (status == "Encerradas" || status == "Em andamento")
                return BadRequest(new { message = "Desconto só pode ser aplicado em reservas futuras." });

            reserva.Desconto = novoDesconto;
            reserva.CalcularValores(agora);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { reserva.Id, reserva.Desconto, reserva.ValorTotal });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao atualizar desconto.", error = ex.Message });
            }
        }

[HttpGet("resumo")]
        public async Task<ActionResult<object>> GetResumo([FromQuery] DateTime? dataInicio = null, [FromQuery] DateTime? dataFim = null)
        {
            var agora = DateTime.UtcNow;
            var query = _context.Reservas.AsQueryable();

            if (dataInicio.HasValue) query = query.Where(r => r.DataInicio >= dataInicio.Value);
            if (dataFim.HasValue) query = query.Where(r => r.DataFim <= dataFim.Value);

            var dados = await query
                .Select(r => new { r.DataInicio, r.DataFim, r.ValorTotal, r.StatusPagamento })
                .ToListAsync();

            var ativas = dados.Count(r => r.DataFim > agora);
            var faturamentoRealizado = dados.Where(r => r.StatusPagamento != null && r.StatusPagamento.ToLower() == "pago").Sum(r => r.ValorTotal);
            var faturamentoPrevisto = dados.Where(r => r.StatusPagamento == null || r.StatusPagamento.ToLower() != "pago").Sum(r => r.ValorTotal);
            var totalHoras = dados.Sum(r => (decimal)(r.DataFim - r.DataInicio).TotalHours);

            return Ok(new { Ativas = ativas, FaturamentoRealizado = faturamentoRealizado, FaturamentoPrevisto = faturamentoPrevisto, TotalHoras = Math.Round(totalHoras, 1) });
        }

        [HttpGet("grafico")]
        public async Task<ActionResult> GetDadosGrafico([FromQuery] DateTime? dataInicio = null, [FromQuery] DateTime? dataFim = null)
        {
            try
            {
                var limiteInicio = dataInicio ?? DateTime.UtcNow.AddDays(-15);
                var limiteFim = dataFim ?? DateTime.UtcNow.AddDays(15);

                var reservas = await _context.Reservas
                    .Where(r => r.DataInicio >= limiteInicio && r.DataInicio <= limiteFim)
                    .ToListAsync();
                
                var diasDiferenca = (limiteFim - limiteInicio).TotalDays;
                object dados;

                // Se o filtro buscar mais de 60 dias, agrupa por Mês automaticamente
                if (diasDiferenca > 60)
                {
                    dados = reservas
                        .GroupBy(r => new { r.DataInicio.Year, r.DataInicio.Month })
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                        .Select(g => new {
                            data = $"{g.Key.Month:00}/{g.Key.Year}",
                            quantidade = g.Count(),
                            faturamento = g.Sum(r => r.ValorTotal)
                        }).ToList();
                }
                else 
                {
                    // Caso contrário, mostra dia a dia
                    dados = reservas
                        .GroupBy(r => r.DataInicio.Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new {
                            data = g.Key.ToString("dd/MM"),
                            quantidade = g.Count(),
                            faturamento = g.Sum(r => r.ValorTotal)
                        }).ToList();
                }

                return Ok(dados);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao processar gráfico", error = ex.Message });
            }
        }

        [HttpPost("{id}/pagamento")]
        public async Task<IActionResult> TogglePagamento(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound(new { message = "Reserva não encontrada." });

            reserva.StatusPagamento = reserva.StatusPagamento == "Pago" ? "Pendente" : "Pago";

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { reserva.Id, reserva.StatusPagamento, reserva.ValorTotal });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno do servidor", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReserva(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound(new { message = "Reserva não encontrada." });

            _context.Reservas.Remove(reserva);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private string CalcularStatus(DateTime dataInicio, DateTime dataFim, DateTime agora)
        {
            if (dataFim < agora) return "Encerradas";
            if (dataInicio <= agora && dataFim >= agora) return "Em andamento";
            if ((dataInicio - agora).TotalHours < 24) return "Futuras proximas";
            return "Futuras normais";
        }

    }
}