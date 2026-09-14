namespace BusinessLogic.Exceptions;

public sealed class InvalidRequestException(string message) : Exception(message);
