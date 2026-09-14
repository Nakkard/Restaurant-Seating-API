using BusinessLogic.Exceptions;
using BusinessLogic.InfrastructureInterfaces;
using BusinessLogic.Models;

namespace BusinessLogic.Actions;

public sealed class QueryRestaurantAction(IRestaurantQueries queries)
{
    public async Task<GroupResult> GetGroup(Guid id, CancellationToken cancellationToken) =>
        await queries.GetGroupAsync(id, cancellationToken) ?? throw new GroupNotFoundException(id);

    public async Task<GroupPage> GetGroups(GroupStatus? status, int offset, int limit, CancellationToken cancellationToken)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
            throw new InvalidRequestException("Unknown group status.");
        if (offset < 0 || limit is < 1 or > 100)
            throw new InvalidRequestException("Offset must be non-negative and limit must be between 1 and 100.");

        return new GroupPage(await queries.GetGroupsAsync(status, offset, limit, cancellationToken), offset, limit);
    }

    public Task<IReadOnlyList<TableResult>> GetTables(CancellationToken cancellationToken) =>
        queries.GetTablesAsync(cancellationToken);
}
