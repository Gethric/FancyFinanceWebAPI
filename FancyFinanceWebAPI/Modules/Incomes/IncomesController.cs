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
        private readonly IIncomeService _service;

        public IncomesController(IIncomeService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<ActionResult<Income>> CreateIncome([FromBody] CreateIncomeDTO dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userIdStr == null) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var income = await _service.CreateIncome(dto, userId);

            return CreatedAtAction(nameof(GetIncomeById), new { id = income.IncomeId }, income);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Income>>> GetUserIncomes()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var incomes = await _service.GetUserIncomes(userId.Value);
            return Ok(incomes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Income>> GetIncomeById(int id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var income = await _service.GetIncomeById(id, userId.Value);
            if (income == null) return NotFound();

            return Ok(income);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateIncome(int id, [FromBody] UpdateIncomeDTO dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var success = await _service.UpdateIncome(id, dto, userId.Value);
            if (!success) return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteIncome(int id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var success = await _service.DeleteIncome(id, userId.Value);
            if (!success) return NotFound();

            return NoContent();
        }

        private Guid? GetUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdStr, out var guid) ? guid : null;
        }
    }
}
