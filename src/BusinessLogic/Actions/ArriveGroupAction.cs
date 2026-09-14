using BusinessLogic.InfrastructureInterfaces;
using BusinessLogic.Models;
using BusinessLogic.Seating;

namespace BusinessLogic.Actions;

public sealed class ArriveGroupAction(IRestaurantStore store, SeatingAllocator allocator, TimeProvider timeProvider)
{
    public async Task<GroupResult> Make(int size, CancellationToken cancellationToken)
    {
        if (size is < 1 or > 6)
            throw new Exceptions.InvalidRequestException("Group size must be between 1 and 6.");

        await using var change = await store.BeginChangeAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var group = new GuestGroup(size, now);
        await change.RegisterAsync(group, cancellationToken);
        allocator.SeatWaitingGroups(change.Tables, change.Groups, now);
        await change.CommitAsync(cancellationToken);
        return GroupResult.From(group);
    }
}
