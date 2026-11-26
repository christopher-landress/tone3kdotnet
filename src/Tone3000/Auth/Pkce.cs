using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Tone3000.Auth;

/// <summary>A PKCE verifier, its S256 challenge and an anti-forgery state value for one sign-in attempt.</summary>
/// <param name="CodeVerifier">Keep this until the callback and pass it to the code exchange.</param>
/// <param name="CodeChallenge">base64url(SHA-256(verifier)), sent on the authorize URL.</param>
/// <param name="State">Random value; check that the callback returns the same one.</param>
public sealed record PkceSession(string CodeVerifier, string CodeChallenge, string State)
{
    /// <summary>Creates a fresh verifier (43 characters from 32 random bytes), challenge and state.</summary>
    public static PkceSession Create()
    {
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        return new PkceSession(verifier, ChallengeFor(verifier), state);
    }

    /// <summary>Computes the S256 challenge for a verifier.</summary>
    public static string ChallengeFor(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }
}
