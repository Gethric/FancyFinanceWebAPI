using Microsoft.Extensions.DependencyInjection;

namespace FancyFinanceWebAPI.Modules.Expenses
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddExpenseModule(this IServiceCollection services)
        {
            services.AddScoped<IExpenseService, ExpenseService>();
            services.AddScoped<IExpenseRepository, ExpenseRepository>();
            return services;
        }
    }
}
