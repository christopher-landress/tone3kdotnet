using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using Tone3000.Auth;
using Tone3000.Http;
using Tone3000.Models;
using Tone3000.Serialization;

namespace Tone3000;

/// <summary>
/// Client for the TONE3000 v1 public API. The <see cref="HttpClient"/> you pass must run through
/// <see cref="Tone3000AuthHandler"/>; <see cref="Create"/> and <c>AddTone3000Client</c> set that up.
/// </summary>
public sealed class Tone3000Client : ITone3000Client
{
    private readonly HttpClient _http;
    private readonly Tone3000ClientOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Creates the client over an <see cref="HttpClient"/> that already authenticates.</summary>
    public Tone3000Client(HttpClient httpClient, IOptions<Tone3000ClientOptions> options, TimeProvider? timeProvider = null)
        : this(httpClient, options?.Value ?? throw new ArgumentNullException(nameof(options)), timeProvider) { }

    /// <summary>Creates the client over an <see cref="HttpClient"/> that already authenticates.</summary>
    public Tone3000Client(HttpClient httpClient, Tone3000ClientOptions options, TimeProvider? timeProvider = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Builds a ready-to-use client without dependency injection.
    /// Keep one instance for the app's lifetime, like <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="options">Options; <see cref="Tone3000ClientOptions.ClientId"/> is needed for refreshes.</param>
    /// <param name="tokenStore">Where tokens are read and saved.</param>
    /// <param name="innerHandler">Optional transport handler, for tests or proxies.</param>
    public static Tone3000ClientBundle Create(Tone3000ClientOptions options, ITokenStore tokenStore, HttpMessageHandler? innerHandler = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokenStore);

        var transport = innerHandler ?? new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        var oauthHttp = new HttpClient(transport, disposeHandler: false);
        var oauth = new Tone3000OAuthClient(oauthHttp, options);
        var tokens = new Tone3000TokenManager(tokenStore, () => oauth, options);
        var apiHttp = new HttpClient(new Tone3000AuthHandler(tokens) { InnerHandler = transport }, disposeHandler: true);
        return new Tone3000ClientBundle(new Tone3000Client(apiHttp, options), oauth, tokens, [apiHttp, oauthHttp, tokens]);
    }

