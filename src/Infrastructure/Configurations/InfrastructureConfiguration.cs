using BusinessLogic.InfrastructureInterfaces;
using Infrastructure.DataAccess;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Configurations;

public static class InfrastructureConfiguration
{
    public static IServiceCollection ConfigureInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Restaurant")
            ?? throw new InvalidOperationException("ConnectionStrings:Restaurant must be configured.");
        services.AddDbContext<RestaurantDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IRestaurantStore, SqlRestaurantStore>();
        services.AddScoped<IRestaurantQueries, SqlRestaurantQueries>();
        return services;
    }
}
