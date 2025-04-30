using Microsoft.Extensions.DependencyInjection;

namespace FancyFinanceWebAPI.Modules.Users;

public static class DependencyInjection
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
