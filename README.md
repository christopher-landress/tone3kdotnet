# Tone3000 .NET client

An unofficial, async C# client for the [TONE3000](https://www.tone3000.com) v1 public API, targeting .NET 10.

- Typed models for tones, models, users, makes, tags and favorites (System.Text.Json source generation, nullable annotations, trim/AOT friendly)
- OAuth 2.0 sign-in with PKCE (S256) and the device flow (RFC 8628), using only your publishable key
- Automatic token refresh before expiry and once on a 401, with refresh-token rotation handled safely
- Typed errors, optional 429 retry that honors `Retry-After`, deprecation-header callbacks
- `IHttpClientFactory` / dependency-injection registration, cancellation tokens everywhere
- Streaming model downloads and `content_hash` helpers for `/models/lookup`

Not covered: the partner-only `GET /tones/{id}/download` endpoint.

## Install

```sh
dotnet add package Tone3000   # once published; for now reference src/Tone3000
```

## Quick start (no DI)

```csharp
using Tone3000;
using Tone3000.Auth;
using Tone3000.Models;

var options = new Tone3000ClientOptions { ClientId = "t3k_pub_..." }; // publishable key only
using var tone3000 = Tone3000Client.Create(options, new InMemoryTokenStore());

// 1. Sign in (PKCE). Open the URL in a browser; TONE3000 redirects to your redirect URI.
var pkce = PkceSession.Create();
var redirect = new Uri("myapp://callback");
Uri signIn = tone3000.OAuth.BuildAuthorizationUri(new AuthorizationRequest(redirect, pkce));

// 2. In your redirect handler:
var callback = AuthorizationCallback.Parse(callbackUri);
var tokens = await tone3000.OAuth.ExchangeCodeAsync(callback.GetCodeOrThrow(pkce), pkce.CodeVerifier, redirect);
await tone3000.Tokens.SetTokensAsync(tokens);

// 3. Call the API.
var me = await tone3000.Client.GetCurrentUserAsync();
var results = await tone3000.Client.SearchTonesAsync(new ToneSearchQuery
{
    Query = "plexi",
    Gears = [Gear.Amp, Gear.AmpCab],
    Format = ToneFormat.Nam,
    Sort = TonesSort.DownloadsAllTime,
});

foreach (var tone in results.Data)
{
    Console.WriteLine($"{tone.Title} by {tone.User.Name} ({tone.License})");
}
```

## Dependency injection

```csharp
builder.Services.AddSingleton<ITokenStore, MySecureTokenStore>(); // optional; defaults to in-memory
builder.Services.AddTone3000Client(o => o.ClientId = builder.Configuration["Tone3000:ClientId"]);

// Inject ITone3000Client, Tone3000OAuthClient and Tone3000TokenManager where needed.
```

`AddTone3000Client` returns the `IHttpClientBuilder`, so you can add your own handlers or resilience policies.

## Device flow (CLIs, consoles, plugins without a browser)

```csharp
var device = await tone3000.OAuth.StartDeviceAuthorizationAsync();
Console.WriteLine($"Go to {device.VerificationUri} and enter {device.UserCode}");
var tokens = await tone3000.OAuth.PollDeviceTokenAsync(device, cancellationToken); // honors interval and slow_down
await tone3000.Tokens.SetTokensAsync(tokens);
```

## Select / load a tone through the TONE3000 UI

```csharp
var uri = tone3000.OAuth.BuildAuthorizationUri(new AuthorizationRequest(redirect, pkce)
{
    Prompt = AuthorizationPrompt.SelectTone,
    Gears = [Gear.Pedal],
    Format = ToneFormat.Nam,
});
// The callback's ToneId is the tone the user picked.
```

The API docs recommend this flow for browsing, since `/tones/search` is heavily rate-limited.

## Models and downloads

```csharp
await foreach (var model in tone3000.Client.EnumerateModelsAsync(toneId, Architecture.A2))
{
    await using var download = await tone3000.Client.DownloadModelAsync(model);
    await using var file = File.Create($"{model.Name}.nam");
    await download.CopyToAsync(file);
}

// Find the TONE3000 page for a file you already have:
await using var ir = File.OpenRead("cab.wav");
var hash = await ContentHash.ForImpulseResponseAsync(ir);
var matches = await tone3000.Client.LookupModelsAsync(hash);
```

