using FancyFinanceWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FancyFinanceWebAPI.Shared.Category
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly FancyFinanceDbContext _context;

        public CategoriesController(FancyFinanceDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            return await _context.Categories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetSelectedCategory([FromRoute] int id)
        {
            var category = await _context.Categories.FindAsync(id);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category);      
        }

        [HttpPost]
        public async Task<ActionResult<Category>> CreateCategory([FromBody] Category category)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            category.CreatedBy = Guid.Parse(userId);
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSelectedCategory), new { id = category.CategoryId }, category );
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<Category>> UpdateCategory(int id, [FromBody] Category patch)
        {
            var existing = await _context.Categories.FindAsync(id);
            if (existing == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            // ID check just to be safe (optional)
            if (patch.CategoryId != 0 && patch.CategoryId != id)
                return BadRequest("CategoryId cannot be changed");

            // Only update allowed fields
            if (!string.IsNullOrWhiteSpace(patch.CategoryName))
                existing.CategoryName = patch.CategoryName;

            if (patch.UpdatedAt != default)
                existing.UpdatedAt = DateTime.UtcNow;

            existing.UpdatedBy = Guid.Parse(userId);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound();

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
