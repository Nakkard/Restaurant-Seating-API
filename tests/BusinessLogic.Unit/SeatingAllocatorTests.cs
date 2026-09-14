using BusinessLogic.Models;
using BusinessLogic.Seating;

namespace BusinessLogic.Unit;

public sealed class SeatingAllocatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
    private readonly SeatingAllocator _allocator = new();

    [Fact]
    public void EmptyTableWinsEvenWhenSharedTableHasAnExactFit()
    {
        var seated = Group(1, 2);
        seated.Seat(1, Now);
        var waiting = Group(2, 2);

        _allocator.SeatWaitingGroups([new(1, 4), new(2, 6)], [seated, waiting], Now);

        Assert.Equal(2, waiting.TableId);
        Assert.Equal(1, seated.TableId);
    }

    [Fact]
    public void SmallestEmptyTableWinsWithIdAsTieBreaker()
    {
        var group = Group(1, 2);
        _allocator.SeatWaitingGroups([new(3, 2), new(1, 4), new(2, 2)], [group], Now);
        Assert.Equal(2, group.TableId);
    }

    [Fact]
    public void SharedTableWithLeastRemainingSpaceWins()
    {
        var first = Group(1, 1);
        first.Seat(1, Now);
        var second = Group(2, 2);
        second.Seat(2, Now);
        var waiting = Group(3, 2);

        _allocator.SeatWaitingGroups([new(1, 4), new(2, 4)], [first, second, waiting], Now);

        Assert.Equal(2, waiting.TableId);
    }

    [Fact]
    public void EarlierGroupWinsEvenIfInputIsUnsorted()
    {
        var earlier = Group(1, 4);
        var later = Group(2, 2);
        _allocator.SeatWaitingGroups([new(1, 4)], [later, earlier], Now);
        Assert.Equal(GroupStatus.Seated, earlier.Status);
        Assert.Equal(GroupStatus.Waiting, later.Status);
    }

    [Fact]
    public void UnseatableGroupDoesNotBlockSmallerGroupsBehindIt()
    {
        var earlier = Group(1, 6);
        var later = Group(2, 2);
        _allocator.SeatWaitingGroups([new(1, 2)], [earlier, later], Now);
        Assert.Equal(GroupStatus.Waiting, earlier.Status);
        Assert.Equal(GroupStatus.Seated, later.Status);
    }

    [Fact]
    public void GroupCannotBeSplitAcrossTables()
    {
        var group = Group(1, 5);
        _allocator.SeatWaitingGroups([new(1, 3), new(2, 3)], [group], Now);
        Assert.Equal(GroupStatus.Waiting, group.Status);
        Assert.Null(group.TableId);
    }

    [Fact]
    public void MultipleGroupsCanShareButNeverExceedCapacity()
    {
        var groups = new[] { Group(1, 2), Group(2, 2), Group(3, 1) };
        _allocator.SeatWaitingGroups([new(1, 4)], groups, Now);
        Assert.All(groups.Take(2), group => Assert.Equal(1, group.TableId));
        Assert.Equal(GroupStatus.Waiting, groups[2].Status);
    }

    [Fact]
    public void DepartureSeatsWaitingGroupsAndPreservesOtherAssignments()
    {
        var leaving = Group(1, 2);
        leaving.Seat(1, Now);
        var staying = Group(2, 2);
        staying.Seat(1, Now);
        var waiting = Group(3, 2);
        leaving.Leave(Now.AddHours(1));

        _allocator.SeatWaitingGroups([new(1, 4)], [leaving, staying, waiting], Now.AddHours(1));

        Assert.Equal(GroupStatus.Completed, leaving.Status);
        Assert.Equal(1, staying.TableId);
        Assert.Equal(GroupStatus.Seated, waiting.Status);
    }

    [Fact]
    public void GroupThatLeftTheQueueIsNeverSeated()
    {
        var group = Group(1, 2);
        group.Leave(Now);
        _allocator.SeatWaitingGroups([new(1, 6)], [group], Now);
        Assert.Equal(GroupStatus.Left, group.Status);
        Assert.Null(group.TableId);
    }

    [Fact]
    public void NoTablesLeavesEveryoneWaiting()
    {
        var group = Group(1, 1);
        _allocator.SeatWaitingGroups([], [group], Now);
        Assert.Equal(GroupStatus.Waiting, group.Status);
    }

    private static GuestGroup Group(long order, int size)
    {
        var group = new GuestGroup(size, Now);
        // EF assigns this database-generated property; fixtures emulate persisted arrival order.
        typeof(GuestGroup).GetProperty(nameof(GuestGroup.ArrivalOrder))!.SetValue(group, order);
        return group;
    }
}
