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

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<Income>>> GetUserIncomes([FromRoute] Guid userId)
        {
            return await _context.Incomes
                .Where(i => i.UserId == userId)
                .OrderBy(i => i.IncomeSource)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Currency>> GetSelectedIncome([FromRoute] int id)
        {
            var currency = await _context.Currencies.FindAsync(id);

            if (currency == null)
            {
                return NotFound();
            }

            return Ok(currency);
        }

        [HttpPost]
        public async Task<ActionResult<Currency>> CreateIncome([FromBody] Income income)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null) return Unauthorized();

            income.CreatedBy = Guid.Parse(userId);
            _context.Incomes.Add(income);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSelectedIncome), new { id = income.CurrencyId }, income);
        }
    }
}
