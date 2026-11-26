using System.Net;
using System.Net.Http.Headers;
using Tone3000.Auth;
using Tone3000.Models;

namespace Tone3000.Tests;

public class ClientTests
{
    private static (Tone3000ClientBundle Bundle, FakeHandler Handler, InMemoryTokenStore Store) Build(
        Func<HttpRequestMessage, int, HttpResponseMessage> respond, Tone3000ClientOptions? options = null, Tone3000Tokens? tokens = null)
    {
        var handler = new FakeHandler(respond);
        var store = new InMemoryTokenStore(tokens ?? Fixtures.ValidTokens());
        return (Tone3000Client.Create(options ?? Fixtures.Options(), store, handler), handler, store);
    }

    [Fact]
    public async Task Get_tone_sends_bearer_and_parses()
    {
        var (bundle, handler, _) = Build((_, _) => FakeHandler.Json(Fixtures.ToneJson));
        using var _b = bundle;

        var tone = await bundle.Client.GetToneAsync(42, Architecture.A1);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://www.tone3000.com/api/v1/tones/42?architecture=1", request.Uri.ToString());
        Assert.Equal("Bearer access-1", request.Authorization);
        Assert.Equal("Plexi 1968", tone.Title);
    }

    [Fact]
    public async Task Search_builds_documented_query()
    {
        var (bundle, handler, _) = Build((_, _) => FakeHandler.Json(Fixtures.Page(Fixtures.ToneJson)));
        using var _b = bundle;

        var page = await bundle.Client.SearchTonesAsync(new ToneSearchQuery
        {
            Query = "plexi",
            Sort = TonesSort.Newest,
            Gears = [Gear.Amp, Gear.AmpCab],
            Sizes = [ModelSize.Standard, ModelSize.Lite],
            Makes = ["Fender Twin Reverb", "Vox AC30"],
            Creators = ["alice", "bob"],
            Format = ToneFormat.Nam,
            Architecture = Architecture.A2,
            Calibrated = true,
            Page = 2,
            PageSize = 25,
        });

        var uri = Assert.Single(handler.Requests).Uri;
        Assert.Equal("/api/v1/tones/search", uri.AbsolutePath);
        Assert.Equal(
            "?query=plexi&sort=newest&gears=amp_amp-cab&sizes=standard-lite&makes=Fender%20Twin%20Reverb_Vox%20AC30" +
            "&creators=alice%2Cbob&format=nam&architecture=2&calibrated=true&page=2&page_size=25",
            uri.Query);
        Assert.Single(page.Data);
    }

    [Fact]
    public async Task Search_rejects_values_containing_the_separator()
    {
        var (bundle, _, _) = Build((_, _) => FakeHandler.Json("{}"));
        using var _b = bundle;
        await Assert.ThrowsAsync<ArgumentException>(() => bundle.Client.SearchTonesAsync(new ToneSearchQuery { Tags = ["bad_tag"] }));
    }

    [Fact]
    public async Task List_models_always_sends_architecture()
    {
        var (bundle, handler, _) = Build((_, _) => FakeHandler.Json("""{ "data": [], "page": 1, "page_size": 10, "total": 0, "total_pages": 0 }"""));
        using var _b = bundle;

        await bundle.Client.ListModelsAsync(new ModelListQuery(42));

        Assert.Equal("?tone_id=42&architecture=2", Assert.Single(handler.Requests).Uri.Query);
    }

    [Fact]
    public async Task Trending_and_latest_unwrap_data()
    {
        var (bundle, handler, _) = Build((_, _) => FakeHandler.Json($$"""{ "data": [{{Fixtures.ToneJson}}] }"""));
        using var _b = bundle;

        var trending = await bundle.Client.GetTrendingTonesAsync(Gear.Pedal);
        var latest = await bundle.Client.GetLatestTonesAsync();

        Assert.Single(trending);
        Assert.Single(latest);
        Assert.Equal("?gear=pedal", handler.Requests.First().Uri.Query);
    }

