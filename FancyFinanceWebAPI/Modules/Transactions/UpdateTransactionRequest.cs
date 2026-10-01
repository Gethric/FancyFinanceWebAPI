using System.ComponentModel.DataAnnotations;

namespace FancyFinanceWebAPI.Modules.Transactions
{
    public class UpdateTransactionRequest
    {
        [Range(1, int.MaxValue)]
        public int AccountId { get; set; }

        [Required]
        public DateOnly? TransactionDate { get; set; }

        public TimeOnly? TransactionTime { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public decimal? Amount { get; set; }

        [Range(1, int.MaxValue)]
        public int? CategoryId { get; set; }
    }
}