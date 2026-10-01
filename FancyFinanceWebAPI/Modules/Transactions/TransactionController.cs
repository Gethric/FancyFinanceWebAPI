using System.Security.Claims;
using FancyFinanceWebAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FancyFinanceWebAPI.Modules.Transactions
{
    [Authorize]
    [ApiController]
    [Route("api/transactions")]
    public class TransactionsController : ControllerBase
    {
        private readonly FancyFinanceDbContext _context;

        public TransactionsController(FancyFinanceDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TransactionResponse>>>
            GetTransactions(
                [FromQuery] int year,
                [FromQuery] int month,
                [FromQuery] int? accountId = null)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            if (year < 1 || year > 9999 || month < 1 || month > 12)
                return BadRequest("Enter a valid year and month.");

            if (accountId.HasValue && accountId.Value < 1)
                return BadRequest("Enter a valid account ID.");

            var accountsQuery = _context.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId);

            if (accountId.HasValue)
            {
                accountsQuery = accountsQuery.Where(
                    a => a.AccountId == accountId.Value);
            }

            var accounts = await accountsQuery.ToListAsync();

            if (accountId.HasValue && accounts.Count == 0)
                return NotFound("Account not found.");

            if (accounts.Count == 0)
                return Ok(Array.Empty<TransactionResponse>());

            var start = new DateOnly(year, month, 1);
            var end = new DateOnly(
                year, month, DateTime.DaysInMonth(year, month));

            var accountIds = accounts
                .Select(a => a.AccountId)
                .ToArray();

            var accountsById = accounts.ToDictionary(a => a.AccountId);

            var balances = accounts.ToDictionary(
                a => a.AccountId,
                a => a.OpeningBalance);

            // Include earlier months when calculating running balances.
            var previousTotals = await _context.Transactions
                .AsNoTracking()
                .Where(t =>
                    accountIds.Contains(t.AccountId) &&
                    t.TransactionDate < start)
                .GroupBy(t => t.AccountId)
                .Select(group => new
                {
                    AccountId = group.Key,
                    Total = group.Sum(t => t.Amount)
                })
                .ToListAsync();

            foreach (var previous in previousTotals)
            {
                balances[previous.AccountId] += previous.Total;
            }

            var transactions = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t =>
                    accountIds.Contains(t.AccountId) &&
                    t.TransactionDate >= start &&
                    t.TransactionDate <= end)
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.AccountId)
                .ThenBy(t => t.Sequence)
                .ThenBy(t => t.TransactionId)
                .ToListAsync();

            var results = new List<TransactionResponse>();

            foreach (var transaction in transactions)
            {
                balances[transaction.AccountId] += transaction.Amount;

                results.Add(ToResponse(
                    transaction,
                    accountsById[transaction.AccountId].AccountName,
                    transaction.Category!.Name,
                    balances[transaction.AccountId]));
            }

            return Ok(results);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransactionResponse>>
            GetTransaction(int id)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var transaction = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t =>
                    t.TransactionId == id &&
                    t.Account!.UserId == userId);

            if (transaction == null)
                return NotFound();

            var balance = await GetBalanceAsync(
                transaction,
                transaction.Account!.OpeningBalance);

            return Ok(ToResponse(
                transaction,
                transaction.Account.AccountName,
                transaction.Category!.Name,
                balance));
        }

        [HttpPost]
        public async Task<ActionResult<TransactionResponse>>
            CreateTransaction([FromBody] CreateTransactionRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                ModelState.AddModelError(
                    nameof(request.Description),
                    "Enter a description.");
            }

            if (!request.TransactionDate.HasValue)
            {
                ModelState.AddModelError(
                    nameof(request.TransactionDate),
                    "Enter a transaction date.");
            }

            if (!request.Amount.HasValue || request.Amount.Value == 0)
            {
                ModelState.AddModelError(
                    nameof(request.Amount),
                    "Enter a non-zero amount.");
            }

            if (request.Amount.HasValue)
            {
                const decimal maximumAmount = 9999999999999999.99m;
                var value = request.Amount.Value;

                if (value < -maximumAmount || value > maximumAmount)
                {
                    ModelState.AddModelError(
                        nameof(request.Amount),
                        "The amount is outside the supported range.");
                }

                if (decimal.Round(value, 2) != value)
                {
                    ModelState.AddModelError(
                        nameof(request.Amount),
                        "Use no more than two decimal places.");
                }
            }

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.AccountId == request.AccountId &&
                    a.UserId == userId);

            if (account == null)
                return NotFound("Account not found.");

            if (!account.IsActive)
                return BadRequest("This account is inactive.");

            var date = request.TransactionDate!.Value;

            if (date < account.OpeningBalanceDate)
            {
                return BadRequest(
                    "The transaction date cannot be before " +
                    "the account's opening balance date.");
            }

            var categories = _context.Categories.AsNoTracking();

            var category = request.CategoryId.HasValue
                ? await categories.SingleOrDefaultAsync(
                    c => c.CategoryId == request.CategoryId.Value)
                : await categories.SingleOrDefaultAsync(
                    c => c.Name == "Misc");

            if (category == null)
            {
                if (request.CategoryId.HasValue)
                    return BadRequest("Category not found.");

                return Problem(
                    detail: "The default Misc category is missing.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            // Append manual entries after existing entries on this date.
            var lastSequence = await _context.Transactions
                .Where(t =>
                    t.AccountId == account.AccountId &&
                    t.TransactionDate == date)
                .MaxAsync(t => (int?)t.Sequence) ?? 0;

            if (lastSequence == int.MaxValue)
                return Conflict("No more entries can be added on this date.");

            var now = DateTime.UtcNow;

            var transaction = new Transaction
            {
                AccountId = account.AccountId,
                CategoryId = category.CategoryId,
                TransactionDate = date,
                TransactionTime = request.TransactionTime,
                Description = request.Description.Trim(),
                Amount = request.Amount!.Value,
                StatementBalance = null,
                Sequence = lastSequence + 1,
                CreatedAt = now,
                CreatedBy = userId,
                UpdatedAt = now,
                UpdatedBy = userId
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            var balance = await GetBalanceAsync(
                transaction,
                account.OpeningBalance);

            return CreatedAtAction(
                nameof(GetTransaction),
                new { id = transaction.TransactionId },
                ToResponse(
                    transaction,
                    account.AccountName,
                    category.Name,
                    balance));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<TransactionResponse>> UpdateTransaction(
    int id,
    [FromBody] UpdateTransactionRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t =>
                    t.TransactionId == id &&
                    t.Account!.UserId == userId);

            if (transaction == null)
                return NotFound("Transaction not found.");

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                ModelState.AddModelError(
                    nameof(request.Description),
                    "Enter a description.");
            }

            if (!request.TransactionDate.HasValue)
            {
                ModelState.AddModelError(
                    nameof(request.TransactionDate),
                    "Enter a transaction date.");
            }

            if (!request.Amount.HasValue || request.Amount.Value == 0)
            {
                ModelState.AddModelError(
                    nameof(request.Amount),
                    "Enter a non-zero amount.");
            }

            if (request.Amount.HasValue)
            {
                const decimal maximumAmount = 9999999999999999.99m;
                var value = request.Amount.Value;

                if (value < -maximumAmount || value > maximumAmount)
                {
                    ModelState.AddModelError(
                        nameof(request.Amount),
                        "The amount is outside the supported range.");
                }

                if (decimal.Round(value, 2) != value)
                {
                    ModelState.AddModelError(
                        nameof(request.Amount),
                        "Use no more than two decimal places.");
                }
            }

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.AccountId == request.AccountId &&
                    a.UserId == userId);

            if (account == null)
                return NotFound("Account not found.");

            // Existing entries in inactive accounts can still be corrected,
            // but entries cannot be moved into an inactive account.
            if (!account.IsActive &&
                account.AccountId != transaction.AccountId)
            {
                return BadRequest(
                    "The transaction cannot be moved into an inactive account.");
            }

            var date = request.TransactionDate!.Value;

            if (date < account.OpeningBalanceDate)
            {
                return BadRequest(
                    "The transaction date cannot be before " +
                    "the account's opening balance date.");
            }

            var categories = _context.Categories.AsNoTracking();

            var category = request.CategoryId.HasValue
                ? await categories.SingleOrDefaultAsync(
                    c => c.CategoryId == request.CategoryId.Value)
                : await categories.SingleOrDefaultAsync(
                    c => c.Name == "Misc");

            if (category == null)
            {
                if (request.CategoryId.HasValue)
                    return BadRequest("Category not found.");

                return Problem(
                    detail: "The default Misc category is missing.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var positionChanged =
                transaction.AccountId != account.AccountId ||
                transaction.TransactionDate != date;

            if (positionChanged)
            {
                var lastSequence = await _context.Transactions
                    .Where(t =>
                        t.AccountId == account.AccountId &&
                        t.TransactionDate == date &&
                        t.TransactionId != id)
                    .MaxAsync(t => (int?)t.Sequence) ?? 0;

                if (lastSequence == int.MaxValue)
                    return Conflict("No more entries can be added on this date.");

                transaction.Sequence = lastSequence + 1;

                // A statement balance belongs to its original account/date.
                transaction.StatementBalance = null;
            }

            transaction.AccountId = account.AccountId;
            transaction.CategoryId = category.CategoryId;
            transaction.TransactionDate = date;
            transaction.TransactionTime = request.TransactionTime;
            transaction.Description = request.Description.Trim();
            transaction.Amount = request.Amount!.Value;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.UpdatedBy = userId;

            await _context.SaveChangesAsync();

            var balance = await GetBalanceAsync(
                transaction,
                account.OpeningBalance);

            return Ok(ToResponse(
                transaction,
                account.AccountName,
                category.Name,
                balance));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t =>
                    t.TransactionId == id &&
                    t.Account!.UserId == userId);

            if (transaction == null)
                return NotFound("Transaction not found.");

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("months")]
        public async Task<IActionResult> GetTransactionMonths(
    [FromQuery] int? accountId = null)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            if (accountId.HasValue)
            {
                if (accountId.Value < 1)
                    return BadRequest("Enter a valid account ID.");

                var ownsAccount = await _context.Accounts
                    .AnyAsync(a =>
                        a.AccountId == accountId.Value &&
                        a.UserId == userId);

                if (!ownsAccount)
                    return NotFound("Account not found.");
            }

            var query = _context.Transactions
                .AsNoTracking()
                .Where(t => t.Account!.UserId == userId);

            if (accountId.HasValue)
            {
                query = query.Where(
                    t => t.AccountId == accountId.Value);
            }

            var months = await query
                .Select(t => new
                {
                    Year = t.TransactionDate.Year,
                    Month = t.TransactionDate.Month
                })
                .Distinct()
                .OrderByDescending(m => m.Year)
                .ThenByDescending(m => m.Month)
                .ToListAsync();

            return Ok(months);
        }

        private async Task<decimal> GetBalanceAsync(
            Transaction transaction,
            decimal openingBalance)
        {
            // Transaction ID breaks ties if entries share a sequence.
            var total = await _context.Transactions
                .Where(t =>
                    t.AccountId == transaction.AccountId &&
                    (
                        t.TransactionDate < transaction.TransactionDate ||
                        (
                            t.TransactionDate == transaction.TransactionDate &&
                            (
                                t.Sequence < transaction.Sequence ||
                                (
                                    t.Sequence == transaction.Sequence &&
                                    t.TransactionId <= transaction.TransactionId
                                )
                            )
                        )
                    ))
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return openingBalance + total;
        }

        private bool TryGetUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out userId);
        }

        private static TransactionResponse ToResponse(
            Transaction transaction,
            string accountName,
            string categoryName,
            decimal balance)
        {
            return new TransactionResponse(
                transaction.TransactionId,
                transaction.AccountId,
                accountName,
                transaction.TransactionDate,
                transaction.TransactionTime,
                transaction.Description,
                transaction.CategoryId,
                categoryName,
                transaction.Amount,
                balance,
                transaction.StatementBalance,
                transaction.Sequence);
        }
    }

    public record TransactionResponse(
        int TransactionId,
        int AccountId,
        string AccountName,
        DateOnly TransactionDate,
        TimeOnly? TransactionTime,
        string Description,
        int CategoryId,
        string CategoryName,
        decimal Amount,
        decimal Balance,
        decimal? StatementBalance,
        int Sequence);
}