using System.Text.Json;

namespace Tone3000.Models;

/// <summary>A downloadable model file (NAM capture, IR, ...) belonging to a tone.</summary>
public sealed record Model
{
    /// <summary>Model id.</summary>
    public long Id { get; init; }
    /// <summary>Owning tone id.</summary>
    public long ToneId { get; init; }
    /// <summary>Creator's user id.</summary>
    public long UserId { get; init; }
    /// <summary>When the model was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>When the model was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
    /// <summary>File URL. Download it with <see cref="ITone3000Client.DownloadModelAsync(Model, CancellationToken)"/>, which sends the bearer token.</summary>
    public string ModelUrl { get; init; } = "";
    /// <summary>Model name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Model size.</summary>
    public ModelSize Size { get; init; }
    /// <summary>NAM architecture; null for IRs.</summary>
    public Architecture? ArchitectureVersion { get; init; }
    /// <summary>Content hash, <c>nam-content-v1:</c> or <c>ir-content-v1:</c> followed by 64 hex characters.</summary>
    public string? ContentHash { get; init; }
    /// <summary>Free-form metadata. Values are strings, numbers, booleans or null.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}
