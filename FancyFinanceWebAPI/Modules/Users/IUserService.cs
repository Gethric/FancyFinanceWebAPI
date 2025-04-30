namespace FancyFinanceWebAPI.Modules.Users;

public interface IUserService
{
    Task<User?> GetByEmailAsync(string email);
}
