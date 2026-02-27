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
    /// <summary>
    /// Controller para gerenciamento de reservas de salas de reunião.
    /// Implementa CRUD completo com validações, filtros, paginação e edição inline.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ReservasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReservasController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================================
        // GET: api/reservas
        // ============================================================================
        // Suporta filtros por status, intervalo de datas e paginação (requisitos do PDF)
        // Ex: /api/reservas?pagina=1&tamanhoPagina=10&status=EmAndamento&dataInicio=2026-02-26
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

            //  Filtro por intervalo de datas (requisito obrigatório)
            if (dataInicio.HasValue)
            {
                query = query.Where(r => r.DataInicio >= dataInicio.Value);
            }
            if (dataFim.HasValue)
            {
                query = query.Where(r => r.DataFim <= dataFim.Value);
            }
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Encerradas")
                    query = query.Where(r => r.DataFim < agora);
                else if (status == "Em andamento")
                    query = query.Where(r => r.DataInicio <= agora && r.DataFim >= agora);
                else if (status == "Futuras proximas")
                    query = query.Where(r => r.DataInicio > agora && r.DataInicio <= agora.AddHours(24));
                else if (status == "Futuras normais")
                    query = query.Where(r => r.DataInicio > agora.AddHours(24));
            }

            //  Ordenação padrão por data de início
            query = query.OrderBy(r => r.DataInicio);

            //  Paginação (requisito obrigatório)
            var totalRegistros = await query.CountAsync();
            var reservas = await query
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .ToListAsync();

            //  Projeta os dados com o status calculado conforme regras do PDF
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
                // Status calculado para cores no front-end
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

        // ============================================================================
        // GET: api/reservas/5
        // ============================================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<Reserva>> GetReserva(int id)
        {
            var reserva = await _context.Reservas
                .Include(r => r.Cliente)
                .Include(r => r.Sala)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reserva == null)
                return NotFound(new { message = "Reserva não encontrada." });

            return Ok(reserva);
        }

        // ============================================================================
        // POST: api/reservas
        // ============================================================================
        [HttpPost]
        public async Task<ActionResult<Reserva>> PostReserva(Reserva reserva)
        {
            //  Validação automática do ModelState (Data Annotations)
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    message = "Dados inválidos",
                    errors = ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            e => e.Key,
                            e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray()
                        )
                });
            }

            //  Validação: data_fim deve ser posterior a data_inicio (obrigatório)
            if (reserva.DataFim <= reserva.DataInicio)
            {
                ModelState.AddModelError(nameof(reserva.DataFim),
                    "A data final deve ser posterior à data inicial.");
                return BadRequest(new
                {
                    message = "Validação falhou",
                    errors = new
                    {
                        dataFim = new[] { "A data final deve ser posterior à data inicial." }
                    }
                });
            }

            //  Validação: CONFLITO DE HORÁRIO (regra obrigatória do PDF)
            // Considera apenas 1 sala conforme simplificação do requisito
            bool temConflito = await _context.Reservas
                .AnyAsync(r =>
                    r.SalaId == reserva.SalaId &&
                    r.Id != reserva.Id && // ignora o próprio registro em edições
                    r.DataInicio < reserva.DataFim &&
                    r.DataFim > reserva.DataInicio);

            if (temConflito)
            {
                ModelState.AddModelError(nameof(reserva.DataInicio),
                    "Já existe uma reserva neste horário para esta sala.");
                return BadRequest(new
                {
                    message = "Conflito de horário",
                    errors = new
                    {
                        dataInicio = new[] { "Horário indisponível. Há sobreposição com outra reserva." }
                    }
                });
            }

            try
            {
                //  Calcula valores ANTES de salvar (regra de negócio)
                reserva.CalcularValores(DateTime.UtcNow);

                //  Sanitização segura (evita warnings CS8602)
                reserva.TituloReserva = reserva.TituloReserva?.Trim() ?? string.Empty;
                reserva.Responsavel = reserva.Responsavel?.Trim() ?? string.Empty;
                reserva.StatusPagamento ??= "Pendente";

                _context.Reservas.Add(reserva);
                await _context.SaveChangesAsync();

                //  Retorna 201 Created com localização do recurso (padrão REST)
                return CreatedAtAction(nameof(GetReserva), new { id = reserva.Id }, reserva);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Em produção: usar ILogger para log estruturado
                return StatusCode(500, new
                {
                    message = "Erro interno ao salvar reserva.",
                    error = ex.Message
                });
            }
        }

        // ============================================================================
        // PUT: api/reservas/5
        // ============================================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> PutReserva(int id, [FromBody] Reserva reservaAtualizada)
        {
            // Validação de integridade do ID
            if (id != reservaAtualizada.Id)
            {
                return BadRequest(new { message = "O ID da URL não coincide com o ID do corpo da requisição." });
            }

            // Busca o registro original no banco (Tracked pelo EF)
            var reservaExistente = await _context.Reservas.FindAsync(id);
            if (reservaExistente == null)
            {
                return NotFound(new { message = "Reserva não encontrada no sistema." });
            }

            // 1. Validação de Cronologia
            if (reservaAtualizada.DataFim <= reservaAtualizada.DataInicio)
            {
                return BadRequest(new { message = "A data de término deve ser posterior ao início." });
            }

            // 2. Validação de Conflito Sem Gambiarra:
            // Filtramos por SalaId E verificamos se há sobreposição, 
            // MAS ignoramos explicitamente o ID que estamos editando (r.Id != id).
            bool temConflito = await _context.Reservas.AnyAsync(r =>
                r.SalaId == reservaAtualizada.SalaId &&
                r.Id != id &&
                r.DataInicio < reservaAtualizada.DataFim &&
                r.DataFim > reservaAtualizada.DataInicio);

            if (temConflito)
            {
                return BadRequest(new { message = "Não foi possível atualizar: o novo horário conflita com outra reserva existente." });
            }

            // 3. Atualização dos campos permitidos
            reservaExistente.TituloReserva = reservaAtualizada.TituloReserva?.Trim() ?? string.Empty;
            reservaExistente.Responsavel = reservaAtualizada.Responsavel?.Trim() ?? string.Empty;
            reservaExistente.ClienteId = reservaAtualizada.ClienteId;
            reservaExistente.DataInicio = reservaAtualizada.DataInicio;
            reservaExistente.DataFim = reservaAtualizada.DataFim;
            reservaExistente.ParticipantesPrevistos = reservaAtualizada.ParticipantesPrevistos;
            reservaExistente.ValorHora = reservaAtualizada.ValorHora;

            // 4. Recalcula os valores financeiros com base nos novos dados
            reservaExistente.CalcularValores(DateTime.Now);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(reservaExistente); // Retorna o objeto atualizado para o front-end
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, new { message = "Erro de concorrência ao salvar os dados." });
            }
        }

        // ============================================================================
        // PATCH: api/reservas/5/desconto
        // ============================================================================
        //  Edição inline do desconto (requisito OBRIGATÓRIO)
        // Atualiza apenas o desconto e recalcula valor_total automaticamente
        [HttpPatch("{id}/desconto")]
        public async Task<IActionResult> PatchDesconto(int id, [FromBody] decimal novoDesconto)
        {
            //  Validação do percentual (0% a 30%)
            if (novoDesconto < 0 || novoDesconto > 30)
            {
                return BadRequest(new { message = "Desconto deve estar entre 0% e 30%." });
            }

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null)
                return NotFound(new { message = "Reserva não encontrada." });

            //  Regra de negócio: desconto só para reservas FUTURAS
            var agora = DateTime.UtcNow;
            var status = CalcularStatus(reserva.DataInicio, reserva.DataFim, agora);

            if (status == "Encerradas" || status == "Em andamento")
            {
                return BadRequest(new
                {
                    message = "Desconto só pode ser aplicado em reservas futuras."
                });
            }

            //  Aplica desconto e recalcula valor_total automaticamente
            reserva.Desconto = novoDesconto;
            reserva.CalcularValores(agora);

            try
            {
                await _context.SaveChangesAsync();

                //  Retorna apenas os campos atualizados para o front-end
                return Ok(new
                {
                    reserva.Id,
                    reserva.Desconto,
                    reserva.ValorTotal
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Erro ao atualizar desconto.",
                    error = ex.Message
                });
            }
        }
        // ============================================================================
        // GET: api/reservas/resumo
        // ============================================================================
