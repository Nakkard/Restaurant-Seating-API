namespace BusinessLogic.Models;

public sealed record GroupPage(IReadOnlyList<GroupResult> Items, int Offset, int Limit);
