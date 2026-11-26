using System.Runtime.CompilerServices;
using Tone3000.Models;

namespace Tone3000;

/// <summary>Helpers for walking paginated endpoints.</summary>
public static class Tone3000Paging
{
    /// <summary>
    /// Yields every item across pages by calling <paramref name="fetchPage"/> with page 1, 2, ... until
    /// <see cref="PagedResult{T}.HasNextPage"/> is false or <paramref name="maxPages"/> is reached.
    /// The API terms forbid bulk scraping or catalog caching, so keep walks bounded.
    /// </summary>
    public static async IAsyncEnumerable<T> EnumerateAsync<T>(
        Func<int, CancellationToken, Task<PagedResult<T>>> fetchPage,
        int maxPages = int.MaxValue,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fetchPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);

        for (var page = 1; page <= maxPages; page++)
        {
            var result = await fetchPage(page, cancellationToken).ConfigureAwait(false);
            foreach (var item in result.Data)
            {
                yield return item;
            }

            if (!result.HasNextPage || result.Data.Count == 0)
            {
                yield break;
            }
        }
    }

    /// <summary>Every model of a tone for one architecture.</summary>
    public static IAsyncEnumerable<Model> EnumerateModelsAsync(this ITone3000Client client, long toneId, Architecture architecture = Architecture.A2, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return EnumerateAsync(
            (page, ct) => client.ListModelsAsync(new ModelListQuery(toneId) { Architecture = architecture, Page = page, PageSize = pageSize }, ct),
            cancellationToken: cancellationToken);
    }
}
