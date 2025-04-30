using Microsoft.EntityFrameworkCore;
using FancyFinanceWebAPI.Data;

namespace FancyFinanceWebAPI.Modules.Users;

public class UserService : IUserService
{
    private readonly FancyFinanceDbContext _db;

    public UserService(FancyFinanceDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
    }
}