[HttpGet("resumo")]
        public async Task<ActionResult<object>> GetResumo()
        {
            var agora = DateTime.UtcNow;

            var dados = await _context.Reservas
                .Select(r => new { r.DataInicio, r.DataFim, r.ValorTotal, r.StatusPagamento })
                .ToListAsync();

            var ativas = dados.Count(r => r.DataFim > agora);
            
            // Faturamento Realizado: Blindado contra textos nulos ou diferença de maiúsculas
            var faturamentoRealizado = dados
                .Where(r => r.StatusPagamento != null && r.StatusPagamento.ToLower() == "pago")
                .Sum(r => r.ValorTotal);
            
            // Faturamento Previsto: Tudo que não é explicitamente "pago"
            var faturamentoPrevisto = dados
                .Where(r => r.StatusPagamento == null || r.StatusPagamento.ToLower() != "pago")
                .Sum(r => r.ValorTotal);
            
            var totalHoras = dados.Sum(r => (decimal)(r.DataFim - r.DataInicio).TotalHours);

            return Ok(new
            {
                Ativas = ativas,
                FaturamentoRealizado = faturamentoRealizado,
                FaturamentoPrevisto = faturamentoPrevisto,
                TotalHoras = Math.Round(totalHoras, 1)
            });
        }
        // ============================================================================
        // PATCH: api/reservas/5/pagamento (NOVO ENDPOINT)
        // ============================================================================
        [HttpPost("{id}/pagamento")]
        public async Task<IActionResult> TogglePagamento(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound(new { message = "Reserva não encontrada." });

            // A inteligência fica aqui. O C# escreve a string exata que o banco de dados exige.
            reserva.StatusPagamento = reserva.StatusPagamento == "Pago" ? "Pendente" : "Pago";

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    reserva.Id,
                    reserva.StatusPagamento,
                    reserva.ValorTotal
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno do servidor", error = ex.Message });
            }
        }
        // ============================================================================
        // DELETE: api/reservas/5
        // ============================================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReserva(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null)
                return NotFound(new { message = "Reserva não encontrada." });

            _context.Reservas.Remove(reserva);
            await _context.SaveChangesAsync();

            return NoContent(); // 204 - Excluído com sucesso
        }

        // ============================================================================
        // MÉTODO AUXILIAR: CalcularStatus
        // ============================================================================
        //  Implementa as regras de exibição por cores (requisito do PDF):
        // • Em andamento: agora está entre data_inicio e data_fim
        // • Futuras próximas: faltam < 24h para data_inicio
        // • Futuras normais: faltam >= 24h para data_inicio  
        // • Encerradas: data_fim já passou
        private string CalcularStatus(DateTime dataInicio, DateTime dataFim, DateTime agora)
        {
            if (dataFim < agora)
                return "Encerradas";

            if (dataInicio <= agora && dataFim >= agora)
                return "Em andamento";

            var horasParaInicio = (dataInicio - agora).TotalHours;

            if (horasParaInicio < 24)
                return "Futuras proximas";

            return "Futuras normais";
        }

        [HttpGet("grafico")]
        public async Task<ActionResult> GetDadosGrafico()
        {
            try
            {
                var limiteInicio = DateTime.UtcNow.AddDays(-15);
                var limiteFim = DateTime.UtcNow.AddDays(15);

                //trazer as reservas brutas para a memoria
                var reservas = await _context.Reservas
                    .Where(r => r.DataInicio >= limiteInicio && r.DataInicio <= limiteFim)
                    .Select(r => new { r.DataInicio, r.ValorTotal })
                    .ToListAsync();
                //agrupamento seguro
                var dados = reservas
                    .GroupBy(r => r.DataInicio.Date)
                    .Select(g => new
                    {
                        Data = g.Key.ToString("dd/MM"),
                        Quantidade = g.Count(),
                        Faturamento = g.Sum(r => r.ValorTotal)
                    })
                    .OrderBy(x => DateTime.ParseExact(x.Data, "dd/MM", null))
                    .ToList();

                return Ok(dados);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao processar dados do gráfico", error = ex.Message });
            }
        }
    }
}