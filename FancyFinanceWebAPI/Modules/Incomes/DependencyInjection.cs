using Microsoft.Extensions.DependencyInjection;

namespace FancyFinanceWebAPI.Modules.Incomes
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddIncomeModule(this IServiceCollection services)
        {
            services.AddScoped<IIncomeService, IncomeService>();
            services.AddScoped<IIncomeRepository, IncomeRepository>();
            return services;
        }
    }
}
