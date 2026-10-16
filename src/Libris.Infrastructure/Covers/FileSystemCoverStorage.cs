using Libris.Application.Abstractions.Covers;
using Microsoft.Extensions.Options;

namespace Libris.Infrastructure.Covers;

public sealed class FileSystemCoverStorage : ICoverStorage
{
    private readonly IOptions<CoverOptions> _options;

    public FileSystemCoverStorage(IOptions<CoverOptions> options)
    {
        _options = options;
    }

    public async Task SaveAsync(Guid coverId, Stream webp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(webp);

        Directory.CreateDirectory(_options.Value.Path);
        await using var file = File.Create(PathOf(coverId));
        await webp.CopyToAsync(file, ct);
    }

    public Stream? OpenRead(Guid coverId)
    {
        var path = PathOf(coverId);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(Guid coverId) => File.Delete(PathOf(coverId));

    private string PathOf(Guid coverId) => System.IO.Path.Combine(_options.Value.Path, coverId.ToString("N") + ".webp");
}
