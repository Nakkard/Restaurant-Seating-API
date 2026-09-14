using BusinessLogic.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

public sealed class RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : DbContext(options)
{
    public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
    public DbSet<GuestGroup> Groups => Set<GuestGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RestaurantDbContext).Assembly);
}
