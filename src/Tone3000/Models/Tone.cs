namespace Tone3000.Models;

/// <summary>A tone: one captured piece of gear, with one or more downloadable models.</summary>
public sealed record Tone
{
    /// <summary>Tone id.</summary>
    public long Id { get; init; }
    /// <summary>Creator's user id.</summary>
    public long UserId { get; init; }
    /// <summary>Creator summary. Show it for attribution.</summary>
    public EmbeddedUser User { get; init; } = new();
    /// <summary>When the tone was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>When the tone was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
    /// <summary>When the tone was published, or null if never published.</summary>
    public DateTimeOffset? PublishedAt { get; init; }
    /// <summary>Title.</summary>
    public string Title { get; init; } = "";
    /// <summary>Description.</summary>
    public string? Description { get; init; }
    /// <summary>Kind of gear captured.</summary>
    public Gear Gear { get; init; }
    /// <summary>Image URLs.</summary>
    public IReadOnlyList<string>? Images { get; init; }
    /// <summary>Whether the tone is public.</summary>
    public bool? IsPublic { get; init; }
    /// <summary>Related links.</summary>
    public IReadOnlyList<string>? Links { get; init; }
    /// <summary>Model file format.</summary>
    public ToneFormat Format { get; init; }
    /// <summary>License the tone is published under. Show it alongside the tone.</summary>
    public License License { get; init; }
    /// <summary>Model sizes available.</summary>
    public IReadOnlyList<ModelSize> Sizes { get; init; } = [];
    /// <summary>Gear makes.</summary>
    public IReadOnlyList<Make> Makes { get; init; } = [];
    /// <summary>Tags.</summary>
    public IReadOnlyList<Tag> Tags { get; init; } = [];
    /// <summary>Total models across all architectures.</summary>
    public int ModelsCount { get; init; }
    /// <summary>Architecture 1 models.</summary>
    public int A1ModelsCount { get; init; }
    /// <summary>Architecture 2 models.</summary>
    public int A2ModelsCount { get; init; }
    /// <summary>Impulse responses.</summary>
    public int IrsCount { get; init; }
    /// <summary>Custom-architecture models.</summary>
    public int CustomModelsCount { get; init; }
    /// <summary>Download count.</summary>
    public int DownloadsCount { get; init; }
    /// <summary>Favorite count.</summary>
    public int FavoritesCount { get; init; }
    /// <summary>Whether the signed-in user has favorited this tone.</summary>
    public bool IsFavorite { get; init; }
    /// <summary>Web page URL.</summary>
    public string Url { get; init; } = "";
}

/// <summary>A gear make attached to a tone.</summary>
public sealed record Make(long Id, string Name);

/// <summary>A tag attached to a tone.</summary>
public sealed record Tag(long Id, string Name);

/// <summary>A make as listed by <c>GET /makes</c>.</summary>
public sealed record PublicMake(long Id, string Name, int TonesCount, string Url);

/// <summary>A tag as listed by <c>GET /tags</c>.</summary>
public sealed record PublicTag(long Id, string Name, int TonesCount, string Url);

/// <summary>Result of favoriting a tone.</summary>
public sealed record Favorite
{
    /// <summary>Favorite id.</summary>
    public long Id { get; init; }
    /// <summary>Tone id.</summary>
    public long ToneId { get; init; }
    /// <summary>User id. Documented as a string on this type, unlike elsewhere.</summary>
    public string UserId { get; init; } = "";
    /// <summary>When the favorite was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