    [Fact]
    public async Task Favorite_and_unfavorite_use_put_and_delete()
    {
        var (bundle, handler, _) = Build((req, _) => req.Method == HttpMethod.Put
            ? FakeHandler.Json("""{ "id": 1, "tone_id": 42, "user_id": "7", "created_at": "2026-11-01T00:00:00Z" }""")
            : new HttpResponseMessage(HttpStatusCode.NoContent));
        using var _b = bundle;

        var fav = await bundle.Client.FavoriteToneAsync(42);
        await bundle.Client.UnfavoriteToneAsync(42);

        Assert.Equal(42, fav.ToneId);
        Assert.Equal([HttpMethod.Put, HttpMethod.Delete], handler.Requests.Select(r => r.Method));
        Assert.All(handler.Requests, r => Assert.EndsWith("/tones/42/favorite", r.Uri.AbsolutePath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Not_found_throws_api_exception_with_body()
    {
        var (bundle, _, _) = Build((_, _) => FakeHandler.Json("""{ "error": "Not found" }""", HttpStatusCode.NotFound));
        using var _b = bundle;

        var ex = await Assert.ThrowsAsync<Tone3000ApiException>(() => bundle.Client.GetModelAsync(1));

        Assert.True(ex.IsNotFound);
        Assert.Contains("Not found", ex.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rate_limit_is_retried_after_retry_after()
    {
        var (bundle, handler, _) = Build((_, n) =>
        {
            if (n == 0)
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return r;
            }

            return FakeHandler.Json(Fixtures.ToneJson);
        });
        using var _b = bundle;

        var tone = await bundle.Client.GetToneAsync(42);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(42, tone.Id);
    }

    [Fact]
    public async Task Search_is_not_retried_on_rate_limit_by_default()
    {
        var (bundle, handler, _) = Build((_, _) =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
            return r;
        });
        using var _b = bundle;

        var ex = await Assert.ThrowsAsync<Tone3000RateLimitException>(() => bundle.Client.SearchTonesAsync(new ToneSearchQuery { Query = "x" }));

        Assert.Single(handler.Requests);
        Assert.Equal(TimeSpan.FromSeconds(12), ex.RetryAfter);
    }

    [Fact]
    public async Task Rate_limit_longer_than_max_wait_throws_at_once()
    {
        var options = Fixtures.Options();
        options.MaxRetryAfter = TimeSpan.FromSeconds(1);
        var (bundle, handler, _) = Build((_, _) =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return r;
        }, options);
        using var _b = bundle;

        await Assert.ThrowsAsync<Tone3000RateLimitException>(() => bundle.Client.GetLatestTonesAsync());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Deprecation_headers_reach_the_callback()
    {
        Tone3000DeprecationNotice? notice = null;
        var options = Fixtures.Options();
        options.OnDeprecation = n => notice = n;
        var (bundle, _, _) = Build((_, _) =>
        {
            var r = FakeHandler.Json(Fixtures.ToneJson);
            r.Headers.TryAddWithoutValidation("Deprecation", "true");
            r.Headers.TryAddWithoutValidation("X-Tone3000-Deprecations", "legacy_platform_key, legacy_ir_gear_value");
            r.Headers.TryAddWithoutValidation("Link", "<https://www.tone3000.com/api#deprecations>; rel=\"deprecation\"");
            return r;
        }, options);
        using var _b = bundle;

        await bundle.Client.GetToneAsync(42);

        Assert.NotNull(notice);
        Assert.Equal(["legacy_platform_key", "legacy_ir_gear_value"], notice.Deprecations);
        Assert.Equal("https://www.tone3000.com/api#deprecations", notice.Link);
    }

    [Fact]
    public async Task Download_streams_and_sends_token_only_to_tone3000()
    {
        var (bundle, handler, _) = Build((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var _b = bundle;

        await using (var own = await bundle.Client.DownloadModelAsync(new Uri("https://www.tone3000.com/api/v1/models/9/file")))
        {
            using var ms = new MemoryStream();
            await own.CopyToAsync(ms);
            Assert.Equal([1, 2, 3], ms.ToArray());
        }

        await using (await bundle.Client.DownloadModelAsync(new Uri("https://cdn.example.com/m.nam")))
        {
        }

        var requests = handler.Requests.ToArray();
        Assert.Equal("Bearer access-1", requests[0].Authorization);
        Assert.Null(requests[1].Authorization);
    }

    [Fact]
    public async Task Paging_walks_until_last_page()
    {
        var pages = 0;
        var items = new List<int>();
        await foreach (var i in Tone3000Paging.EnumerateAsync<int>((page, _) =>
        {
            pages++;
            return Task.FromResult(new PagedResult<int> { Data = [page * 10, page * 10 + 1], Page = page, TotalPages = 3 });
        }))
        {
            items.Add(i);
        }

        Assert.Equal(3, pages);
        Assert.Equal([10, 11, 20, 21, 30, 31], items);
    }

    [Fact]
    public async Task Cancellation_is_honored()
    {
        var (bundle, _, _) = Build((_, _) => FakeHandler.Json(Fixtures.ToneJson));
        using var _b = bundle;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bundle.Client.GetToneAsync(42, cancellationToken: cts.Token));
    }
}
