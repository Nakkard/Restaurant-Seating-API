using BusinessLogic.Exceptions;
using BusinessLogic.Models;

namespace BusinessLogic.Unit;

public sealed class GuestGroupTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    public void InvalidSizeIsRejected(int size) =>
        Assert.Throws<InvalidRequestException>(() => new GuestGroup(size, Now));

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void ValidSizeStartsWaiting(int size)
    {
        var group = new GuestGroup(size, Now);
        Assert.Equal(GroupStatus.Waiting, group.Status);
        Assert.Null(group.TableId);
    }

    [Fact]
    public void SeatedGroupCannotBeMoved()
    {
        var group = new GuestGroup(2, Now);
        group.Seat(1, Now);
        Assert.Throws<InvalidOperationException>(() => group.Seat(2, Now));
        Assert.Equal(1, group.TableId);
    }

    [Theory]
    [InlineData(false, GroupStatus.Left)]
    [InlineData(true, GroupStatus.Completed)]
    public void LeavingIsIdempotent(bool seated, GroupStatus expected)
    {
        var group = new GuestGroup(2, Now);
        if (seated)
            group.Seat(1, Now);
        group.Leave(Now.AddHours(1));
        group.Leave(Now.AddHours(2));
        Assert.Equal(expected, group.Status);
        Assert.Equal(Now.AddHours(1), group.LeftAt);
        Assert.Throws<InvalidOperationException>(() => group.Seat(1, Now));
    }
}
