using FancyFinanceWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FancyFinanceWebAPI.Shared.Currency;

namespace FancyFinanceWebAPI.Modules.Incomes
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class IncomesController : ControllerBase
    {
        private readonly FancyFinanceDbContext _context;

        public IncomesController(FancyFinanceDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<IncomeDTO>> CreateIncome([FromBody] CreateIncomeDTO dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var income = new Income
            {
                IncomeSource = dto.IncomeSource,
                Amount = dto.Amount,
                CurrencyId = dto.CurrencyId,
                FrequencyId = dto.FrequencyId,
                UserId = userId,
                CreatedBy = userId,
                UpdatedBy = userId
            };

            _context.Incomes.Add(income);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetIncomeById), new { id = income.IncomeId }, income);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Income>>> GetUserIncomes()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var incomes = await _context.Incomes
                .Where(i => i.UserId == userId)
                .Include(i => i.Currency)
                .Include(i => i.Frequency)
                .OrderBy(i => i.IncomeSource)
                .ToListAsync();

            return Ok(incomes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Income>> GetIncomeById([FromRoute] int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var income = await _context.Incomes
                .Include(i => i.Currency)
                .Include(i => i.Frequency)
                .FirstOrDefaultAsync(i => i.IncomeId == id && i.UserId == userId);

            if (income == null)
                return NotFound();

            return Ok(income);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateIncome([FromRoute] int id, [FromBody] UpdateIncomeDTO dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var income = await _context.Incomes.FirstOrDefaultAsync(i => i.IncomeId == id && i.UserId == userId);
            if (income == null) return NotFound();

            income.IncomeSource = dto.IncomeSource;
            income.Amount = dto.Amount;
            income.CurrencyId = dto.CurrencyId;
            income.FrequencyId = dto.FrequencyId;
            income.UpdatedAt = DateTime.UtcNow;
            income.UpdatedBy = userId;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteIncome([FromRoute] int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var income = await _context.Incomes.FirstOrDefaultAsync(i => i.IncomeId == id && i.UserId == userId);
            if (income == null)
                return NotFound();

            _context.Incomes.Remove(income);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
