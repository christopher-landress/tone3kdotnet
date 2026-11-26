namespace Tone3000.Auth;

/// <summary>A signed-in user's token pair. Persist every new pair: refresh tokens rotate on each refresh.</summary>
public sealed record Tone3000Tokens
{
    /// <summary>Bearer access token.</summary>
    public required string AccessToken { get; init; }
    /// <summary>Refresh token. Each refresh invalidates the previous one.</summary>
    public string? RefreshToken { get; init; }
    /// <summary>When the access token expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
    /// <summary>Granted scope.</summary>
    public string? Scope { get; init; }

    /// <summary>True when the access token has expired or expires within <paramref name="skew"/>.</summary>
    public bool IsExpired(TimeProvider clock, TimeSpan skew) => clock.GetUtcNow() + skew >= ExpiresAt;

    internal static Tone3000Tokens From(TokenResponse response, TimeProvider clock) => new()
    {
        AccessToken = response.AccessToken,
        RefreshToken = response.RefreshToken,
        ExpiresAt = clock.GetUtcNow().AddSeconds(response.ExpiresIn),
        Scope = response.Scope,
    };
}

/// <summary>Raw body of <c>POST /oauth/token</c>.</summary>
internal sealed record TokenResponse
{
    public string AccessToken { get; init; } = "";
    public string? RefreshToken { get; init; }
    public string? TokenType { get; init; }
    public int ExpiresIn { get; init; }
    public string? Scope { get; init; }
}

internal sealed record OAuthErrorBody
{
    public string? Error { get; init; }
    public string? ErrorDescription { get; init; }
}

/// <summary>Where the client reads and saves the user's tokens. Implement it over your app's secure storage.</summary>
public interface ITokenStore
{
    /// <summary>Returns the current tokens, or null if the user has not signed in.</summary>
    ValueTask<Tone3000Tokens?> GetTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves a new token pair, or clears it when <paramref name="tokens"/> is null.</summary>
    ValueTask SaveTokensAsync(Tone3000Tokens? tokens, CancellationToken cancellationToken = default);
}

/// <summary>Keeps tokens in memory only. Fine for tests and short-lived tools; real apps should persist tokens.</summary>
public sealed class InMemoryTokenStore(Tone3000Tokens? initial = null) : ITokenStore
{
    private volatile Tone3000Tokens? _tokens = initial;

    /// <inheritdoc />
    public ValueTask<Tone3000Tokens?> GetTokensAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_tokens);

    /// <inheritdoc />
    public ValueTask SaveTokensAsync(Tone3000Tokens? tokens, CancellationToken cancellationToken = default)
    {
        _tokens = tokens;
        return ValueTask.CompletedTask;
    }
}
