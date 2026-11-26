using System.Net;
using Tone3000.Auth;
using Tone3000.Models;

namespace Tone3000.Tests;

public class AuthTests
{
    [Fact]
    public void Pkce_challenge_is_unpadded_base64url_sha256()
        // Expected value computed independently with Python's hashlib + base64.urlsafe_b64encode.
        => Assert.Equal("yxIVJO2e491gwYmJMPnDWGY_8jD0XOPmmrmJwoX19JY", PkceSession.ChallengeFor("dBjftJeZ4CVP-mJ92K1oyW5rUxZD4ycGJqLDt7Q8cE"));

    [Fact]
    public void Pkce_session_is_fresh_and_valid()
    {
        var a = PkceSession.Create();
        var b = PkceSession.Create();
        Assert.NotEqual(a.CodeVerifier, b.CodeVerifier);
        Assert.Equal(43, a.CodeVerifier.Length);
        Assert.Equal(PkceSession.ChallengeFor(a.CodeVerifier), a.CodeChallenge);
    }

    [Fact]
    public void Authorize_uri_has_pkce_and_select_params()
    {
        var oauth = new Tone3000OAuthClient(new HttpClient(), Fixtures.Options());
        var pkce = new PkceSession("verifier", "challenge", "state123");

        var uri = oauth.BuildAuthorizationUri(new AuthorizationRequest(new Uri("myapp://callback"), pkce)
        {
            Prompt = AuthorizationPrompt.SelectTone,
            Gears = [Gear.Amp, Gear.Pedal],
            Format = ToneFormat.Nam,
            Architecture = Architecture.A2,
        });

        Assert.Equal("https://www.tone3000.com/api/v1/oauth/authorize", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal(
            "?client_id=t3k_pub_test&redirect_uri=myapp%3A%2F%2Fcallback&response_type=code&code_challenge=challenge" +
            "&code_challenge_method=S256&state=state123&prompt=select_tone&gears=amp_pedal&format=nam&architecture=2",
            uri.Query);
    }

    [Fact]
    public void Load_tone_requires_tone_id()
    {
        var oauth = new Tone3000OAuthClient(new HttpClient(), Fixtures.Options());
        Assert.Throws<ArgumentException>(() => oauth.BuildAuthorizationUri(
            new AuthorizationRequest(new Uri("myapp://cb"), PkceSession.Create()) { Prompt = AuthorizationPrompt.LoadTone }));
    }

    [Fact]
    public void Callback_parses_and_checks_state()
    {
        var pkce = new PkceSession("v", "c", "s1");
        var ok = AuthorizationCallback.Parse(new Uri("myapp://callback?code=abc&state=s1&tone_id=42"));
        Assert.Equal("abc", ok.GetCodeOrThrow(pkce));
        Assert.Equal(42, ok.ToneId);

        var forged = AuthorizationCallback.Parse(new Uri("myapp://callback?code=abc&state=other"));
        Assert.Equal("state_mismatch", Assert.Throws<Tone3000OAuthException>(() => forged.GetCodeOrThrow(pkce)).Error);

        var denied = AuthorizationCallback.Parse(new Uri("myapp://callback?error=access_denied&state=s1"));
        Assert.Equal("access_denied", Assert.Throws<Tone3000OAuthException>(() => denied.GetCodeOrThrow(pkce)).Error);

        Assert.True(AuthorizationCallback.Parse(new Uri("myapp://callback?canceled=true")).Canceled);
    }

    [Fact]
    public async Task Exchange_code_posts_form_and_returns_tokens()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json(Fixtures.TokenJson));
        var clock = new InstantTimeProvider(Fixtures.Now);
        var oauth = new Tone3000OAuthClient(new HttpClient(handler), Fixtures.Options(), clock);

        var tokens = await oauth.ExchangeCodeAsync("the-code", "the-verifier", new Uri("myapp://callback"));

