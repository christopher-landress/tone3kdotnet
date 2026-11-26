using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tone3000;

/// <summary>
/// Computes the <c>content_hash</c> values that <see cref="ITone3000Client.LookupModelsAsync"/> takes,
/// so an app can find the TONE3000 page for a file it already has.
/// </summary>
public static class ContentHash
{
    /// <summary>Prefix of IR content hashes.</summary>
    public const string IrPrefix = "ir-content-v1:";

    /// <summary>Prefix of NAM content hashes.</summary>
    public const string NamPrefix = "nam-content-v1:";

    /// <summary>
    /// Hash of an impulse response WAV: <c>ir-content-v1:</c> + hex SHA-256 of <c>"ir-content-v1\0"</c> followed by the bytes of the WAV <c>data</c> chunk.
    /// </summary>
    /// <exception cref="InvalidDataException">The stream is not a RIFF/WAVE file with a data chunk.</exception>
    public static async Task<string> ForImpulseResponseAsync(Stream wav, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wav);
        var header = new byte[12];
        await wav.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (!header.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !header.AsSpan(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        var chunk = new byte[8];
        while (true)
        {
            try
            {
                await wav.ReadExactlyAsync(chunk, cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("The WAV file has no data chunk.");
            }

            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4));
            if (chunk.AsSpan(0, 4).SequenceEqual("data"u8))
            {
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                sha.AppendData("ir-content-v1\0"u8);
                var buffer = new byte[81920];
                long remaining = size;
                while (remaining > 0)
                {
                    var read = await wav.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new InvalidDataException("The WAV data chunk is truncated.");
                    }

                    sha.AppendData(buffer, 0, read);
                    remaining -= read;
                }

                return IrPrefix + Convert.ToHexStringLower(sha.GetHashAndReset());
            }

            // Chunks are padded to an even length.
            await SkipAsync(wav, size + (size & 1), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the hash of a <c>.nam</c> file, which the API docs say to send as stored in <c>metadata.weights_hash</c>.
    /// Returns null when the file has none.
    /// </summary>
    public static async Task<string?> ForNamAsync(Stream namJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(namJson);
        using var doc = await JsonDocument.ParseAsync(namJson, cancellationToken: cancellationToken).ConfigureAwait(false);
        return doc.RootElement.TryGetProperty("metadata", out var meta)
               && meta.ValueKind == JsonValueKind.Object
               && meta.TryGetProperty("weights_hash", out var hash)
               && hash.ValueKind == JsonValueKind.String
            ? hash.GetString()
            : null;
    }

    private static async Task SkipAsync(Stream stream, long count, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(count, SeekOrigin.Current);
            return;
        }

        var buffer = new byte[Math.Min(count, 81920)];
        while (count > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, count)), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("The WAV file ended inside a chunk.");
            }

            count -= read;
        }
    }

}
