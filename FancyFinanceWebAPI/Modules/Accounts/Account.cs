using FancyFinanceWebAPI.Modules.Users;
using FancyFinanceWebAPI.Shared.Currency;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FancyFinanceWebAPI.Modules.Accounts
{
    [Table("accounts")]
    public class Account
    {
        [Key]
        [Column("account_id")]
        public int AccountId { get; set; }

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("account_name")]
        public string AccountName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("account_type")]
        public string AccountType { get; set; } = string.Empty;

        [Required]
        [Column("currency_id")]
        public int CurrencyId { get; set; }

        [ForeignKey(nameof(CurrencyId))]
        public Currency? Currency { get; set; }

        [Column("opening_balance", TypeName = "numeric(18,2)")]
        public decimal OpeningBalance { get; set; }

        [Column("opening_balance_date", TypeName = "date")]
        public DateOnly OpeningBalanceDate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

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