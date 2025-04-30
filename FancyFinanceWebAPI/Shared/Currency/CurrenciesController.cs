using FancyFinanceWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FancyFinanceWebAPI.Shared.Currency
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CurrenciesController : ControllerBase
    {
        private readonly FancyFinanceDbContext _context;

        public CurrenciesController(FancyFinanceDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Currency>>> GetCurrencies()
        {
            return await _context.Currencies
                .OrderBy(c => c.CurrencyName)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Currency>> GetSelectedCurrency([FromRoute] int id)
        {
            var currency = await _context.Currencies.FindAsync(id);

            if (currency == null)
            {
                return NotFound();
            }

            return Ok(currency);
        }

        [HttpPost]
        public async Task<ActionResult<Currency>> CreateCurrency([FromBody] Currency currency)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null) return Unauthorized();

            currency.CreatedBy = Guid.Parse(userId);
            _context.Currencies.Add(currency);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSelectedCurrency), new { id = currency.CurrencyId }, currency);
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<Currency>> UpdateCurrency(int id, [FromBody] Currency patch)
        {
            var existing = await _context.Currencies.FindAsync(id);
            if (existing == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            // ID check just to be safe (optional)
            if (patch.CurrencyId != 0 && patch.CurrencyId != id)
                return BadRequest("CurrencyId cannot be changed");

            // Only update allowed fields
            if (!string.IsNullOrWhiteSpace(patch.CurrencyName))
                existing.CurrencyName = patch.CurrencyName;

            if (patch.UpdatedAt != default)
                existing.UpdatedAt = DateTime.UtcNow;

            existing.UpdatedBy = Guid.Parse(userId);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var currency = await _context.Currencies.FindAsync(id);
            if (currency == null)
                return NotFound();

            _context.Currencies.Remove(currency);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
