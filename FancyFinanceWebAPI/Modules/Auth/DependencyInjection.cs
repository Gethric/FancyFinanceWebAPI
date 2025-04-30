using Microsoft.Extensions.DependencyInjection;

namespace FancyFinanceWebAPI.Modules.Auth
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAuthModule(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            return services;
        }
    }
}
