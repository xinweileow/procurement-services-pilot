namespace Procurement.Api.Common;

/// <summary>Base for typed exceptions the global exception handler maps to RFC 7807 ProblemDetails.</summary>
public abstract class ApiException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

public sealed class NotFoundException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public sealed class ConflictException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

public sealed class UnprocessableException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
}

public sealed class ForbiddenException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}
