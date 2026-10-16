using Libris.Application.Abstractions.Covers;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Libris.Infrastructure.Covers;

public sealed class CoverImageProcessor : ICoverImageProcessor
{
    private const int WebpQuality = 85;

    private readonly IOptions<CoverOptions> _options;

    public CoverImageProcessor(IOptions<CoverOptions> options)
    {
        _options = options;
    }

    public async Task<Stream?> ToWebpAsync(Stream image, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);

        // SKBitmap.Decode needs a seekable stream; uploads arrive as forward-only request bodies.
        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        // Decoding to a bitmap and re-encoding drops EXIF/XMP metadata as a side effect.
        using var decoded = SKBitmap.Decode(buffer);
        if (decoded is null)
        {
            return null;
        }

        var maxHeight = _options.Value.MaxHeight;
        using var resized = decoded.Height > maxHeight
            ? decoded.Resize(
                new SKImageInfo((int)Math.Round(decoded.Width * (double)maxHeight / decoded.Height), maxHeight),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            : null;

        using var output = SKImage.FromBitmap(resized ?? decoded);
        using var webp = output.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        return new MemoryStream(webp.ToArray());
    }
}
