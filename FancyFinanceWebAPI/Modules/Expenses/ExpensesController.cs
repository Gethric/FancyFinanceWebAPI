using FancyFinanceWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FancyFinanceWebAPI.Modules.Incomes;

namespace FancyFinanceWebAPI.Modules.Expenses
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExpensesController : ControllerBase
    {
        private readonly FancyFinanceDbContext _context;

        public ExpensesController(FancyFinanceDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ExpenseDTO>> CreateExpense([FromBody] CreateExpenseDTO dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var expense = new Expense()
            {
                Description = dto.Description,
                Amount = dto.Amount,
                CategoryId = dto.CategoryId,
                CurrencyId = dto.CurrencyId,
                FrequencyId = dto.FrequencyId,
                UserId = userId,
                CreatedBy = userId,
                UpdatedBy = userId
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetExpenseById), new { id = expense.ExpenseId }, expense);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Expense>>> GetUserExpenses()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var expenses = await _context.Expenses
                .Where(i => i.UserId == userId)
                .Include(i => i.Category)
                .Include(i => i.Currency)
                .Include(i => i.Frequency)
                .OrderBy(i => i.Description)
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Expense>> GetExpenseById([FromRoute] int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var expense = await _context.Expenses
                .Include (e => e.Category)
                .Include(e => e.Currency)
                .Include(e => e.Frequency)
                .FirstOrDefaultAsync(e => e.ExpenseId == id && e.UserId == userId);

            if (expense == null)
                return NotFound();

            return Ok(expense);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense([FromRoute] int id, [FromBody] UpdateExpenseDTO dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var expense = await _context.Expenses.FirstOrDefaultAsync(i => i.ExpenseId == id && i.UserId == userId);
            if (expense == null) return NotFound();

            expense.Description = dto.Description;
            expense.Amount = dto.Amount;
            expense.CategoryId = dto.CategoryId;
            expense.CurrencyId = dto.CurrencyId;
            expense.FrequencyId = dto.FrequencyId;
            expense.UpdatedAt = DateTime.UtcNow;
            expense.UpdatedBy = userId;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense([FromRoute] int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var expense = await _context.Expenses.FirstOrDefaultAsync(i => i.ExpenseId == id && i.UserId == userId);
            if (expense == null)
                return NotFound();

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
