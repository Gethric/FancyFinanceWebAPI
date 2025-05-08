using FancyFinanceWebAPI.Data;
using FancyFinanceWebAPI.Modules.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FancyFinanceWebAPI.Modules.Incomes
{
    public class IncomeService : IIncomeService
    {
        private readonly FancyFinanceDbContext _context;

        public IncomeService(FancyFinanceDbContext context)
        {
            _context = context;
        }

        public async Task<Income> CreateIncome([FromBody] CreateIncomeDTO dto, Guid userId)
        {
            var income = new Income
            {
                IncomeSource = dto.IncomeSource,
                Amount = dto.Amount,
                CurrencyId = dto.CurrencyId,
                FrequencyId = dto.FrequencyId,
                UserId = userId,
                CreatedBy = userId,
                UpdatedBy = userId
            };

            _context.Incomes.Add(income);
            await _context.SaveChangesAsync();

            return income;
        }

        public async Task<IEnumerable<Income>> GetUserIncomes(Guid userId)
        {
            return await _context.Incomes
                .Where(i => i.UserId == userId)
                .Include(i => i.Currency)
                .Include(i => i.Frequency)
                .OrderBy(i => i.IncomeSource)
                .ToListAsync();
        }

        public async Task<Income?> GetIncomeById(int incomeId, Guid userId)
        {
            return await _context.Incomes
                .Include(i => i.Currency)
                .Include(i => i.Frequency)
                .FirstOrDefaultAsync(i => i.IncomeId == incomeId && i.UserId == userId);
        }

        public async Task<bool> UpdateIncome(int id, UpdateIncomeDTO dto, Guid userId)
        {
            var income = await _context.Incomes.FirstOrDefaultAsync(i => i.IncomeId == id && i.UserId == userId);
            if (income == null) return false;

            income.IncomeSource = dto.IncomeSource;
            income.Amount = dto.Amount;
            income.CurrencyId = dto.CurrencyId;
            income.FrequencyId = dto.FrequencyId;
            income.UpdatedAt = DateTime.UtcNow;
            income.UpdatedBy = userId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteIncome(int id, Guid userId)
        {
            var income = await _context.Incomes.FirstOrDefaultAsync(i => i.IncomeId == id && i.UserId == userId);
            if (income == null) return false;

            _context.Incomes.Remove(income);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
