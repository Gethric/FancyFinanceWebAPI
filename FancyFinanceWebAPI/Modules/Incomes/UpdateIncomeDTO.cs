namespace FancyFinanceWebAPI.Modules.Incomes
{
    public class UpdateIncomeDTO
    {
        public string IncomeSource { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public int FrequencyId { get; set; }
    }
}
