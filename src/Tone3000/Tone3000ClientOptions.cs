namespace Tone3000;

/// <summary>Settings for <see cref="Tone3000Client"/> and <see cref="Auth.Tone3000OAuthClient"/>.</summary>
public sealed class Tone3000ClientOptions
{
    /// <summary>Default API root.</summary>
    public static readonly Uri DefaultBaseAddress = new("https://www.tone3000.com/api/v1/");

    /// <summary>API root. Must end with a slash.</summary>
    public Uri BaseAddress { get; set; } = DefaultBaseAddress;

    /// <summary>Your publishable key (<c>t3k_pub_…</c>), used as the OAuth <c>client_id</c>. Never put a secret key here.</summary>
    public string? ClientId { get; set; }

    /// <summary>How long before expiry an access token is refreshed. Default 60 seconds.</summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How many times a request that gets 429 is retried after waiting for <c>Retry-After</c>. Default 2.
    /// Set 0 to turn retries off.
    /// </summary>
    public int MaxRateLimitRetries { get; set; } = 2;

    /// <summary>Longest <c>Retry-After</c> the client will wait for before throwing instead. Default 30 seconds.</summary>
    public TimeSpan MaxRetryAfter { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Wait used when a 429 has no <c>Retry-After</c> header. Default 5 seconds.</summary>
    public TimeSpan DefaultRetryAfter { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether <c>/tones/search</c> is retried on 429 too. Default false, since the endpoint is heavily rate-limited
    /// and the API docs ask clients not to hammer it.
    /// </summary>
    public bool RetrySearchOnRateLimit { get; set; }

    /// <summary>Called when a response carries deprecation headers, so you can log them.</summary>
    public Action<Tone3000DeprecationNotice>? OnDeprecation { get; set; }

    /// <summary>User-Agent product token sent with every request.</summary>
    public string UserAgent { get; set; } = "Tone3000.NET/0.1";

    internal Uri Resolve(string relative) => new(BaseAddress, relative);

    internal string RequireClientId()
        => string.IsNullOrWhiteSpace(ClientId)
            ? throw new InvalidOperationException("Tone3000ClientOptions.ClientId (your publishable key) is required for OAuth calls.")
            : ClientId;
}

/// <summary>Deprecation headers the API attached to a response.</summary>
/// <param name="RequestUri">Request that triggered it.</param>
/// <param name="Deprecations">Codes from <c>X-Tone3000-Deprecations</c>, for example <c>legacy_platform_key</c>.</param>
/// <param name="Link">Documentation link from the <c>Link: rel="deprecation"</c> header, when sent.</param>
public sealed record Tone3000DeprecationNotice(Uri? RequestUri, IReadOnlyList<string> Deprecations, string? Link);
