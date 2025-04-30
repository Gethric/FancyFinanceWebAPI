using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FancyFinanceWebAPI.Modules.Expenses
{
    [Table("Expenses")]
    public class Expense
    {
        [Key]
        [Column("expense_id")]
        public int IncomeId { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("amount")]
        public int Amount { get; set; }

        [Column("currency_id")]
        public int CurrencyId { get; set; }

        [Column("frequency_id")]
        public int FrequencyId { get; set; }

        [Column("category_id")]
        public int CategoryId { get; set; }

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
