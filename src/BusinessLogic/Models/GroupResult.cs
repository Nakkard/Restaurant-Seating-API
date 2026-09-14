namespace BusinessLogic.Models;

public sealed record GroupResult(
    Guid Id, int Size, GroupStatus Status, long ArrivalOrder, int? TableId,
    DateTimeOffset ArrivedAt, DateTimeOffset? SeatedAt, DateTimeOffset? LeftAt)
{
    public static GroupResult From(GuestGroup group) => new(
        group.Id, group.Size, group.Status, group.ArrivalOrder, group.TableId,
        group.ArrivedAt, group.SeatedAt, group.LeftAt);
}
