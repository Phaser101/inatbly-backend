namespace Intably.Application.Processes;

public sealed class ProcessValidationException(string message)
    : Exception(message);

public sealed class ProcessForbiddenException(string message)
    : Exception(message);

public sealed class ProcessNotFoundException(string message)
    : Exception(message);

public sealed class ProcessConflictException(string message)
    : Exception(message);
