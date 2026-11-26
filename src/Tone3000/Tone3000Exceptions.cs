using System.Net;

namespace Tone3000;

/// <summary>Base type for every error this library throws about the TONE3000 API.</summary>
public class Tone3000Exception : Exception
{
    /// <summary>Creates the exception.</summary>
    public Tone3000Exception() { }
    /// <summary>Creates the exception.</summary>
    public Tone3000Exception(string message) : base(message) { }
    /// <summary>Creates the exception.</summary>
    public Tone3000Exception(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A resource endpoint returned a non-success status.</summary>
public class Tone3000ApiException : Tone3000Exception
{
    /// <summary>Creates the exception.</summary>
    public Tone3000ApiException() { }
    /// <summary>Creates the exception.</summary>
    public Tone3000ApiException(string message) : base(message) { }
    /// <summary>Creates the exception.</summary>
    public Tone3000ApiException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Creates the exception.</summary>
    public Tone3000ApiException(HttpStatusCode statusCode, string? responseBody, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>HTTP status returned.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Raw response body. Resource error bodies are undocumented, so it is kept as is.</summary>
    public string? ResponseBody { get; }

    /// <summary>True for 404.</summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>True for 403, for example a private tone or a partner-only endpoint.</summary>
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
}

/// <summary>The API answered 429 Too Many Requests.</summary>
public sealed class Tone3000RateLimitException : Tone3000ApiException
{
    /// <summary>Creates the exception.</summary>
    public Tone3000RateLimitException() { }
    /// <summary>Creates the exception.</summary>
    public Tone3000RateLimitException(string message) : base(message) { }
    /// <summary>Creates the exception.</summary>
    public Tone3000RateLimitException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Creates the exception.</summary>
    public Tone3000RateLimitException(string? responseBody, TimeSpan? retryAfter)
        : base(HttpStatusCode.TooManyRequests, responseBody,
               retryAfter is { } r ? $"TONE3000 rate limit hit; retry after {r.TotalSeconds:0} s." : "TONE3000 rate limit hit.")
        => RetryAfter = retryAfter;

    /// <summary>Delay from the <c>Retry-After</c> header, when the server sent one.</summary>
    public TimeSpan? RetryAfter { get; }
}

/// <summary>The access token was rejected, or no tokens are available and the user must sign in.</summary>
public sealed class Tone3000AuthenticationException : Tone3000ApiException
{
    /// <summary>Creates the exception.</summary>
    public Tone3000AuthenticationException() { }
    /// <summary>Creates the exception.</summary>
    public Tone3000AuthenticationException(string message) : base(message) { }
    /// <summary>Creates the exception.</summary>
    public Tone3000AuthenticationException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Creates the exception.</summary>
    public Tone3000AuthenticationException(string? responseBody, string message)
        : base(HttpStatusCode.Unauthorized, responseBody, message) { }
}

/// <summary>An OAuth endpoint returned an error such as <c>invalid_grant</c> or <c>access_denied</c>.</summary>
public sealed class Tone3000OAuthException : Tone3000Exception
{
    /// <summary>Creates the exception.</summary>
    public Tone3000OAuthException() { }
    /// <summary>Creates the exception.</summary>
    public Tone3000OAuthException(string message) : base(message) { }
    /// <summary>Creates the exception.</summary>
    public Tone3000OAuthException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Creates the exception.</summary>
    public Tone3000OAuthException(HttpStatusCode statusCode, string error, string? errorDescription)
        : base(errorDescription is null ? $"OAuth error '{error}'." : $"OAuth error '{error}': {errorDescription}")
    {
        StatusCode = statusCode;
        Error = error;
        ErrorDescription = errorDescription;
    }

    /// <summary>HTTP status returned.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>OAuth error code, for example <c>invalid_grant</c>.</summary>
    public string Error { get; } = "";

    /// <summary>Human-readable description, when sent.</summary>
    public string? ErrorDescription { get; }

    /// <summary>True when the refresh token is no longer valid and the user must sign in again.</summary>
    public bool RequiresReauthorization => Error is OAuthErrors.InvalidGrant or OAuthErrors.ExpiredToken or OAuthErrors.AccessDenied;
}

/// <summary>OAuth error codes the TONE3000 API documents.</summary>
public static class OAuthErrors
{
    /// <summary>Device flow: the user has not finished yet.</summary>
    public const string AuthorizationPending = "authorization_pending";
    /// <summary>Device flow: poll more slowly.</summary>
    public const string SlowDown = "slow_down";
    /// <summary>The user declined.</summary>
    public const string AccessDenied = "access_denied";
    /// <summary>The device code expired.</summary>
    public const string ExpiredToken = "expired_token";
    /// <summary>The code or refresh token is invalid or used.</summary>
    public const string InvalidGrant = "invalid_grant";
    /// <summary>Unknown client id.</summary>
    public const string InvalidClient = "invalid_client";
    /// <summary>Malformed request.</summary>
    public const string InvalidRequest = "invalid_request";
}