`/models` returns one architecture per call. This library always sends `architecture` (default `A2`), because the docs and other clients disagree on the server default.

## Endpoints

| Method | API | Client method |
| --- | --- | --- |
| GET | `/user` | `GetCurrentUserAsync` |
| GET | `/users` | `ListUsersAsync` |
| GET | `/tones/created`, `/favorited`, `/downloaded` | `ListCreatedTonesAsync`, `ListFavoritedTonesAsync`, `ListDownloadedTonesAsync` |
| GET | `/tones/{id}` | `GetToneAsync` |
| PUT / DELETE | `/tones/{id}/favorite` | `FavoriteToneAsync`, `UnfavoriteToneAsync` |
| GET | `/tones/search` | `SearchTonesAsync` |
| GET | `/tones/trending`, `/tones/latest` | `GetTrendingTonesAsync`, `GetLatestTonesAsync` |
| GET | `/models/{id}`, `/models`, `/models/lookup` | `GetModelAsync`, `ListModelsAsync`, `LookupModelsAsync` |
| GET | `/makes`, `/tags` | `ListMakesAsync`, `ListTagsAsync` |
| GET | `model_url` | `DownloadModelAsync` |
| | `/oauth/authorize`, `/oauth/token`, `/oauth/device_authorization` | `Tone3000OAuthClient` |

## Errors

| Exception | When |
| --- | --- |
| `Tone3000ApiException` | Any non-success status. Has `StatusCode`, `ResponseBody`, `IsNotFound`, `IsForbidden`. |
| `Tone3000RateLimitException` | 429 after retries are used up. Has `RetryAfter`. |
| `Tone3000AuthenticationException` | Not signed in, or the token was rejected and could not be refreshed. Send the user through sign-in again. |
| `Tone3000OAuthException` | OAuth errors (`invalid_grant`, `access_denied`, ...). Has `Error`, `ErrorDescription`, `RequiresReauthorization`. |

## Tokens

Refresh tokens rotate: every refresh invalidates the previous one. Implement `ITokenStore` over secure storage
(Keychain, DPAPI, a database) and the client saves each new pair; refreshes are serialized so concurrent calls never
spend the same refresh token twice. `Tone3000TokenManager.TokensRefreshed` fires after each refresh.

## Options

| Option | Default | Notes |
| --- | --- | --- |
| `ClientId` | none | Publishable key (`t3k_pub_…`). Never ship a secret key in an app. |
| `BaseAddress` | `https://www.tone3000.com/api/v1/` | |
| `RefreshBeforeExpiry` | 60 s | |
| `MaxRateLimitRetries` | 2 | 0 turns retries off. |
| `MaxRetryAfter` | 30 s | Longer waits throw at once. |
| `RetrySearchOnRateLimit` | false | Search is not retried unless you opt in. |
| `OnDeprecation` | null | Receives `Deprecation` / `X-Tone3000-Deprecations` headers. |

## Using the API responsibly

The [API terms](https://www.tone3000.com/api/terms) forbid bulk downloading, scraping and caching the catalog, and
require you to keep creator attribution and license info and to show "Powered by TONE3000". `EmbeddedUser.Name` and
`Tone.License` are there for attribution. Commercial apps need an agreement with TONE3000.

## Assumptions to confirm against the live API

The public docs have no OpenAPI spec or raw JSON samples, so a few details are inferred:

- Timestamps are parsed as ISO 8601 `DateTimeOffset`.
- `sizes` on search is joined with hyphens (the docs show both hyphen and underscore).
- 401, 404 and 429 status codes and the `Retry-After` header are not documented; the client handles them in the usual way.
- `Favorite.user_id` is a string, as documented, unlike other ids.
- The bearer token is only sent to `model_url` hosts under `tone3000.com`.

## Build and test

```sh
dotnet test
```

## License

MIT. Not affiliated with TONE3000.
