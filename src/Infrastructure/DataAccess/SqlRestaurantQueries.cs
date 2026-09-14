using BusinessLogic.InfrastructureInterfaces;
using BusinessLogic.Models;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DataAccess;

public sealed class SqlRestaurantQueries(RestaurantDbContext database) : IRestaurantQueries
{
    public async Task<GroupResult?> GetGroupAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await database.Groups.AsNoTracking().SingleOrDefaultAsync(group => group.Id == id, cancellationToken);
        return group is null ? null : GroupResult.From(group);
    }

    public async Task<IReadOnlyList<GroupResult>> GetGroupsAsync(
        GroupStatus? status, int offset, int limit, CancellationToken cancellationToken)
    {
        var query = database.Groups.AsNoTracking();
        if (status.HasValue)
            query = query.Where(group => group.Status == status.Value);

        var groups = await query.OrderBy(group => group.ArrivalOrder)
            .Skip(offset).Take(limit).ToListAsync(cancellationToken);
        return groups.Select(GroupResult.From).ToArray();
    }

    public async Task<IReadOnlyList<TableResult>> GetTablesAsync(CancellationToken cancellationToken) =>
        await database.Tables.AsNoTracking().OrderBy(table => table.Id)
            .Select(table => new TableResult(table.Id, table.Capacity,
                database.Groups.Where(group => group.TableId == table.Id && group.Status == GroupStatus.Seated)
                    .Sum(group => (int?)group.Size) ?? 0))
            .ToListAsync(cancellationToken);
}
