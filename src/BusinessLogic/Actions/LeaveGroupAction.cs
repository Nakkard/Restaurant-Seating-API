using BusinessLogic.Exceptions;
using BusinessLogic.InfrastructureInterfaces;
using BusinessLogic.Models;
using BusinessLogic.Seating;

namespace BusinessLogic.Actions;

public sealed class LeaveGroupAction(IRestaurantStore store, SeatingAllocator allocator, TimeProvider timeProvider)
{
    public async Task<GroupResult> Make(Guid id, CancellationToken cancellationToken)
    {
        await using var change = await store.BeginChangeAsync(cancellationToken);
        var group = await change.FindGroupAsync(id, cancellationToken)
                    ?? throw new GroupNotFoundException(id);
        var now = timeProvider.GetUtcNow();
        group.Leave(now);
        allocator.SeatWaitingGroups(change.Tables, change.Groups, now);
        await change.CommitAsync(cancellationToken);
        return GroupResult.From(group);
    }
}
