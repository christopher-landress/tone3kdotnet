namespace Tone3000.Models;

/// <summary>The creator summary embedded in a <see cref="Tone"/>.</summary>
public sealed record EmbeddedUser
{
    /// <summary>User id.</summary>
    public long Id { get; init; }
    /// <summary>Username.</summary>
    public string Username { get; init; } = "";
    /// <summary>Display name; set only for verified creators.</summary>
    public string? DisplayName { get; init; }
    /// <summary>Whether the creator is verified.</summary>
    public bool IsVerified { get; init; }
    /// <summary>Avatar image URL.</summary>
    public string? AvatarUrl { get; init; }
    /// <summary>Profile page URL.</summary>
    public string Url { get; init; } = "";

    /// <summary>The name to show for attribution: the display name, falling back to the username.</summary>
    public string Name => string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName;
}

/// <summary>The signed-in user, from <c>GET /user</c>.</summary>
public sealed record User
{
    /// <summary>User id.</summary>
    public long Id { get; init; }
    /// <summary>Username.</summary>
    public string Username { get; init; } = "";
    /// <summary>Display name; set only for verified creators.</summary>
    public string? DisplayName { get; init; }
    /// <summary>Whether the user is a verified creator.</summary>
    public bool IsVerified { get; init; }
    /// <summary>Avatar image URL.</summary>
    public string? AvatarUrl { get; init; }
    /// <summary>Profile page URL.</summary>
    public string Url { get; init; } = "";
    /// <summary>Profile bio.</summary>
    public string? Bio { get; init; }
    /// <summary>Profile links.</summary>
    public IReadOnlyList<string>? Links { get; init; }
    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>When the account was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>A user as listed by <c>GET /users</c>.</summary>
public sealed record PublicUser
{
    /// <summary>User id.</summary>
    public long Id { get; init; }
    /// <summary>Username.</summary>
    public string Username { get; init; } = "";
    /// <summary>Display name; set only for verified creators.</summary>
    public string? DisplayName { get; init; }
    /// <summary>Whether the user is a verified creator.</summary>
    public bool IsVerified { get; init; }
    /// <summary>Profile bio.</summary>
    public string? Bio { get; init; }
    /// <summary>Profile links.</summary>
    public IReadOnlyList<string>? Links { get; init; }
    /// <summary>Avatar image URL.</summary>
    public string? AvatarUrl { get; init; }
    /// <summary>Total downloads of the user's tones.</summary>
    public int DownloadsCount { get; init; }
    /// <summary>Total favorites of the user's tones.</summary>
    public int FavoritesCount { get; init; }
    /// <summary>Number of models the user has published.</summary>
    public int ModelsCount { get; init; }
    /// <summary>Number of tones the user has published.</summary>
    public int TonesCount { get; init; }
    /// <summary>Profile page URL.</summary>
    public string Url { get; init; } = "";
}
