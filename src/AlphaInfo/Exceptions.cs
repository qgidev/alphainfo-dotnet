namespace AlphaInfo;

/// <summary>
/// Base exception for every alphainfo client error. Catch this at the
/// boundary; use subclasses for targeted handling.
/// </summary>
public class AlphaInfoException : Exception
{
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, object?>? ResponseData { get; }

    public AlphaInfoException(
        string message,
        int statusCode = 0,
        IReadOnlyDictionary<string, object?>? responseData = null,
        Exception? inner = null
    ) : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseData = responseData;
    }
}

/// <summary>
/// Invalid or missing API key (HTTP 401). Not retryable.
/// </summary>
public sealed class AuthException : AlphaInfoException
{
    public AuthException(
        string? message = null,
        int statusCode = 401,
        IReadOnlyDictionary<string, object?>? responseData = null
    ) : base(
        message ??
            "Invalid or missing API key. Get a free key at " +
            "https://alphainfo.io/register and pass it to new AlphaInfoClient(\"ai_...\").",
        statusCode,
        responseData
    )
    { }
}

/// <summary>
/// Rate/quota limit exceeded (HTTP 429). Carries the server's
/// <c>Retry-After</c> hint when present.
/// </summary>
public sealed class RateLimitException : AlphaInfoException
{
    public int RetryAfterSeconds { get; }

    public RateLimitException(
        string message,
        int retryAfterSeconds,
        int statusCode = 429,
        IReadOnlyDictionary<string, object?>? responseData = null
    ) : base(message, statusCode, responseData)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

/// <summary>
/// Request validation failure (HTTP 400, 413, 422). Not retryable.
/// </summary>
public sealed class ValidationException : AlphaInfoException
{
    public ValidationException(
        string message,
        int statusCode = 400,
        IReadOnlyDictionary<string, object?>? responseData = null
    ) : base(message, statusCode, responseData) { }
}

/// <summary>
/// Resource not found (HTTP 404).
/// </summary>
public sealed class NotFoundException : AlphaInfoException
{
    public NotFoundException(
        string message,
        int statusCode = 404,
        IReadOnlyDictionary<string, object?>? responseData = null
    ) : base(message, statusCode, responseData) { }
}

/// <summary>
/// Server-side error (HTTP 5xx).
/// </summary>
public sealed class ApiException : AlphaInfoException
{
    public ApiException(
        string message,
        int statusCode,
        IReadOnlyDictionary<string, object?>? responseData = null
    ) : base(message, statusCode, responseData) { }
}

/// <summary>
/// Transport-level failure — DNS, TCP, TLS, timeout, cancellation.
/// </summary>
public sealed class NetworkException : AlphaInfoException
{
    public NetworkException(string message, Exception? inner = null) : base(message, 0, null, inner) { }
}
