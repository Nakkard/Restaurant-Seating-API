using BusinessLogic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public sealed class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
{
    public void Configure(EntityTypeBuilder<RestaurantTable> builder)
    {
        builder.ToTable("RestaurantTables", table =>
            table.HasCheckConstraint("CK_RestaurantTables_Capacity", "[Capacity] BETWEEN 2 AND 6"));
        builder.HasKey(table => table.Id);
        builder.Property(table => table.Id).ValueGeneratedNever();
        builder.HasData(
            new RestaurantTable(1, 2),
            new RestaurantTable(2, 3),
            new RestaurantTable(3, 4),
            new RestaurantTable(4, 5),
            new RestaurantTable(5, 6));
    }
}
