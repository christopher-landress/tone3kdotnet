namespace Tone3000;

/// <summary>A model file being streamed from TONE3000. Dispose it when done.</summary>
public sealed class ModelDownload : IDisposable, IAsyncDisposable
{
    private readonly HttpResponseMessage _response;

    internal ModelDownload(HttpResponseMessage response, Stream content)
    {
        _response = response;
        Content = content;
    }

    /// <summary>The file bytes. Read it once; it is not buffered.</summary>
    public Stream Content { get; }

    /// <summary>File length, when the server sent it.</summary>
    public long? ContentLength => _response.Content.Headers.ContentLength;

    /// <summary>Content type, when the server sent it.</summary>
    public string? ContentType => _response.Content.Headers.ContentType?.MediaType;

    /// <summary>File name from <c>Content-Disposition</c>, when the server sent it.</summary>
    public string? FileName => _response.Content.Headers.ContentDisposition?.FileNameStar?.Trim('"')
                               ?? _response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

    /// <summary>Copies the file to <paramref name="destination"/>.</summary>
    public Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default)
        => Content.CopyToAsync(destination, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        Content.Dispose();
        _response.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
    }
}
