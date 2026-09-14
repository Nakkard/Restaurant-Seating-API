using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Database;

public sealed class RestaurantDbContextFactory : IDesignTimeDbContextFactory<RestaurantDbContext>
{
    public RestaurantDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Restaurant")
            ?? "Server=localhost,14333;Database=RestaurantSeating;Integrated Security=true;TrustServerCertificate=true";
        return new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}
