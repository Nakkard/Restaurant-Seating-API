namespace BusinessLogic.Exceptions;

public sealed class GroupNotFoundException(Guid id) : Exception($"Group '{id}' was not found.");
