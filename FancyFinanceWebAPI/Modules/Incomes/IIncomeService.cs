namespace FancyFinanceWebAPI.Modules.Incomes
{
    public interface IIncomeService
    {
        Task<IEnumerable<Income>> GetUserIncomes(Guid userId);
        Task<Income?> GetIncomeById(int incomeId, Guid userId);
        Task<Income> CreateIncome(CreateIncomeDTO dto, Guid userId);
        Task<bool> UpdateIncome(int id, UpdateIncomeDTO dto, Guid userId);
        Task<bool> DeleteIncome(int id, Guid userId);
    }
}
