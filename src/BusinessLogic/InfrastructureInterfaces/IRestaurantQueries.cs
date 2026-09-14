using BusinessLogic.Models;

namespace BusinessLogic.InfrastructureInterfaces;

public interface IRestaurantQueries
{
    Task<GroupResult?> GetGroupAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupResult>> GetGroupsAsync(GroupStatus? status, int offset, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<TableResult>> GetTablesAsync(CancellationToken cancellationToken);
}
