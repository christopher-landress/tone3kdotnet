using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tone3000.Http;
using Tone3000.Models;
using Tone3000.Serialization;

namespace Tone3000.Auth;

/// <summary>
/// The OAuth 2.0 side of the API: authorize URLs (PKCE S256), code exchange, refresh and the device flow (RFC 8628).
/// Uses only your publishable key; there is no client secret.
/// </summary>
public sealed class Tone3000OAuthClient
{
    private readonly HttpClient _http;
    private readonly Tone3000ClientOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Creates the client.</summary>
    public Tone3000OAuthClient(HttpClient httpClient, IOptions<Tone3000ClientOptions> options, TimeProvider? timeProvider = null)
        : this(httpClient, options?.Value ?? throw new ArgumentNullException(nameof(options)), timeProvider) { }

    /// <summary>Creates the client.</summary>
    public Tone3000OAuthClient(HttpClient httpClient, Tone3000ClientOptions options, TimeProvider? timeProvider = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Builds the URL to open in a browser for sign-in, tone select or tone load.</summary>
    public Uri BuildAuthorizationUri(AuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Prompt == AuthorizationPrompt.LoadTone && request.ToneId is null)
        {
            throw new ArgumentException("ToneId is required with AuthorizationPrompt.LoadTone.", nameof(request));
        }

        var q = new QueryString()
            .Add("client_id", _options.RequireClientId())
            .Add("redirect_uri", request.RedirectUri.OriginalString)
            .Add("response_type", "code")
            .Add("code_challenge", request.Pkce.CodeChallenge)
            .Add("code_challenge_method", "S256")
            .Add("state", request.Pkce.State)
            .Add("login_hint", request.LoginHint)
            .Add("locale", request.Locale)
            .Add("prompt", request.Prompt switch
            {
                AuthorizationPrompt.SelectTone => "select_tone",
                AuthorizationPrompt.LoadTone => "load_tone",
                _ => null,
            })
            .Add("tone_id", request.ToneId)
            .AddJoined("gears", request.Gears?.Select(WireValues.ToWire), '_')
            .AddEnum("format", request.Format)
            .AddEnum("architecture", request.Architecture)
            .Add("calibrated", request.Calibrated)
            .Add("menubar", request.Menubar)
            .Add("preview", request.Preview);

        return new Uri(_options.Resolve("oauth/authorize") + q.ToString());
    }

    /// <summary>Exchanges the code from the callback for tokens.</summary>
    public Task<Tone3000Tokens> ExchangeCodeAsync(string code, string codeVerifier, Uri redirectUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);
        ArgumentNullException.ThrowIfNull(redirectUri);
        return RequestTokensAsync(
        [
            new("grant_type", "authorization_code"),
            new("code", code),
            new("code_verifier", codeVerifier),
            new("redirect_uri", redirectUri.OriginalString),
            new("client_id", _options.RequireClientId()),
        ], cancellationToken);
    }

    /// <summary>Gets a new token pair. The old refresh token stops working, so save the result.</summary>
    public Task<Tone3000Tokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);
        return RequestTokensAsync(
        [
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken),
            new("client_id", _options.RequireClientId()),
        ], cancellationToken);
    }

    /// <summary>Starts the device flow. Show the returned user code and verification URI, then call <see cref="PollDeviceTokenAsync"/>.</summary>
    public async Task<DeviceAuthorization> StartDeviceAuthorizationAsync(string? scope = null, CancellationToken cancellationToken = default)
    {
        var form = new List<KeyValuePair<string, string>> { new("client_id", _options.RequireClientId()) };
        if (!string.IsNullOrEmpty(scope))
        {
            form.Add(new("scope", scope));
        }

        using var response = await PostFormAsync("oauth/device_authorization", form, cancellationToken).ConfigureAwait(false);
        await ThrowIfOAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync(Tone3000JsonContext.Default.DeviceAuthorization, cancellationToken).ConfigureAwait(false)
            ?? throw new Tone3000Exception("Empty device authorization response.");
    }

    /// <summary>
    /// Polls until the user approves, honoring <c>interval</c> and <c>slow_down</c>.
    /// Throws <see cref="Tone3000OAuthException"/> on <c>access_denied</c>, <c>expired_token</c> and other errors.
    /// </summary>
    public async Task<Tone3000Tokens> PollDeviceTokenAsync(DeviceAuthorization authorization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var interval = TimeSpan.FromSeconds(Math.Max(1, authorization.Interval));
        var deadline = _clock.GetUtcNow().AddSeconds(authorization.ExpiresIn > 0 ? authorization.ExpiresIn : 600);

        while (true)
        {
            await Task.Delay(interval, _clock, cancellationToken).ConfigureAwait(false);
            try
            {
                return await RequestTokensAsync(
                [
                    new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                    new("device_code", authorization.DeviceCode),
                    new("client_id", _options.RequireClientId()),
                ], cancellationToken).ConfigureAwait(false);
            }
            catch (Tone3000OAuthException ex) when (ex.Error == OAuthErrors.AuthorizationPending)
            {
            }
            catch (Tone3000OAuthException ex) when (ex.Error == OAuthErrors.SlowDown)
            {
                interval += TimeSpan.FromSeconds(5);
            }

            if (_clock.GetUtcNow() >= deadline)
            {
                throw new Tone3000OAuthException(HttpStatusCode.BadRequest, OAuthErrors.ExpiredToken, "The device code expired before the user approved it.");
            }
        }
    }

    private async Task<Tone3000Tokens> RequestTokensAsync(List<KeyValuePair<string, string>> form, CancellationToken cancellationToken)
    {
        using var response = await PostFormAsync("oauth/token", form, cancellationToken).ConfigureAwait(false);
        await ThrowIfOAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadFromJsonAsync(Tone3000JsonContext.Default.TokenResponse, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrEmpty(body.AccessToken))
        {
            throw new Tone3000Exception("The token response had no access_token.");
        }

        return Tone3000Tokens.From(body, _clock);
    }

    private async Task<HttpResponseMessage> PostFormAsync(string path, List<KeyValuePair<string, string>> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Resolve(path))
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ThrowIfOAuthErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        OAuthErrorBody? error = null;
        try
        {
            error = JsonSerializer.Deserialize(raw, Tone3000JsonContext.Default.OAuthErrorBody);
        }
        catch (JsonException)
        {
        }

        if (error?.Error is { Length: > 0 } code)
        {
            throw new Tone3000OAuthException(response.StatusCode, code, error.ErrorDescription);
        }

        throw new Tone3000ApiException(response.StatusCode, raw, $"OAuth endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}.");
    }
}
