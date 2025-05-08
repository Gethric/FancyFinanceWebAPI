namespace FancyFinanceWebAPI.Modules.Expenses
{
    public class UpdateExpenseDTO
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int CategoryId { get; set; }
        public int CurrencyId { get; set; }
        public int FrequencyId { get; set; }
    }
}
