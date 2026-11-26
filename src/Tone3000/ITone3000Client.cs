using Tone3000.Models;

namespace Tone3000;

/// <summary>Typed access to the TONE3000 v1 public API. Every call acts as the signed-in user.</summary>
public interface ITone3000Client
{
    /// <summary><c>GET /user</c>: the signed-in user.</summary>
    Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /users</c>: browse creators.</summary>
    Task<PagedResult<PublicUser>> ListUsersAsync(UserListQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tones/created</c>: tones the signed-in user created.</summary>
    Task<PagedResult<Tone>> ListCreatedTonesAsync(ToneListQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tones/favorited</c>: tones the signed-in user favorited.</summary>
    Task<PagedResult<Tone>> ListFavoritedTonesAsync(ToneListQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tones/downloaded</c>: tones the signed-in user downloaded (duplicates collapsed).</summary>
    Task<PagedResult<Tone>> ListDownloadedTonesAsync(ToneListQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tones/{id}</c>. <paramref name="architecture"/> only affects <see cref="Tone.ModelsCount"/>.</summary>
    Task<Tone> GetToneAsync(long toneId, Architecture? architecture = null, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /tones/{id}/favorite</c>. Idempotent.</summary>
    Task<Favorite> FavoriteToneAsync(long toneId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /tones/{id}/favorite</c>. Idempotent.</summary>
    Task UnfavoriteToneAsync(long toneId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tones/search</c>. Heavily rate-limited; prefer the Select sign-in flow for browsing.</summary>
    Task<PagedResult<Tone>> SearchTonesAsync(ToneSearchQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tones/trending</c>: up to 10 tones.</summary>
    Task<IReadOnlyList<Tone>> GetTrendingTonesAsync(Gear? gear = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tones/latest</c>: up to 10 tones.</summary>
    Task<IReadOnlyList<Tone>> GetLatestTonesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /models/{id}</c>.</summary>
    Task<Model> GetModelAsync(long modelId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /models</c>: one tone's models for one architecture.</summary>
    Task<PagedResult<Model>> ListModelsAsync(ModelListQuery query, CancellationToken cancellationToken = default);

    /// <summary><c>GET /models/lookup</c>: models with a given content hash, oldest first. No match returns an empty page.</summary>
    Task<PagedResult<Model>> LookupModelsAsync(string contentHash, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /makes</c>.</summary>
    Task<PagedResult<PublicMake>> ListMakesAsync(TaxonomyQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /tags</c>.</summary>
    Task<PagedResult<PublicTag>> ListTagsAsync(TaxonomyQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Streams a model file from <see cref="Model.ModelUrl"/>.</summary>
    Task<ModelDownload> DownloadModelAsync(Model model, CancellationToken cancellationToken = default);

    /// <summary>Streams a model file from a <c>model_url</c>.</summary>
    Task<ModelDownload> DownloadModelAsync(Uri modelUrl, CancellationToken cancellationToken = default);
}
