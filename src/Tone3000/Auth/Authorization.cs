using Tone3000.Models;

namespace Tone3000.Auth;

/// <summary>What the TONE3000 authorize page shows.</summary>
public enum AuthorizationPrompt
{
    /// <summary>Plain sign-in.</summary>
    SignIn = 0,
    /// <summary>The user signs in and picks a tone (<c>prompt=select_tone</c>). The callback carries <c>tone_id</c>.</summary>
    SelectTone,
    /// <summary>The user signs in and loads a given tone (<c>prompt=load_tone</c>).</summary>
    LoadTone,
}

/// <summary>Inputs for <see cref="Tone3000OAuthClient.BuildAuthorizationUri"/>.</summary>
/// <param name="RedirectUri">Where TONE3000 sends the user back, for example <c>yourapp://callback</c>.</param>
/// <param name="Pkce">The PKCE session for this attempt.</param>
public sealed record AuthorizationRequest(Uri RedirectUri, PkceSession Pkce)
{
    /// <summary>Which page to show.</summary>
    public AuthorizationPrompt Prompt { get; init; }
    /// <summary>Tone to load; required with <see cref="AuthorizationPrompt.LoadTone"/>.</summary>
    public long? ToneId { get; init; }
    /// <summary>Pre-fills the email field.</summary>
    public string? LoginHint { get; init; }
    /// <summary>UI locale, for example <c>zh-CN</c>.</summary>
    public string? Locale { get; init; }
    /// <summary>Select/load only: gear types to offer.</summary>
    public IReadOnlyCollection<Gear>? Gears { get; init; }
    /// <summary>Select/load only: model format to offer.</summary>
    public ToneFormat? Format { get; init; }
    /// <summary>Select/load only: architecture (server default 2).</summary>
    public Architecture? Architecture { get; init; }
    /// <summary>Select/load only: only tones with a calibrated model.</summary>
    public bool? Calibrated { get; init; }
    /// <summary>Show the navigation bar.</summary>
    public bool? Menubar { get; init; }
    /// <summary>Select only: show audition players.</summary>
    public bool? Preview { get; init; }
}

/// <summary>The query string TONE3000 sends to your redirect URI.</summary>
public sealed record AuthorizationCallback
{
    /// <summary>Authorization code to exchange.</summary>
    public string? Code { get; init; }
    /// <summary>State echoed back.</summary>
    public string? State { get; init; }
    /// <summary>Tone picked or loaded (select/load prompts). May differ from the tone requested.</summary>
    public long? ToneId { get; init; }
    /// <summary>Error code, for example <c>access_denied</c>.</summary>
    public string? Error { get; init; }
    /// <summary>True when the user closed the page from the menu bar.</summary>
    public bool Canceled { get; init; }

    /// <summary>Parses a callback URI.</summary>
    public static AuthorizationCallback Parse(Uri callbackUri)
    {
        ArgumentNullException.ThrowIfNull(callbackUri);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var query = callbackUri.IsAbsoluteUri ? callbackUri.Query : callbackUri.OriginalString[(callbackUri.OriginalString.IndexOf('?', StringComparison.Ordinal) + 1)..];
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            var key = Uri.UnescapeDataString((eq < 0 ? part : part[..eq]).Replace('+', ' '));
            var val = eq < 0 ? "" : Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
            values.TryAdd(key, val);
        }

        return new AuthorizationCallback
        {
            Code = values.GetValueOrDefault("code"),
            State = values.GetValueOrDefault("state"),
            ToneId = long.TryParse(values.GetValueOrDefault("tone_id"), System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : null,
            Error = values.GetValueOrDefault("error"),
            Canceled = string.Equals(values.GetValueOrDefault("canceled"), "true", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Checks the callback against the session that started it and returns the code.
    /// Throws <see cref="Tone3000OAuthException"/> on an error, cancel or state mismatch.
    /// </summary>
    public string GetCodeOrThrow(PkceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Error is not null)
        {
            throw new Tone3000OAuthException(System.Net.HttpStatusCode.BadRequest, Error, null);
        }

        if (Canceled)
        {
            throw new Tone3000OAuthException(System.Net.HttpStatusCode.BadRequest, OAuthErrors.AccessDenied, "The user closed the sign-in page.");
        }

        if (!string.Equals(State, session.State, StringComparison.Ordinal))
        {
            throw new Tone3000OAuthException(System.Net.HttpStatusCode.BadRequest, "state_mismatch", "The callback state does not match this sign-in attempt.");
        }

        return string.IsNullOrEmpty(Code)
            ? throw new Tone3000OAuthException(System.Net.HttpStatusCode.BadRequest, OAuthErrors.InvalidRequest, "The callback has no code.")
            : Code;
    }
}

/// <summary>Response of <c>POST /oauth/device_authorization</c>. Show <see cref="UserCode"/> and <see cref="VerificationUri"/> to the user.</summary>
public sealed record DeviceAuthorization
{
    /// <summary>Code the device polls with. Keep it private.</summary>
    public string DeviceCode { get; init; } = "";
    /// <summary>Code the user types, formatted <c>XXXX-XXXX</c>.</summary>
    public string UserCode { get; init; } = "";
    /// <summary>Page where the user enters the code.</summary>
    public string VerificationUri { get; init; } = "";
    /// <summary>Same page with the code pre-filled, suitable for a QR code.</summary>
    public string? VerificationUriComplete { get; init; }
    /// <summary>Seconds until the codes expire.</summary>
    public int ExpiresIn { get; init; }
    /// <summary>Seconds to wait between polls.</summary>
    public int Interval { get; init; } = 5;
}
