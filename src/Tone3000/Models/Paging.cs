namespace Tone3000.Models;

/// <summary>One page of a paginated list.</summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>Items on this page.</summary>
    public IReadOnlyList<T> Data { get; init; } = [];
    /// <summary>1-based page number.</summary>
    public int Page { get; init; }
    /// <summary>Page size used.</summary>
    public int PageSize { get; init; }
    /// <summary>Total items across all pages.</summary>
    public int Total { get; init; }
    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; init; }

    /// <summary>True while another page follows this one.</summary>
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>Body of the unpaginated list endpoints (<c>/tones/trending</c>, <c>/tones/latest</c>).</summary>
internal sealed record DataList<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];
}
