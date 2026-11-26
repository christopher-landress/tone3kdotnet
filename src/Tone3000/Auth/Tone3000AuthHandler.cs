using System.Net;
using System.Net.Http.Headers;

namespace Tone3000.Auth;

/// <summary>
/// Adds <c>Authorization: Bearer</c> to each request and, on a 401, refreshes once and retries.
/// </summary>
public sealed class Tone3000AuthHandler(Tone3000TokenManager tokens) : DelegatingHandler
{
    private readonly Tone3000TokenManager _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));

    /// <summary>Set on a request to send it without the bearer token.</summary>
    internal static readonly HttpRequestOptionsKey<bool> SkipAuth = new("Tone3000.SkipAuth");

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(SkipAuth, out var skip) && skip)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var token = await _tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Requests from this library carry no body, so resending the same message is safe.
        if (response.StatusCode != HttpStatusCode.Unauthorized || request.Content is not null)
        {
            return response;
        }

        Tone3000Tokens refreshed;
        try
        {
            refreshed = await _tokens.RefreshAsync(token, cancellationToken).ConfigureAwait(false);
        }
        catch (Tone3000AuthenticationException)
        {
            return response;
        }

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
