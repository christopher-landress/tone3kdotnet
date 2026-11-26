namespace Tone3000.Auth;

/// <summary>
/// Hands out a valid access token, refreshing it before it expires.
/// Refreshes are serialized because each one rotates the refresh token.
/// </summary>
public sealed class Tone3000TokenManager : IDisposable
{
    private readonly ITokenStore _store;
    private readonly Func<Tone3000OAuthClient> _oauthFactory;
    private readonly Tone3000ClientOptions _options;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>Creates the manager.</summary>
    /// <param name="store">Where tokens live.</param>
    /// <param name="oauthFactory">Creates the OAuth client used for refreshes.</param>
    /// <param name="options">Client options.</param>
    /// <param name="timeProvider">Clock, for tests.</param>
    public Tone3000TokenManager(ITokenStore store, Func<Tone3000OAuthClient> oauthFactory, Tone3000ClientOptions options, TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _oauthFactory = oauthFactory ?? throw new ArgumentNullException(nameof(oauthFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Raised after a refresh saves a new token pair to the store.</summary>
    public event EventHandler<Tone3000Tokens>? TokensRefreshed;

    /// <summary>Saves tokens from a sign-in.</summary>
    public ValueTask SetTokensAsync(Tone3000Tokens tokens, CancellationToken cancellationToken = default)
        => _store.SaveTokensAsync(tokens, cancellationToken);

    /// <summary>Clears the stored tokens (sign out).</summary>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        => _store.SaveTokensAsync(null, cancellationToken);

    /// <summary>Returns a usable access token, refreshing first when it is about to expire.</summary>
    /// <exception cref="Tone3000AuthenticationException">No tokens are stored, or they expired and cannot be refreshed.</exception>
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await _store.GetTokensAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new Tone3000AuthenticationException(null, "Not signed in to TONE3000. Complete an OAuth sign-in first.");

        if (!tokens.IsExpired(_clock, _options.RefreshBeforeExpiry))
        {
            return tokens.AccessToken;
        }

        if (tokens.RefreshToken is null)
        {
            // Nothing to refresh with; use the token while it is still technically valid.
            return tokens.IsExpired(_clock, TimeSpan.Zero)
                ? throw new Tone3000AuthenticationException(null, "The TONE3000 access token expired and there is no refresh token.")
                : tokens.AccessToken;
        }

        return (await RefreshAsync(tokens.AccessToken, cancellationToken).ConfigureAwait(false)).AccessToken;
    }

    /// <summary>
    /// Refreshes the tokens unless another caller already replaced <paramref name="staleAccessToken"/>.
    /// </summary>
    public async Task<Tone3000Tokens> RefreshAsync(string? staleAccessToken, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await _store.GetTokensAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new Tone3000AuthenticationException(null, "Not signed in to TONE3000.");

            // Someone else refreshed while we waited.
            if (staleAccessToken is not null && current.AccessToken != staleAccessToken && !current.IsExpired(_clock, _options.RefreshBeforeExpiry))
            {
                return current;
            }

            if (current.RefreshToken is null)
            {
                throw new Tone3000AuthenticationException(null, "The TONE3000 access token is no longer valid and there is no refresh token.");
            }

            Tone3000Tokens fresh;
            try
            {
                fresh = await _oauthFactory().RefreshAsync(current.RefreshToken, cancellationToken).ConfigureAwait(false);
            }
            catch (Tone3000OAuthException ex) when (ex.RequiresReauthorization)
            {
                await _store.SaveTokensAsync(null, cancellationToken).ConfigureAwait(false);
                throw new Tone3000AuthenticationException("The TONE3000 refresh token was rejected; the user must sign in again.", ex);
            }

            // Keep the old refresh token if the server did not send a new one.
            fresh = fresh.RefreshToken is null ? fresh with { RefreshToken = current.RefreshToken } : fresh;
            await _store.SaveTokensAsync(fresh, cancellationToken).ConfigureAwait(false);
            TokensRefreshed?.Invoke(this, fresh);
            return fresh;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _refreshLock.Dispose();
}
