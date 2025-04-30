using Microsoft.EntityFrameworkCore;
using FancyFinanceWebAPI.Modules.Users;
using FancyFinanceWebAPI.Modules.Incomes;
using FancyFinanceWebAPI.Modules.Expenses;
using FancyFinanceWebAPI.Shared.Category;
using FancyFinanceWebAPI.Shared.Currency;
using FancyFinanceWebAPI.Shared.Frequency;

namespace FancyFinanceWebAPI.Data
{
    public class FancyFinanceDbContext : DbContext
    {
        public FancyFinanceDbContext(DbContextOptions<FancyFinanceDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<Category> Categories { get; set; }

        public DbSet<Currency> Currencies { get; set; }

        public DbSet<Frequency> Frequencies { get; set; }

        public DbSet<Income> Incomes { get; set; }

        public DbSet<Expense> Expenses { get; set; }
    }
}
