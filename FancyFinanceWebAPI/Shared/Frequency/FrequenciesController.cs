using FancyFinanceWebAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FancyFinanceWebAPI.Shared.Frequency
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FrequenciesController : ControllerBase
    {
        private readonly FancyFinanceDbContext _context;

        public FrequenciesController(FancyFinanceDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Frequency>>> GetFrequencies()
        {
            return await _context.Frequencies
                .OrderBy(c => c.FrequencyName)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Frequency>> GetSelectedFrequency([FromRoute] int id)
        {
            var frequency = await _context.Frequencies.FindAsync(id);

            if (frequency == null)
            {
                return NotFound();
            }

            return Ok(frequency);
        }

        [HttpPost]
        public async Task<ActionResult<Frequency>> CreateFrequency([FromBody] Frequency frequency)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            frequency.CreatedBy = Guid.Parse(userId);
            _context.Frequencies.Add(frequency);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSelectedFrequency), new { id = frequency.FrequencyId }, frequency);
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<Frequency>> UpdateFrequency(int id, [FromBody] Frequency patch)
        {
            var existing = await _context.Frequencies.FindAsync(id);
            if (existing == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            // ID check just to be safe (optional)
            if (patch.FrequencyId != 0 && patch.FrequencyId != id)
                return BadRequest("FrequencyId cannot be changed");

            // Only update allowed fields
            if (!string.IsNullOrWhiteSpace(patch.FrequencyName))
                existing.FrequencyName = patch.FrequencyName;

            if (patch.UpdatedAt != default)
                existing.UpdatedAt = DateTime.UtcNow;

            existing.UpdatedBy = Guid.Parse(userId);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var frequency = await _context.Frequencies.FindAsync(id);
            if (frequency == null)
                return NotFound();

            _context.Frequencies.Remove(frequency);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
