using System.Security.Claims;
using FancyFinanceWebAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FancyFinanceWebAPI.Modules.Accounts
{
    [Authorize]
    [ApiController]
    [Route("api/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly FancyFinanceDbContext _context;

        public AccountsController(FancyFinanceDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountResponse>>> GetAccounts()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.AccountName)
                .ThenBy(a => a.AccountId)
                .ToListAsync();

            return Ok(accounts.Select(ToResponse));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AccountResponse>> GetAccount(int id)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.AccountId == id && a.UserId == userId);

            if (account == null)
                return NotFound();

            return Ok(ToResponse(account));
        }

        [HttpPost]
        public async Task<ActionResult<AccountResponse>> CreateAccount(
            [FromBody] CreateAccountRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.AccountName))
            {
                ModelState.AddModelError(
                    nameof(request.AccountName),
                    "Enter an account name.");
            }

            if (request.OpeningBalanceDate == null)
            {
                ModelState.AddModelError(
                    nameof(request.OpeningBalanceDate),
                    "Enter an opening balance date.");
            }

            const decimal maximumBalance = 9999999999999999.99m;

            if (request.OpeningBalance < -maximumBalance ||
                request.OpeningBalance > maximumBalance)
            {
                ModelState.AddModelError(
                    nameof(request.OpeningBalance),
                    "The opening balance is outside the supported range.");
            }

            if (decimal.Round(request.OpeningBalance, 2) !=
                request.OpeningBalance)
            {
                ModelState.AddModelError(
                    nameof(request.OpeningBalance),
                    "Use no more than two decimal places.");
            }

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var currency = await _context.Currencies
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.IsoCode == "GBP");

            if (currency == null)
            {
                return Problem(
                    detail: "GBP is missing from the currencies table.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var now = DateTime.UtcNow;

            var account = new Account
            {
                UserId = userId,
                AccountName = request.AccountName.Trim(),
                AccountType = request.AccountType,
                CurrencyId = currency.CurrencyId,
                OpeningBalance = request.OpeningBalance,
                OpeningBalanceDate = request.OpeningBalanceDate!.Value,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId,
                UpdatedAt = now,
                UpdatedBy = userId
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetAccount),
                new { id = account.AccountId },
                ToResponse(account));
        }

        private bool TryGetUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out userId);
        }

        private static AccountResponse ToResponse(Account account)
        {
            return new AccountResponse(
                account.AccountId,
                account.AccountName,
                account.AccountType,
                account.CurrencyId,
                account.OpeningBalance,
                account.OpeningBalanceDate,
                account.IsActive);
        }
    }

    public record AccountResponse(
        int AccountId,
        string AccountName,
        string AccountType,
        int CurrencyId,
        decimal OpeningBalance,
        DateOnly OpeningBalanceDate,
        bool IsActive);
}