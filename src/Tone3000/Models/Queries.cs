namespace Tone3000.Models;

/// <summary>Paging parameters shared by list endpoints.</summary>
public abstract record PagedQuery
{
    /// <summary>1-based page number. Null uses the server default (1).</summary>
    public int? Page { get; init; }
    /// <summary>Page size. Null uses the server default (10). Each endpoint has its own maximum.</summary>
    public int? PageSize { get; init; }
}

/// <summary>Filters for <c>GET /users</c>. Max page size 10.</summary>
public sealed record UserListQuery : PagedQuery
{
    /// <summary>Username substring.</summary>
    public string? Query { get; init; }
    /// <summary>Sort order.</summary>
    public UsersSort Sort { get; init; }
}

/// <summary>Filters for the signed-in user's created, favorited and downloaded tones. Max page size 100.</summary>
public sealed record ToneListQuery : PagedQuery
{
    /// <summary>Title substring.</summary>
    public string? Query { get; init; }
    /// <summary>Only tones of this gear type.</summary>
    public Gear? Gear { get; init; }
}

/// <summary>Filters for <c>GET /tones/search</c>. Max page size 25. This endpoint is heavily rate-limited.</summary>
public sealed record ToneSearchQuery : PagedQuery
{
    /// <summary>Free-text query.</summary>
    public string? Query { get; init; }
    /// <summary>Sort order. Default is best match with a query, otherwise trending.</summary>
    public TonesSort Sort { get; init; }
    /// <summary>Gear types to include.</summary>
    public IReadOnlyCollection<Gear>? Gears { get; init; }
    /// <summary>Model sizes to include.</summary>
    public IReadOnlyCollection<ModelSize>? Sizes { get; init; }
    /// <summary>Tag names (not ids).</summary>
    public IReadOnlyCollection<string>? Tags { get; init; }
    /// <summary>Make names (not ids).</summary>
    public IReadOnlyCollection<string>? Makes { get; init; }
    /// <summary>Creator usernames.</summary>
    public IReadOnlyCollection<string>? Creators { get; init; }
    /// <summary>Model file format.</summary>
    public ToneFormat? Format { get; init; }
    /// <summary>NAM architecture.</summary>
    public Architecture? Architecture { get; init; }
    /// <summary>Only tones with a calibrated model.</summary>
    public bool? Calibrated { get; init; }
    /// <summary>Only tones from verified creators.</summary>
    public bool? Verified { get; init; }
}

/// <summary>Filters for <c>GET /models</c>. Max page size 300.</summary>
/// <param name="ToneId">Tone whose models to list.</param>
public sealed record ModelListQuery(long ToneId) : PagedQuery
{
    /// <summary>
    /// Architecture to list. The endpoint returns one architecture per call. This library always sends it,
    /// because sources disagree on the server default.
    /// </summary>
    public Architecture Architecture { get; init; } = Architecture.A2;
}

/// <summary>Filters for <c>GET /makes</c> and <c>GET /tags</c>. Max page size 25.</summary>
public sealed record TaxonomyQuery : PagedQuery
{
    /// <summary>Name substring.</summary>
    public string? Query { get; init; }
    /// <summary>Sort order.</summary>
    public TaxonomySort Sort { get; init; }
}
