using BusinessLogic.Exceptions;

namespace BusinessLogic.Models;

public sealed class GuestGroup
{
    private GuestGroup() { }

    public GuestGroup(int size, DateTimeOffset arrivedAt)
    {
        if (size is < 1 or > 6)
            throw new InvalidRequestException("Group size must be between 1 and 6.");

        Id = Guid.NewGuid();
        Size = size;
        ArrivedAt = arrivedAt;
        Status = GroupStatus.Waiting;
    }

    public Guid Id { get; private set; }
    public long ArrivalOrder { get; private set; }
    public int Size { get; private set; }
    public GroupStatus Status { get; private set; }
    public int? TableId { get; private set; }
    public DateTimeOffset ArrivedAt { get; private set; }
    public DateTimeOffset? SeatedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }

    public void Seat(int tableId, DateTimeOffset now)
    {
        if (Status != GroupStatus.Waiting)
            throw new InvalidOperationException("Only a waiting group can be seated.");
        if (tableId <= 0)
            throw new ArgumentOutOfRangeException(nameof(tableId));

        TableId = tableId;
        SeatedAt = now;
        Status = GroupStatus.Seated;
    }

    public void Leave(DateTimeOffset now)
    {
        if (Status is GroupStatus.Left or GroupStatus.Completed)
            return;

        Status = Status == GroupStatus.Seated ? GroupStatus.Completed : GroupStatus.Left;
        LeftAt = now;
    }
}
