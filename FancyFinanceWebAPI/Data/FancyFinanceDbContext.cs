using Microsoft.EntityFrameworkCore;
using FancyFinanceWebAPI.Models;

namespace FancyFinanceWebAPI.Data
{
    public class FancyFinanceDbContext : DbContext
    {
        public FancyFinanceDbContext(DbContextOptions<FancyFinanceDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
    }
}
