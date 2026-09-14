using BusinessLogic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public sealed class GuestGroupConfiguration : IEntityTypeConfiguration<GuestGroup>
{
    public void Configure(EntityTypeBuilder<GuestGroup> builder)
    {
        builder.ToTable("GuestGroups", table =>
        {
            table.HasCheckConstraint("CK_GuestGroups_Size", "[Size] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_GuestGroups_State", """
                ([Status] = 'Waiting' AND [TableId] IS NULL AND [SeatedAt] IS NULL AND [LeftAt] IS NULL) OR
                ([Status] = 'Seated' AND [TableId] IS NOT NULL AND [SeatedAt] IS NOT NULL AND [LeftAt] IS NULL) OR
                ([Status] = 'Completed' AND [TableId] IS NOT NULL AND [SeatedAt] IS NOT NULL AND [LeftAt] IS NOT NULL) OR
                ([Status] = 'Left' AND [TableId] IS NULL AND [SeatedAt] IS NULL AND [LeftAt] IS NOT NULL)
                """);
        });
        builder.HasKey(group => group.Id).IsClustered(false);
        builder.Property(group => group.Id).ValueGeneratedNever();
        builder.Property(group => group.ArrivalOrder).UseIdentityColumn();
        builder.HasIndex(group => group.ArrivalOrder).IsUnique().IsClustered();
        builder.Property(group => group.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(group => new { group.Status, group.ArrivalOrder });
        builder.HasIndex(group => new { group.TableId, group.Status });
        builder.HasOne<RestaurantTable>().WithMany().HasForeignKey(group => group.TableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
