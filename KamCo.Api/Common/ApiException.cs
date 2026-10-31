namespace KamCo.Api.Common;

/// <summary>Base class for exceptions that map to a specific HTTP status code.</summary>
public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Raised when a requested resource does not exist → 404.</summary>
public class NotFoundException(string message) : ApiException(404, message);

/// <summary>Raised on invalid business rules / input → 400.</summary>
public class BadRequestException(string message) : ApiException(400, message);

/// <summary>Raised when the caller is not allowed to do the action → 403.</summary>
public class ForbiddenException(string message) : ApiException(403, message);

/// <summary>Raised on duplicate resources (e.g. email already registered) → 409.</summary>
public class ConflictException(string message) : ApiException(409, message);
