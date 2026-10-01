using System.ComponentModel.DataAnnotations;

namespace FancyFinanceWebAPI.Modules.Accounts
{
    public class CreateAccountRequest
    {
        [Required]
        [MaxLength(100)]
        public string AccountName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(
            "^(Current|Savings|CreditCard)$",
            ErrorMessage = "Account type must be Current, Savings or CreditCard.")]
        public string AccountType { get; set; } = string.Empty;

        public decimal OpeningBalance { get; set; }

        [Required]
        public DateOnly? OpeningBalanceDate { get; set; }
    }
}