using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using FancyFinanceWebAPI.Shared.Frequency;
using FancyFinanceWebAPI.Shared.Currency;
using FancyFinanceWebAPI.Shared.Category;
using FancyFinanceWebAPI.Modules.Users;

namespace FancyFinanceWebAPI.Modules.Expenses
{
    [Table("expenses")]
    public class Expense
    {
        [Key]
        [Column("expense_id")]
        public int ExpenseId { get; set; }

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column("expense_amount")]
        public decimal Amount { get; set; }

        [Required]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        [Required]
        [Column("currency_id")]
        public int CurrencyId { get; set; }

        [ForeignKey("CurrencyId")]
        public Currency? Currency { get; set; }

        [Required]
        [Column("frequency_id")]
        public int FrequencyId { get; set; }

        [ForeignKey("FrequencyId")]
        public Frequency? Frequency { get; set; }

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
