using FancyFinanceWebAPI.Modules.Accounts;
using FancyFinanceWebAPI.Shared.Category;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FancyFinanceWebAPI.Modules.Transactions
{
    [Table("transactions")]
    public class Transaction
    {
        [Key]
        [Column("transaction_id")]
        public int TransactionId { get; set; }

        [Required]
        [Column("account_id")]
        public int AccountId { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account? Account { get; set; }

        [Required]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        [Column("transaction_date", TypeName = "date")]
        public DateOnly TransactionDate { get; set; }

        [Column("transaction_time", TypeName = "time without time zone")]
        public TimeOnly? TransactionTime { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("amount", TypeName = "numeric(18,2)")]
        public decimal Amount { get; set; }

        [Column("statement_balance", TypeName = "numeric(18,2)")]
        public decimal? StatementBalance { get; set; }

        [Column("sequence")]
        public int Sequence { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; }
    }
}