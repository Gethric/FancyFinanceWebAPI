using FancyFinanceWebAPI.Modules.Users;
using FancyFinanceWebAPI.Shared.Currency;
using FancyFinanceWebAPI.Shared.Frequency;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FancyFinanceWebAPI.Modules.Incomes
{
    [Table("Incomes")]
    public class Income
    {
        [Key]
        [Column("income_id")]
        public int IncomeId { get; set; }

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [Column("income_source")]
        public string IncomeSource { get; set; } = string.Empty;

        [Required]
        [Column("income_amount")]
        public decimal Amount { get; set; }

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
