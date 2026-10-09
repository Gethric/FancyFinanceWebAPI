using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FancyFinanceWebAPI.Data;
using FancyFinanceWebAPI.Modules.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;
using LedgerTransaction = FancyFinanceWebAPI.Modules.Transactions.Transaction;

namespace FancyFinanceWebAPI.Modules.Transactions.Import
{
    [Authorize]
    [ApiController]
    [Route("api/transactions/import")]
    public class StatementImportController : ControllerBase
    {
        private const int MaximumFileSize = 10 * 1024 * 1024;
        private const string ImportIndex = "IX_statement_imports_account_id_file_hash";
        private readonly FancyFinanceDbContext _context;
        private readonly CoOpStatementParser _parser;
        private readonly IDataProtector _protector;

        public StatementImportController(FancyFinanceDbContext context,
            CoOpStatementParser parser, IDataProtectionProvider protectionProvider)
        {
            _context = context;
            _parser = parser;
            _protector = protectionProvider.CreateProtector("FancyFinance.StatementImportPreview.v1");
        }

        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaximumFileSize)]
        public async Task<IActionResult> Preview([FromForm] StatementUploadRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            byte[] bytes;
            StatementPreview statement;
            try
            {
                bytes = await ReadPdfAsync(request.File, cancellationToken);
                statement = _parser.Parse(bytes);
            }
            catch (Exception ex) when (IsInputError(ex))
            {
                return BadRequest(InputErrorMessage(ex));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var fileHash = Convert.ToHexString(SHA256.HashData(bytes));

            // Read one consistent view of the account and its transactions.
            await using var read = await _context.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, cancellationToken);
            var state = await LoadStateAsync(request.AccountId, userId, false, cancellationToken);
            if (state == null) return NotFound("Account not found.");
            var accountError = AccountError(state.Account);
            if (accountError != null) return BadRequest(accountError);

            ValidateStatement(statement);
            var review = ReviewTrackingStart(statement, state.Account, state.Transactions);

            var possibleDuplicates = FindPossibleDuplicates(statement, state.Transactions);
            if (possibleDuplicates.Count > 0)
                statement.Warnings.Add($"{possibleDuplicates.Count} entries may already exist. Only select Skip for entries you recognise as already recorded.");

            var alreadyImported = await _context.StatementImports.AnyAsync(
                i => i.AccountId == request.AccountId && i.FileHash == fileHash, cancellationToken);
            if (alreadyImported)
                statement.Errors.Add("This PDF has already been imported into this account. No further transactions will be added from the same file.");

            var stamp = new PreviewStamp(userId, request.AccountId, fileHash,
                StateHash(state.Account, state.Transactions), DateTime.UtcNow.AddMinutes(30));
            var token = _protector.Protect(JsonSerializer.Serialize(stamp));
            await read.CommitAsync(cancellationToken);

            return Ok(new
            {
                accountId = state.Account.AccountId,
                accountName = state.Account.AccountName,
                statement,
                possibleDuplicateSequences = possibleDuplicates,
                trackingStartAdjustment = review.Adjustment,
                requiresContinuityAcknowledgement = review.RequiresAcknowledgement,
                previewToken = token
            });
        }

        [HttpPost("confirm")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaximumFileSize)]
        public async Task<IActionResult> Confirm([FromForm] ConfirmStatementImportRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            PreviewStamp? stamp;
            try
            {
                stamp = JsonSerializer.Deserialize<PreviewStamp>(
                    _protector.Unprotect(request.PreviewToken ?? ""));
            }
            catch (Exception ex) when (ex is CryptographicException ||
                ex is JsonException || ex is ArgumentException)
            {
                return BadRequest("The preview is invalid or no longer available. Preview the statement again.");
            }

            if (stamp == null || stamp.UserId != userId || stamp.AccountId != request.AccountId ||
                stamp.ExpiresAtUtc <= DateTime.UtcNow)
                return BadRequest("The preview has expired or belongs to a different account. Preview the statement again.");

            byte[] bytes;
            StatementPreview statement;
            try
            {
                bytes = await ReadPdfAsync(request.File, cancellationToken);
                if (Convert.ToHexString(SHA256.HashData(bytes)) != stamp.FileHash)
                    return BadRequest("The PDF has changed since the preview. Preview it again.");
                statement = _parser.Parse(bytes);
            }
            catch (Exception ex) when (IsInputError(ex))
            {
                return BadRequest(InputErrorMessage(ex));
            }

            ValidateStatement(statement);
            if (statement.Errors.Count > 0)
                return BadRequest(new { detail = "Resolve the statement errors before importing.", errors = new { statement = statement.Errors } });

            try
            {
                // Any early return or exception rolls back this entire transaction.
                await using var write = await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                var state = await LoadStateAsync(request.AccountId, userId, true, cancellationToken);
                if (state == null) return NotFound("Account not found.");
                var accountError = AccountError(state.Account);
                if (accountError != null) return BadRequest(accountError);

                if (await _context.StatementImports.AnyAsync(i =>
                    i.AccountId == request.AccountId && i.FileHash == stamp.FileHash, cancellationToken))
                    return Conflict("This PDF has already been imported. Refresh the ledger; nothing has been added again.");

                if (StateHash(state.Account, state.Transactions) != stamp.StateHash)
                    return Conflict("The account or its transactions changed after the preview. Preview the statement again before importing.");

                var review = ReviewTrackingStart(statement, state.Account, state.Transactions);
                if (review.Adjustment != null && !request.AcceptTrackingStartChange)
                    return BadRequest("Confirm the proposed tracking start date and opening balance change.");
                if (review.RequiresAcknowledgement && !request.AcceptContinuityWarning)
                    return BadRequest("Acknowledge the gap or balance mismatch before importing.");

                var misc = await _context.Categories.AsNoTracking()
                    .SingleOrDefaultAsync(c => c.Name == "Misc", cancellationToken);
                if (misc == null)
                    return Problem(detail: "The default Misc category is missing.", statusCode: 500);

                var now = DateTime.UtcNow;
                ImportPlan plan;
                try
                {
                    plan = BuildImportPlan(statement, state.Transactions,
                        request.SkipSequences ?? new List<int>(), request.AccountId,
                        misc.CategoryId, userId, now);
                }
                catch (InvalidDataException ex)
                {
                    return BadRequest(ex.Message);
                }

                if (review.Adjustment != null)
                {
                    state.Account.OpeningBalanceDate = review.Adjustment.ProposedDate;
                    state.Account.OpeningBalance = review.Adjustment.ProposedBalance;
                    state.Account.UpdatedAt = now;
                    state.Account.UpdatedBy = userId;
                }

                foreach (var (entry, sequence) in plan.SequenceChanges)
                {
                    entry.Sequence = sequence;
                    entry.UpdatedAt = now;
                    entry.UpdatedBy = userId;
                }

                _context.Transactions.AddRange(plan.NewTransactions);
                var receipt = new StatementImport
                {
                    AccountId = request.AccountId,
                    FileHash = stamp.FileHash,
                    StatementDate = statement.StatementDate,
                    ImportedTransactionCount = plan.NewTransactions.Count,
                    ImportedAt = now
                };
                _context.StatementImports.Add(receipt);
                await _context.SaveChangesAsync(cancellationToken);
                await write.CommitAsync(cancellationToken);

                var lastDate = statement.Transactions.Count == 0 ? statement.StatementDate :
                    statement.Transactions.Max(t => t.TransactionDate);

                return Ok(new
                {
                    accountId = request.AccountId,
                    statementImportId = receipt.StatementImportId,
                    importedCount = plan.NewTransactions.Count,
                    skippedCount = plan.SkippedCount,
                    trackingStartAdjusted = review.Adjustment != null,
                    year = lastDate.Year,
                    month = lastDate.Month
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("The account changed during import. Preview again before retrying.");
            }
            catch (DbUpdateException ex) when (IsImportConflict(ex.InnerException))
            {
                return Conflict("Another change or import occurred at the same time. Refresh and preview again before retrying.");
            }
            catch (PostgresException ex) when (IsImportConflict(ex))
            {
                return Conflict("Another change or import occurred at the same time. Refresh and preview again before retrying.");
            }
        }

        private async Task<LedgerState?> LoadStateAsync(int accountId, Guid userId,
            bool tracking, CancellationToken cancellationToken)
        {
            IQueryable<Account> accounts = _context.Accounts.Include(a => a.Currency);
            IQueryable<LedgerTransaction> transactions = _context.Transactions;
            if (!tracking)
            {
                accounts = accounts.AsNoTracking();
                transactions = transactions.AsNoTracking();
            }
            var account = await accounts.FirstOrDefaultAsync(
                a => a.AccountId == accountId && a.UserId == userId, cancellationToken);
            if (account == null) return null;
            var entries = await transactions.Where(t => t.AccountId == accountId)
                .OrderBy(t => t.TransactionId).ToListAsync(cancellationToken);
            return new LedgerState(account, entries);
        }

        private bool TryGetUserId(out Guid userId) =>
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

        private static string? AccountError(Account account)
        {
            if (!account.IsActive) return "Select an active account.";
            return account.Currency?.IsoCode.Trim() == "GBP" ? null :
                "This importer currently supports GBP accounts.";
        }

        private static async Task<byte[]> ReadPdfAsync(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0) throw new InvalidDataException("Choose a PDF statement.");
            if (file.Length > MaximumFileSize) throw new InvalidDataException("Choose a PDF no larger than 10 MB.");
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            if (bytes.Length < 5 || bytes.Length > MaximumFileSize ||
                bytes[0] != '%' || bytes[1] != 'P' || bytes[2] != 'D' || bytes[3] != 'F' || bytes[4] != '-')
                throw new InvalidDataException("The uploaded file is not a recognised PDF.");
            return bytes;
        }

        private static bool IsInputError(Exception ex) => ex is InvalidDataException ||
            ex is PdfDocumentEncryptedException || ex is PdfDocumentFormatException || ex is FormatException;

        private static string InputErrorMessage(Exception ex) => ex switch
        {
            PdfDocumentEncryptedException => "This PDF is password-protected. Upload an unlocked copy.",
            InvalidDataException => ex.Message,
            _ => "The PDF could not be read reliably. It may be damaged or use an unsupported layout."
        };

        private static bool IsImportConflict(Exception? ex) => ex is PostgresException pg &&
            (pg.SqlState == PostgresErrorCodes.SerializationFailure ||
             pg.SqlState == PostgresErrorCodes.DeadlockDetected ||
             (pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == ImportIndex));

        internal static void ValidateStatement(StatementPreview statement)
        {
            const decimal max = 9999999999999999.99m;
            bool Fits(decimal value) => value >= -max && value <= max && decimal.Round(value, 2) == value;
            if (!Fits(statement.OpeningBalance) || !Fits(statement.ClosingBalance) ||
                statement.Transactions.Any(t => !Fits(t.Amount) || t.Amount == 0 ||
                    (t.StatementBalance.HasValue && !Fits(t.StatementBalance.Value))))
                statement.Errors.Add("A statement amount is outside the supported range or precision.");
            if (statement.Transactions.Any(t => t.TransactionDate < statement.OpeningBalanceDate ||
                t.TransactionDate > statement.StatementDate || string.IsNullOrWhiteSpace(t.Description)))
                statement.Errors.Add("A transaction has an invalid date or description.");
            if (statement.Transactions.Select(t => t.Sequence).Distinct().Count() != statement.Transactions.Count)
                statement.Errors.Add("Statement row identifiers are not unique.");
        }

        internal static TrackingReview ReviewTrackingStart(StatementPreview statement,
            Account account, IReadOnlyList<LedgerTransaction> entries)
        {
            if (statement.Errors.Count > 0) return new TrackingReview(null, false);
            string? warning = null;
            TrackingStartAdjustment? adjustment = null;
            var hasEntries = entries.Count > 0;

            if (statement.OpeningBalanceDate < account.OpeningBalanceDate)
            {
                var verified = false;
                if (hasEntries)
                {
                    if (statement.StatementDate < account.OpeningBalanceDate)
                        warning = "This statement ends before the existing tracking start date. The intervening period is not covered by this upload, so continuity cannot be verified.";
                    else
                    {
                        var atExistingStart = statement.OpeningBalance + statement.Transactions
                            .Where(t => t.TransactionDate < account.OpeningBalanceDate).Sum(t => t.Amount);
                        verified = atExistingStart == account.OpeningBalance;
                        if (!verified)
                            warning = "The statement balance at the existing tracking start differs from the current opening balance. Moving the start will change existing running balances.";
                    }
                }
                adjustment = new TrackingStartAdjustment(account.OpeningBalanceDate,
                    account.OpeningBalance, statement.OpeningBalanceDate, statement.OpeningBalance,
                    hasEntries, verified, warning != null);
            }
            else
            {
                var expected = account.OpeningBalance + entries
                    .Where(t => t.TransactionDate < statement.OpeningBalanceDate).Sum(t => t.Amount);
                if (expected != statement.OpeningBalance)
                    warning = "The statement opening balance differs from the calculated account balance. Check missing earlier transactions or an incorrect opening balance. Importing will not replace the existing opening balance.";
            }
            if (warning != null) statement.Warnings.Add(warning);
            return new TrackingReview(adjustment, warning != null);
        }

        private static string NormaliseDescription(string value) => string.Join(" ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

        private static (DateOnly Date, string Description, decimal Amount) Key(
            DateOnly date, string description, decimal amount) => (date, NormaliseDescription(description), amount);

        internal static List<int> FindPossibleDuplicates(StatementPreview statement,
            IReadOnlyList<LedgerTransaction> entries)
        {
            var keys = entries.Select(t => Key(t.TransactionDate, t.Description, t.Amount)).ToHashSet();
            return statement.Transactions.Where(t => keys.Contains(Key(t.TransactionDate, t.Description, t.Amount)))
                .Select(t => t.Sequence).ToList();
        }

        // Keeps both the statement order and the relative order of existing entries.
        // Matched duplicates act as anchors. New entries are inserted around them.
        internal static ImportPlan BuildImportPlan(StatementPreview statement,
            IReadOnlyList<LedgerTransaction> entries, IReadOnlyList<int> skips,
            int accountId, int categoryId, Guid userId, DateTime now)
        {
            var skipSet = skips.ToHashSet();
            var rows = statement.Transactions.OrderBy(t => t.Sequence).ToList();
            var rowIds = rows.Select(t => t.Sequence).ToHashSet();
            if (skipSet.Count != skips.Count || skipSet.Any(id => !rowIds.Contains(id)))
                throw new InvalidDataException("The selected rows are invalid. Preview the statement again.");

            var candidates = entries.OrderBy(t => t.Sequence).ThenBy(t => t.TransactionId)
                .GroupBy(t => Key(t.TransactionDate, t.Description, t.Amount))
                .ToDictionary(g => g.Key, g => new Queue<LedgerTransaction>(g));
            var matches = new Dictionary<int, LedgerTransaction>();
            foreach (var row in rows.Where(t => skipSet.Contains(t.Sequence)))
            {
                if (!candidates.TryGetValue(Key(row.TransactionDate, row.Description, row.Amount), out var queue) || queue.Count == 0)
                    throw new InvalidDataException("An entry selected for skipping has no unused matching transaction. Each skipped entry must match a separate existing transaction.");
                matches[row.Sequence] = queue.Dequeue();
            }

            var added = new List<LedgerTransaction>();
            var sequenceChanges = new List<(LedgerTransaction Entry, int Sequence)>();
            foreach (var day in rows.GroupBy(t => t.TransactionDate).OrderBy(g => g.Key))
            {
                var existing = entries.Where(t => t.TransactionDate == day.Key)
                    .OrderBy(t => t.Sequence).ThenBy(t => t.TransactionId).ToList();
                var indexById = existing.Select((t, index) => (t.TransactionId, index))
                    .ToDictionary(pair => pair.TransactionId, pair => pair.index);
                var merged = new List<LedgerTransaction>();
                var cursor = 0;
                var lastAnchor = -1;

                foreach (var row in day)
                {
                    if (matches.TryGetValue(row.Sequence, out var match))
                    {
                        var anchor = indexById[match.TransactionId];
                        if (anchor <= lastAnchor)
                            throw new InvalidDataException("Matching existing transactions are in a different order from the statement. Review those entries before importing.");
                        while (cursor <= anchor) merged.Add(existing[cursor++]);
                        lastAnchor = anchor;
                    }
                    else
                    {
                        var transaction = new LedgerTransaction
                        {
                            AccountId = accountId,
                            CategoryId = categoryId,
                            TransactionDate = row.TransactionDate,
                            TransactionTime = row.TransactionTime,
                            Description = row.Description.Trim(),
                            Amount = row.Amount,
                            StatementBalance = row.StatementBalance,
                            CreatedAt = now,
                            CreatedBy = userId,
                            UpdatedAt = now,
                            UpdatedBy = userId
                        };
                        merged.Add(transaction);
                        added.Add(transaction);
                    }
                }
                while (cursor < existing.Count) merged.Add(existing[cursor++]);

                for (var index = 0; index < merged.Count; index++)
                {
                    var entry = merged[index];
                    var sequence = index + 1;
                    if (entry.TransactionId == 0) entry.Sequence = sequence;
                    else if (entry.Sequence != sequence) sequenceChanges.Add((entry, sequence));
                }
            }
            return new ImportPlan(added, sequenceChanges, skipSet.Count);
        }

        internal static string StateHash(Account account, IReadOnlyList<LedgerTransaction> entries)
        {
            var snapshot = new
            {
                account.AccountId,
                account.UserId,
                account.CurrencyId,
                account.IsActive,
                account.OpeningBalanceDate,
                account.OpeningBalance,
                Transactions = entries.OrderBy(t => t.TransactionId).Select(t => new
                {
                    t.TransactionId,
                    t.AccountId,
                    t.TransactionDate,
                    t.TransactionTime,
                    t.Description,
                    t.Amount,
                    t.CategoryId,
                    t.Sequence,
                    t.StatementBalance,
                    t.UpdatedAt
                }).ToArray()
            };
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot))));
        }

        private sealed record LedgerState(Account Account, List<LedgerTransaction> Transactions);
        private sealed record PreviewStamp(Guid UserId, int AccountId, string FileHash,
            string StateHash, DateTime ExpiresAtUtc);
        internal sealed record TrackingReview(TrackingStartAdjustment? Adjustment, bool RequiresAcknowledgement);
        internal sealed record ImportPlan(List<LedgerTransaction> NewTransactions,
            List<(LedgerTransaction Entry, int Sequence)> SequenceChanges, int SkippedCount);
    }

    public record TrackingStartAdjustment(DateOnly PreviousDate, decimal PreviousBalance,
        DateOnly ProposedDate, decimal ProposedBalance, bool HasExistingTransactions,
        bool ContinuityVerified, bool RequiresAcknowledgement);

    public class StatementUploadRequest
    {
        [Range(1, int.MaxValue)]
        public int AccountId { get; set; }

        [Required]
        public IFormFile? File { get; set; }
    }
}