    /// <inheritdoc />
    public Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        => GetJsonAsync("user", Tone3000JsonContext.Default.User, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<PublicUser>> ListUsersAsync(UserListQuery? query = null, CancellationToken cancellationToken = default)
        => GetJsonAsync("users" + new QueryString()
                .AddEnum("sort", query?.Sort)
                .Add("query", query?.Query)
                .AddPaging(query),
            Tone3000JsonContext.Default.PagedResultPublicUser, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<Tone>> ListCreatedTonesAsync(ToneListQuery? query = null, CancellationToken cancellationToken = default)
        => ListUserTonesAsync("tones/created", query, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<Tone>> ListFavoritedTonesAsync(ToneListQuery? query = null, CancellationToken cancellationToken = default)
        => ListUserTonesAsync("tones/favorited", query, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<Tone>> ListDownloadedTonesAsync(ToneListQuery? query = null, CancellationToken cancellationToken = default)
        => ListUserTonesAsync("tones/downloaded", query, cancellationToken);

    /// <inheritdoc />
    public Task<Tone> GetToneAsync(long toneId, Architecture? architecture = null, CancellationToken cancellationToken = default)
        => GetJsonAsync(Invariant($"tones/{toneId}") + new QueryString().AddEnum("architecture", architecture),
            Tone3000JsonContext.Default.Tone, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async Task<Favorite> FavoriteToneAsync(long toneId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Put, Invariant($"tones/{toneId}/favorite"), isSearch: false, cancellationToken).ConfigureAwait(false);
        return await ReadJsonAsync(response, Tone3000JsonContext.Default.Favorite, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnfavoriteToneAsync(long toneId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, Invariant($"tones/{toneId}/favorite"), isSearch: false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PagedResult<Tone>> SearchTonesAsync(ToneSearchQuery? query = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryString()
            .Add("query", query?.Query)
            .AddEnum("sort", query?.Sort)
            .AddJoined("gears", query?.Gears?.Select(WireValues.ToWire), '_')
            // The docs disagree (underscore in the parameter table, hyphen in the Size section).
            // Hyphen is the form the Size section names as current; sizes never contain hyphens.
            .AddJoined("sizes", query?.Sizes?.Select(WireValues.ToWire), '-')
            .AddJoined("tags", query?.Tags, '_')
            .AddJoined("makes", query?.Makes, '_')
            .AddJoined("creators", query?.Creators, ',')
            .AddEnum("format", query?.Format)
            .AddEnum("architecture", query?.Architecture)
            .Add("calibrated", query?.Calibrated)
            .Add("verified", query?.Verified)
            .AddPaging(query);
        return GetJsonAsync("tones/search" + q, Tone3000JsonContext.Default.PagedResultTone, isSearch: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tone>> GetTrendingTonesAsync(Gear? gear = null, CancellationToken cancellationToken = default)
        => (await GetJsonAsync("tones/trending" + new QueryString().AddEnum("gear", gear),
            Tone3000JsonContext.Default.DataListTone, cancellationToken: cancellationToken).ConfigureAwait(false)).Data;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tone>> GetLatestTonesAsync(CancellationToken cancellationToken = default)
        => (await GetJsonAsync("tones/latest", Tone3000JsonContext.Default.DataListTone, cancellationToken: cancellationToken).ConfigureAwait(false)).Data;

    /// <inheritdoc />
    public Task<Model> GetModelAsync(long modelId, CancellationToken cancellationToken = default)
        => GetJsonAsync(Invariant($"models/{modelId}"), Tone3000JsonContext.Default.Model, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<Model>> ListModelsAsync(ModelListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Architecture == Architecture.Unknown)
        {
            throw new ArgumentException("ModelListQuery.Architecture must be A1, A2 or Custom.", nameof(query));
        }

        return GetJsonAsync("models" + new QueryString()
                .Add("tone_id", query.ToneId)
                .AddEnum("architecture", (Architecture?)query.Architecture)
                .AddPaging(query),
            Tone3000JsonContext.Default.PagedResultModel, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<Model>> LookupModelsAsync(string contentHash, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return GetJsonAsync("models/lookup" + new QueryString()
                .Add("content_hash", contentHash)
                .Add("page", page)
                .Add("page_size", pageSize),
            Tone3000JsonContext.Default.PagedResultModel, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<PublicMake>> ListMakesAsync(TaxonomyQuery? query = null, CancellationToken cancellationToken = default)
        => GetJsonAsync("makes" + TaxonomyQueryString(query), Tone3000JsonContext.Default.PagedResultPublicMake, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<PublicTag>> ListTagsAsync(TaxonomyQuery? query = null, CancellationToken cancellationToken = default)
        => GetJsonAsync("tags" + TaxonomyQueryString(query), Tone3000JsonContext.Default.PagedResultPublicTag, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<ModelDownload> DownloadModelAsync(Model model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Uri.TryCreate(model.ModelUrl, UriKind.Absolute, out var uri)
            ? DownloadModelAsync(uri, cancellationToken)
            : throw new ArgumentException($"Model {model.Id} has no absolute model_url.", nameof(model));
    }

    /// <inheritdoc />
    public async Task<ModelDownload> DownloadModelAsync(Uri modelUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modelUrl);
        if (!modelUrl.IsAbsoluteUri || modelUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("model_url must be an absolute https URL.", nameof(modelUrl));
        }

        var response = await SendCoreAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, modelUrl);
            // Only send the user's bearer token to TONE3000 itself, never to a third-party host.
            if (!IsTone3000Host(modelUrl))
            {
                request.Options.Set(Tone3000AuthHandler.SkipAuth, true);
            }

            return request;
        }, isSearch: false, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new ModelDownload(response, stream);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    internal static bool IsTone3000Host(Uri uri)
        => uri.Host.Equals("tone3000.com", StringComparison.OrdinalIgnoreCase)
           || uri.Host.EndsWith(".tone3000.com", StringComparison.OrdinalIgnoreCase);

    private Task<PagedResult<Tone>> ListUserTonesAsync(string path, ToneListQuery? query, CancellationToken cancellationToken)
        => GetJsonAsync(path + new QueryString()
                .AddEnum("gear", query?.Gear)
                .Add("query", query?.Query)
                .AddPaging(query),
            Tone3000JsonContext.Default.PagedResultTone, cancellationToken: cancellationToken);

    private static QueryString TaxonomyQueryString(TaxonomyQuery? query)
        => new QueryString().Add("query", query?.Query).AddEnum("sort", query?.Sort).AddPaging(query);

    private async Task<T> GetJsonAsync<T>(string relative, JsonTypeInfo<T> typeInfo, bool isSearch = false, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, relative, isSearch, cancellationToken).ConfigureAwait(false);
        return await ReadJsonAsync(response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string relative, bool isSearch, CancellationToken cancellationToken)
    {
        var uri = _options.Resolve(relative);
        return SendCoreAsync(() => new HttpRequestMessage(method, uri), isSearch, HttpCompletionOption.ResponseContentRead, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(Func<HttpRequestMessage> createRequest, bool isSearch, HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        var maxRetries = isSearch && !_options.RetrySearchOnRateLimit ? 0 : Math.Max(0, _options.MaxRateLimitRetries);
        for (var attempt = 0; ; attempt++)
        {
            using var request = createRequest();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);

            var response = await _http.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
            ReportDeprecations(response);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfter(response);
                    var wait = retryAfter ?? _options.DefaultRetryAfter;
                    if (attempt < maxRetries && wait <= _options.MaxRetryAfter)
                    {
                        await Task.Delay(wait, _clock, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new Tone3000RateLimitException(body, retryAfter);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Tone3000AuthenticationException(body, "TONE3000 rejected the access token (401). The user may need to sign in again.");
                }

                throw new Tone3000ApiException(response.StatusCode, body,
                    $"TONE3000 returned {(int)response.StatusCode} {response.ReasonPhrase} for {request.Method} {request.RequestUri?.AbsolutePath}.");
            }
        }
    }

    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - _clock.GetUtcNow();
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private void ReportDeprecations(HttpResponseMessage response)
    {
        if (_options.OnDeprecation is not { } callback)
        {
            return;
        }

        var codes = response.Headers.TryGetValues("X-Tone3000-Deprecations", out var values)
            ? values.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToList()
            : [];
        var flagged = response.Headers.TryGetValues("Deprecation", out _);
        if (codes.Count == 0 && !flagged)
        {
            return;
        }

        string? link = null;
        if (response.Headers.TryGetValues("Link", out var links))
        {
            link = links.SelectMany(l => l.Split(','))
                .Where(l => l.Contains("rel=\"deprecation\"", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Split(';')[0].Trim().TrimStart('<').TrimEnd('>'))
                .FirstOrDefault();
        }

        callback(new Tone3000DeprecationNotice(response.RequestMessage?.RequestUri, codes, link));
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false)
                    ?? throw new Tone3000Exception($"TONE3000 returned an empty {typeof(T).Name}.");
            }
        }
        catch (JsonException ex)
        {
            throw new Tone3000Exception($"Could not read the TONE3000 response as {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}

/// <summary>What <see cref="Tone3000Client.Create"/> returns: the API client plus the OAuth and token pieces it shares.</summary>
public sealed class Tone3000ClientBundle : IDisposable
{
    private readonly IDisposable[] _owned;

    internal Tone3000ClientBundle(Tone3000Client client, Tone3000OAuthClient oauth, Tone3000TokenManager tokens, IDisposable[] owned)
    {
        Client = client;
        OAuth = oauth;
        Tokens = tokens;
        _owned = owned;
    }

    /// <summary>The API client.</summary>
    public Tone3000Client Client { get; }

    /// <summary>Sign-in helpers.</summary>
    public Tone3000OAuthClient OAuth { get; }

    /// <summary>Token storage and refresh.</summary>
    public Tone3000TokenManager Tokens { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var d in _owned)
        {
            d.Dispose();
        }
    }
}
