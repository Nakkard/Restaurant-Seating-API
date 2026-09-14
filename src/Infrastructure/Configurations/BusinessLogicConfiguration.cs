using BusinessLogic.Actions;
using BusinessLogic.Seating;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Configurations;

public static class BusinessLogicConfiguration
{
    public static IServiceCollection ConfigureBusinessLogic(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SeatingAllocator>();
        services.AddScoped<ArriveGroupAction>();
        services.AddScoped<LeaveGroupAction>();
        services.AddScoped<QueryRestaurantAction>();
        return services;
    }
}
