namespace FancyFinanceWebAPI.Modules.Transactions.Import
{
    public class StatementPreview
    {
        public string Bank { get; set; } = "Co-op";

        public DateOnly StatementDate { get; set; }

        public DateOnly OpeningBalanceDate { get; set; }

        public decimal OpeningBalance { get; set; }

        public decimal ClosingBalance { get; set; }

        public decimal StatementMoneyIn { get; set; }

        public decimal StatementMoneyOut { get; set; }

        public List<StatementTransaction> Transactions { get; set; } = new();

        public List<string> Warnings { get; set; } = new();

        public List<string> Errors { get; set; } = new();
    }

    public class StatementTransaction
    {
        public DateOnly TransactionDate { get; set; }

        public TimeOnly? TransactionTime { get; set; }

        public string Description { get; set; } = string.Empty;

        // Positive for money in; negative for money out.
        public decimal Amount { get; set; }

        // Only populated when the statement prints a balance.
        public decimal? StatementBalance { get; set; }

        // Calculated from the statement opening balance.
        public decimal CalculatedBalance { get; set; }

        // Preserves the order of transactions in the PDF.
        public int Sequence { get; set; }

        public int SourcePage { get; set; }
    }
}