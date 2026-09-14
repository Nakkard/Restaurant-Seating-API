using BusinessLogic.Models;

namespace BusinessLogic.Seating;

public sealed class SeatingAllocator
{
    public void SeatWaitingGroups(
        IReadOnlyList<RestaurantTable> tables,
        IReadOnlyList<GuestGroup> groups,
        DateTimeOffset now)
    {
        var occupied = tables.ToDictionary(table => table.Id, _ => 0);
        foreach (var group in groups.Where(group => group.Status == GroupStatus.Seated))
            occupied[group.TableId!.Value] += group.Size;

        foreach (var group in groups.Where(group => group.Status == GroupStatus.Waiting)
                     .OrderBy(group => group.ArrivalOrder))
        {
            // Empty tables take precedence even when a shared table would be an exact fit.
            var table = tables
                .Where(table => table.Capacity - occupied[table.Id] >= group.Size)
                .OrderBy(table => occupied[table.Id] == 0 ? 0 : 1)
                .ThenBy(table => table.Capacity - occupied[table.Id] - group.Size)
                .ThenBy(table => table.Id)
                .FirstOrDefault();

            if (table is null)
                continue;

            group.Seat(table.Id, now);
            occupied[table.Id] += group.Size;
        }
    }
}
