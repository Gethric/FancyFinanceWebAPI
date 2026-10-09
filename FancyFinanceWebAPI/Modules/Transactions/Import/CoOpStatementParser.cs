using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FancyFinanceWebAPI.Modules.Transactions.Import
{
    public sealed class CoOpStatementParser
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-GB");
        private const string MoneyPattern = @"-?(?:\d{1,3}(?:,\d{3})+|\d+)\.\d{2}(?:\s*(?:OD|DR|CR))?";

        // These column boundaries identify the supplied Co-op statement layout.
        // Coordinates are normalised to its 596-point page width.
        private static readonly double[] Boundaries = { 170, 230, 355, 420, 490, 580 };

        public StatementPreview Parse(byte[] pdfBytes)
        {
            if (pdfBytes == null || pdfBytes.Length == 0)
                throw new InvalidDataException("Choose a PDF statement.");

            using var document = PdfDocument.Open(pdfBytes);
            if (document.NumberOfPages > 40)
                throw new InvalidDataException("Use a statement with no more than 40 PDF pages.");

            var pages = new List<TablePage>();

            foreach (var page in document.GetPages())
            {
                var scale = 596d / page.Width;
                var words = page.GetWords().ToList();
                var rows = MakeRows(words.Where(w =>
                    w.BoundingBox.Left * scale >= Boundaries[0] &&
                    w.BoundingBox.Left * scale < Boundaries[^1]));

                var header = rows.FindIndex(row => IsHeader(row, scale));

                if (header >= 0)
                {
                    var sidebar = string.Join(" ", MakeRows(words.Where(w =>
                        w.BoundingBox.Left * scale < Boundaries[0]))
                        .Select(row => Text(row.Words)));

                    pages.Add(new TablePage(page.Number, scale, sidebar,
                        rows.Skip(header + 1).ToList()));
                }
            }

            if (pages.Count == 0)
                throw new InvalidDataException(
                    "No supported Co-op transaction table was found. Scanned PDFs are not supported yet.");

            var first = pages[0];
            var result = new StatementPreview
            {
                StatementDate = ReadStatementDate(first.Sidebar),
                OpeningBalance = ReadSummaryMoney(first.Sidebar, @"Opening\s*balance"),
                ClosingBalance = ReadSummaryMoney(first.Sidebar, @"Statement\s*closing\s*balance"),
                StatementMoneyIn = ReadSummaryMoney(first.Sidebar, @"Money\s*in"),
                StatementMoneyOut = ReadSummaryMoney(first.Sidebar, @"Money\s*out")
            };

            // Co-op counts statement pages separately from information pages.
            var pageCount = Regex.Match(first.Sidebar,
                @"Page\s*number\s*1\s*of\s*(?<total>\d+)", RegexOptions.IgnoreCase);
            if (!pageCount.Success ||
                int.Parse(pageCount.Groups["total"].Value, CultureInfo.InvariantCulture) != pages.Count)
            {
                result.Errors.Add("The statement page count could not be verified. A transaction page may be missing or use a different layout.");
            }

            var runningBalance = result.OpeningBalance;
            DateOnly? previousDate = null;
            var openingFound = false;
            var closingFound = false;

            foreach (var page in pages)
            {
                if (closingFound)
                {
                    result.Errors.Add("Another transaction table appears after the closing balance. Import one statement at a time.");
                    break;
                }

                var pageEnded = false;

                for (var rowIndex = 0; rowIndex < page.Rows.Count; rowIndex++)
                {
                    var row = page.Rows[rowIndex];
                    var columns = Columns(row, page.Scale);
                    var text = Compact(Text(row.Words));
                    var location = $"PDF page {page.Number}, table row {rowIndex + 1}";

                    try
                    {
                        if (text.StartsWith("STATEMENTCLOSINGBALANCE", StringComparison.Ordinal))
                        {
                            var balance = ReadMoney(columns[4]);
                            CheckBalance(result, balance, runningBalance, location);
                            if (balance != result.ClosingBalance)
                                result.Errors.Add($"{location}: closing balance differs from the summary.");
                            closingFound = true;
                            pageEnded = true;
                            break;
                        }

                        var descriptionKey = Compact(columns[1]);

                        if (descriptionKey is "BROUGHTFORWARD" or "CARRIEDFORWARD")
                        {
                            if (columns[2].Length > 0 || columns[3].Length > 0)
                                throw new FormatException("A balance-forward row unexpectedly contains money in or out.");

                            var balance = ReadMoney(columns[4]);
                            CheckBalance(result, balance, runningBalance, location);

                            if (!openingFound)
                            {
                                if (descriptionKey != "BROUGHTFORWARD")
                                    throw new FormatException("The opening balance row is missing.");

                                result.OpeningBalanceDate = ReadRowDate(columns[0], result.StatementDate);
                                previousDate = result.OpeningBalanceDate;
                                openingFound = true;
                            }

                            if (descriptionKey == "CARRIEDFORWARD")
                            {
                                pageEnded = true;
                                break;
                            }

                            continue;
                        }

                        if (!openingFound)
                            throw new FormatException("A transaction appears before the opening balance row.");

                        var hasOut = columns[2].Length > 0;
                        var hasIn = columns[3].Length > 0;

                        // A description-only line may continue the preceding entry.
                        if (columns[0].Length == 0 && !hasOut && !hasIn && columns[4].Length == 0)
                        {
                            if (columns[1].Length == 0)
                                continue;

                            if (result.Transactions.Count == 0 ||
                                result.Transactions[^1].SourcePage != page.Number)
                                throw new FormatException("An unattached description line was found.");

                            result.Transactions[^1].Description += " " + columns[1];
                            continue;
                        }

                        if (hasOut == hasIn)
                            throw new FormatException("Expected exactly one money-in or money-out amount.");

                        var date = columns[0].Length == 0
                            ? previousDate!.Value
                            : ReadRowDate(columns[0], result.StatementDate);

                        if (date < previousDate!.Value)
                            throw new FormatException("Dates are out of order or the year could not be determined safely.");

                        if (string.IsNullOrWhiteSpace(columns[1]))
                            throw new FormatException("The transaction description is missing.");

                        var magnitude = ReadMoney(hasOut ? columns[2] : columns[3]);
                        if (magnitude <= 0)
                            throw new FormatException("Expected a positive amount in the money-in/out column.");

                        var amount = hasOut ? -magnitude : magnitude;
                        decimal? statementBalance = columns[4].Length == 0
                            ? null : ReadMoney(columns[4]);

                        runningBalance += amount;
                        if (statementBalance.HasValue)
                            CheckBalance(result, statementBalance.Value, runningBalance, location);

                        result.Transactions.Add(new StatementTransaction
                        {
                            TransactionDate = date,
                            TransactionTime = null,
                            Description = columns[1],
                            Amount = amount,
                            StatementBalance = statementBalance,
                            CalculatedBalance = runningBalance,
                            Sequence = result.Transactions.Count + 1,
                            SourcePage = page.Number
                        });

                        previousDate = date;
                    }
                    catch (FormatException ex)
                    {
                        result.Errors.Add($"{location}: {ex.Message}");
                        // Do not guess at subsequent rows after losing the table structure.
                        break;
                    }
                }

                if (!pageEnded)
                    result.Errors.Add($"PDF page {page.Number}: the table's closing or carried-forward balance was not recognised.");
            }

            if (!openingFound)
                result.Errors.Add("The brought-forward opening balance was not found.");
            if (!closingFound)
                result.Errors.Add("The final statement closing balance was not found.");

            var moneyIn = result.Transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
            var moneyOut = -result.Transactions.Where(t => t.Amount < 0).Sum(t => t.Amount);

            if (moneyIn != result.StatementMoneyIn)
                result.Errors.Add("Extracted money in does not match the statement summary.");
            if (moneyOut != result.StatementMoneyOut)
                result.Errors.Add("Extracted money out does not match the statement summary.");
            if (result.OpeningBalance + moneyIn - moneyOut != result.ClosingBalance)
                result.Errors.Add("The extracted transactions do not reconcile to the closing balance.");

            result.Warnings.Add("Transaction times are not supplied by this statement and have been left blank.");
            return result;
        }

        private static List<Row> MakeRows(IEnumerable<Word> words)
        {
            var rows = new List<Row>();
            foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
            {
                var bottom = word.BoundingBox.Bottom;
                if (rows.Count == 0 || Math.Abs(rows[^1].Bottom - bottom) > 2.5)
                    rows.Add(new Row(bottom, new List<Word>()));

                rows[^1].Words.Add(word);
            }
            return rows;
        }

        private static string Text(IEnumerable<Word> words) =>
            string.Join(" ", words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));

        private static string Compact(string value) =>
            Regex.Replace(value, @"\s+", "").ToUpperInvariant();

        private static string[] Columns(Row row, double scale) =>
            Enumerable.Range(0, 5).Select(column => Text(row.Words.Where(word =>
                word.BoundingBox.Left * scale >= Boundaries[column] &&
                word.BoundingBox.Left * scale < Boundaries[column + 1]))).ToArray();

        private static bool IsHeader(Row row, double scale)
        {
            var cells = Columns(row, scale).Select(Compact).ToArray();
            return cells.SequenceEqual(new[] { "DATE", "DESCRIPTION", "MONEYOUT", "MONEYIN", "BALANCE" });
        }

        private static decimal ReadSummaryMoney(string sidebar, string label)
        {
            var match = Regex.Match(sidebar, label + @"\s*(?<money>" + MoneyPattern + ")",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new InvalidDataException("A required balance or total could not be read from the statement summary.");
            return ReadMoney(match.Groups["money"].Value);
        }

        private static decimal ReadMoney(string text)
        {
            var compact = Compact(text).TrimStart('£');
            if (!Regex.IsMatch(compact, "^(?:" + MoneyPattern + ")$", RegexOptions.IgnoreCase))
                throw new FormatException("An amount or balance could not be read reliably.");

            var isDebit = compact.EndsWith("OD", StringComparison.Ordinal) ||
                compact.EndsWith("DR", StringComparison.Ordinal);
            compact = Regex.Replace(compact, @"(?:OD|DR|CR)$", "");

            var value = decimal.Parse(compact, NumberStyles.AllowLeadingSign |
                NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, Culture);
            return isDebit ? -Math.Abs(value) : value;
        }

        private static DateOnly ReadStatementDate(string sidebar)
        {
            var match = Regex.Match(sidebar,
                @"Statement\s*date\s*(?<day>\d{1,2})\s*(?<month>[A-Za-z]+)\s*(?<year>\d{4}|\d{2})(?!\d)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new InvalidDataException("The statement date could not be read.");

            var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            if (year < 100) year += 2000;

            var value = $"{match.Groups["day"].Value} {match.Groups["month"].Value} {year}";
            if (!DateOnly.TryParseExact(value, new[] { "d MMM yyyy", "d MMMM yyyy" },
                Culture, DateTimeStyles.None, out var date))
                throw new InvalidDataException("The statement date is invalid.");
            return date;
        }

        private static DateOnly ReadRowDate(string text, DateOnly statementDate)
        {
            for (var year = statementDate.Year; year >= Math.Max(1, statementDate.Year - 1); year--)
            {
                if (DateOnly.TryParseExact(text + " " + year,
                    new[] { "d MMM yyyy", "dd MMM yyyy", "d MMMM yyyy", "dd MMMM yyyy" },
                    Culture, DateTimeStyles.None, out var date) && date <= statementDate)
                    return date;
            }
            throw new FormatException("The transaction date could not be read.");
        }

        private static void CheckBalance(StatementPreview result,
            decimal printed, decimal calculated, string location)
        {
            if (printed != calculated)
                result.Errors.Add($"{location}: the printed balance does not match the calculated balance.");
        }

        private sealed record Row(double Bottom, List<Word> Words);
        private sealed record TablePage(int Number, double Scale, string Sidebar, List<Row> Rows);
    }
}
