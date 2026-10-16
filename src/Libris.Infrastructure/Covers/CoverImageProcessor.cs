using Libris.Application.Abstractions.Covers;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Libris.Infrastructure.Covers;

public sealed class CoverImageProcessor : ICoverImageProcessor
{
    private readonly IOptions<CoverOptions> _options;

    public CoverImageProcessor(IOptions<CoverOptions> options)
    {
        _options = options;
    }

    public async Task<Stream?> ToWebpAsync(Stream image, CancellationToken ct)
    {
        Image loaded;
        try
        {
            loaded = await Image.LoadAsync(image, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            return null;
        }

        using (loaded)
        {
            var maxHeight = _options.Value.MaxHeight;
            if (loaded.Height > maxHeight)
            {
                loaded.Mutate(x => x.Resize(0, maxHeight));
            }

            loaded.Metadata.ExifProfile = null;
            loaded.Metadata.XmpProfile = null;
            loaded.Metadata.IptcProfile = null;

            var output = new MemoryStream();
            await loaded.SaveAsWebpAsync(output, ct);
            output.Position = 0;
            return output;
        }
    }
}
