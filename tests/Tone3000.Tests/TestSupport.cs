using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Tone3000.Auth;

namespace Tone3000.Tests;

/// <summary>Records requests and answers them from a delegate.</summary>
internal sealed class FakeHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
{
    private int _count;

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
        var response = respond(request, Interlocked.Increment(ref _count) - 1);
        response.RequestMessage = request;
        return response;
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body);

/// <summary>Clock whose timers fire at once and whose time moves forward by each delay.</summary>
internal sealed class InstantTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public ConcurrentQueue<TimeSpan> Delays { get; } = new();

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            Delays.Enqueue(dueTime);
            _now += dueTime;
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }

        return new NoopTimer();
    }

    private sealed class NoopTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal static class Fixtures
{
    public static readonly DateTimeOffset Now = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);

    public static Tone3000ClientOptions Options() => new()
    {
        ClientId = "t3k_pub_test",
        DefaultRetryAfter = TimeSpan.Zero,
    };

    public static Tone3000Tokens ValidTokens(string access = "access-1", string refresh = "refresh-1") => new()
    {
        AccessToken = access,
        RefreshToken = refresh,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        Scope = "read",
    };

    public const string ToneJson = """
    {
      "id": 42, "user_id": 7,
      "user": { "id": 7, "username": "fuzzface", "display_name": null, "is_verified": true, "avatar_url": null, "url": "https://www.tone3000.com/fuzzface" },
      "created_at": "2026-10-01T12:00:00.000Z", "updated_at": "2026-10-02T12:00:00Z", "published_at": null,
      "title": "Plexi 1968", "description": "Cranked", "gear": "amp-cab",
      "images": ["https://img/1.png"], "is_public": true, "links": null,
      "format": "nam", "license": "cco", "sizes": ["standard", "nano", "jumbo"],
      "makes": [{ "id": 1, "name": "Marshall" }], "tags": [{ "id": 3, "name": "crunch" }],
      "models_count": 4, "a1_models_count": 1, "a2_models_count": 3, "irs_count": 0, "custom_models_count": 0,
      "downloads_count": 100, "favorites_count": 5, "is_favorite": false,
      "url": "https://www.tone3000.com/tones/plexi-42",
      "brand_new_field": { "ignored": true }
    }
    """;

    public static string Page(string itemJson, int page = 1, int totalPages = 1)
        => $$"""{ "data": [{{itemJson}}], "page": {{page}}, "page_size": 10, "total": {{totalPages * 10}}, "total_pages": {{totalPages}} }""";

    public const string TokenJson = """{ "access_token": "access-2", "refresh_token": "refresh-2", "token_type": "bearer", "expires_in": 3600, "scope": "read" }""";
}
