using Microsoft.EntityFrameworkCore;
using FancyFinanceWebAPI.Models;

namespace FancyFinanceWebAPI.Data
{
    public class FancyFinanceDbContext : DbContext
    {
        public FancyFinanceDbContext(DbContextOptions<FancyFinanceDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<Category> Categories { get; set; }

        public DbSet<Currency> Currency { get; set; }

        public DbSet<Frequency> Frequency { get; set; }
    }
}
