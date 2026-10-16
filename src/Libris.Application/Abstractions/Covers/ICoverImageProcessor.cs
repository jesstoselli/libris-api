namespace Libris.Application.Abstractions.Covers;

public interface ICoverImageProcessor
{
    /// <returns>The cover re-encoded as WebP (resized, metadata stripped), or null when the bytes are not an image.</returns>
    Task<Stream?> ToWebpAsync(Stream image, CancellationToken ct);
}