        var req = Assert.Single(handler.Requests);
        Assert.Equal("https://www.tone3000.com/api/v1/oauth/token", req.Uri.ToString());
        Assert.Equal("grant_type=authorization_code&code=the-code&code_verifier=the-verifier&redirect_uri=myapp%3A%2F%2Fcallback&client_id=t3k_pub_test", req.Body);
        Assert.Equal("access-2", tokens.AccessToken);
        Assert.Equal(Fixtures.Now.AddHours(1), tokens.ExpiresAt);
    }

    [Fact]
    public async Task OAuth_error_is_typed()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json("""{ "error": "invalid_grant", "error_description": "Refresh token revoked" }""", HttpStatusCode.BadRequest));
        var oauth = new Tone3000OAuthClient(new HttpClient(handler), Fixtures.Options());

        var ex = await Assert.ThrowsAsync<Tone3000OAuthException>(() => oauth.RefreshAsync("old"));

        Assert.Equal("invalid_grant", ex.Error);
        Assert.Equal("Refresh token revoked", ex.ErrorDescription);
        Assert.True(ex.RequiresReauthorization);
    }

    [Fact]
    public async Task Device_flow_polls_through_pending_and_slow_down()
    {
        var handler = new FakeHandler((req, n) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("device_authorization", StringComparison.Ordinal))
            {
                return FakeHandler.Json("""
                    { "device_code": "dev", "user_code": "ABCD-EFGH", "verification_uri": "https://www.tone3000.com/activate",
                      "verification_uri_complete": "https://www.tone3000.com/activate?code=ABCD-EFGH", "expires_in": 600, "interval": 5 }
                    """);
            }

            return n switch
            {
                1 => FakeHandler.Json("""{ "error": "authorization_pending" }""", HttpStatusCode.BadRequest),
                2 => FakeHandler.Json("""{ "error": "slow_down" }""", HttpStatusCode.BadRequest),
                _ => FakeHandler.Json(Fixtures.TokenJson),
            };
        });
        var clock = new InstantTimeProvider(Fixtures.Now);
        var oauth = new Tone3000OAuthClient(new HttpClient(handler), Fixtures.Options(), clock);

        var device = await oauth.StartDeviceAuthorizationAsync();
        var tokens = await oauth.PollDeviceTokenAsync(device);

        Assert.Equal("ABCD-EFGH", device.UserCode);
        Assert.Equal("access-2", tokens.AccessToken);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], clock.Delays);
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", handler.Requests.Last().Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Device_flow_surfaces_access_denied()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json("""{ "error": "access_denied" }""", HttpStatusCode.BadRequest));
        var oauth = new Tone3000OAuthClient(new HttpClient(handler), Fixtures.Options(), new InstantTimeProvider(Fixtures.Now));

        var ex = await Assert.ThrowsAsync<Tone3000OAuthException>(() =>
            oauth.PollDeviceTokenAsync(new DeviceAuthorization { DeviceCode = "dev", Interval = 5, ExpiresIn = 600 }));
        Assert.Equal("access_denied", ex.Error);
    }

    [Fact]
    public async Task Expired_token_is_refreshed_before_the_call_and_rotation_is_saved()
    {
        var handler = new FakeHandler((req, _) => req.RequestUri!.AbsolutePath.EndsWith("oauth/token", StringComparison.Ordinal)
            ? FakeHandler.Json(Fixtures.TokenJson)
            : FakeHandler.Json(Fixtures.ToneJson));
        var store = new InMemoryTokenStore(Fixtures.ValidTokens() with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(10) });
        using var bundle = Tone3000Client.Create(Fixtures.Options(), store, handler);
        Tone3000Tokens? raised = null;
        bundle.Tokens.TokensRefreshed += (_, t) => raised = t;

        await bundle.Client.GetToneAsync(42);

        var requests = handler.Requests.ToArray();
        Assert.Contains("grant_type=refresh_token&refresh_token=refresh-1", requests[0].Body, StringComparison.Ordinal);
        Assert.Equal("Bearer access-2", requests[1].Authorization);
        Assert.Equal("refresh-2", (await store.GetTokensAsync())!.RefreshToken);
        Assert.Equal("access-2", raised!.AccessToken);
    }

    [Fact]
    public async Task Unauthorized_triggers_one_refresh_and_retry()
    {
        var handler = new FakeHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("oauth/token", StringComparison.Ordinal))
            {
                return FakeHandler.Json(Fixtures.TokenJson);
            }

            return req.Headers.Authorization!.Parameter == "access-1"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : FakeHandler.Json(Fixtures.ToneJson);
        });
        using var bundle = Tone3000Client.Create(Fixtures.Options(), new InMemoryTokenStore(Fixtures.ValidTokens()), handler);

        var tone = await bundle.Client.GetToneAsync(42);

        Assert.Equal(42, tone.Id);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Concurrent_calls_share_one_refresh()
    {
        var refreshes = 0;
        var handler = new FakeHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("oauth/token", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref refreshes);
                Thread.Sleep(50);
                return FakeHandler.Json(Fixtures.TokenJson);
            }

            return FakeHandler.Json(Fixtures.ToneJson);
        });
        var store = new InMemoryTokenStore(Fixtures.ValidTokens() with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) });
        using var bundle = Tone3000Client.Create(Fixtures.Options(), store, handler);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => bundle.Client.GetToneAsync(42)));

        Assert.Equal(1, refreshes);
    }

    [Fact]
    public async Task Rejected_refresh_clears_tokens_and_asks_for_sign_in()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json("""{ "error": "invalid_grant" }""", HttpStatusCode.BadRequest));
        var store = new InMemoryTokenStore(Fixtures.ValidTokens() with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) });
        using var bundle = Tone3000Client.Create(Fixtures.Options(), store, handler);

        await Assert.ThrowsAsync<Tone3000AuthenticationException>(() => bundle.Client.GetCurrentUserAsync());
        Assert.Null(await store.GetTokensAsync());
    }

    [Fact]
    public async Task Missing_tokens_throw_authentication_exception()
    {
        using var bundle = Tone3000Client.Create(Fixtures.Options(), new InMemoryTokenStore(), new FakeHandler((_, _) => FakeHandler.Json("{}")));
        await Assert.ThrowsAsync<Tone3000AuthenticationException>(() => bundle.Client.GetCurrentUserAsync());
    }
}
