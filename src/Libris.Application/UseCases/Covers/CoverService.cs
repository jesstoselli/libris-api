using Libris.Application.Abstractions.Covers;
using Libris.Application.Abstractions.Persistence;
using Libris.Application.Common;
using Libris.Application.Dtos.Covers;
using Libris.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Libris.Application.UseCases.Covers;

public class CoverService
{
    public const int MaxUploadBytes = 5 * 1024 * 1024;

    private readonly IApplicationDbContext _db;
    private readonly ICoverStorage _storage;
    private readonly ICoverImageProcessor _processor;
    private readonly ICoverSource _source;

    public CoverService(IApplicationDbContext db, ICoverStorage storage, ICoverImageProcessor processor, ICoverSource source)
    {
        _db = db;
        _storage = storage;
        _processor = processor;
        _source = source;
    }

    public async Task<OperationResult<CoverDto>> UploadAsync(Guid userId, Stream file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        // Read one byte past the limit so an oversized upload is detected without trusting Content-Length.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await file.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxUploadBytes)
            {
                return TooLarge();
            }
        }

        buffer.Position = 0;
        return await StoreAsync(userId, buffer, ct);
    }

    public async Task<OperationResult<CoverDto>> FromUrlAsync(Guid userId, CoverFromUrlRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var download = await _source.DownloadAsync(request.Url ?? string.Empty, ct);
        return download.Status switch
        {
            CoverDownloadStatus.Ok => await StoreAsync(userId, new MemoryStream(download.Bytes!), ct),
            CoverDownloadStatus.HostNotAllowed => OperationResult.Fail<CoverDto>(
                "host_not_allowed", "Só é possível baixar capas da Open Library ou do Google Books."),
            CoverDownloadStatus.TooLarge => TooLarge(),
            _ => OperationResult.Fail<CoverDto>("download_failed", "Não foi possível baixar a capa."),
        };
    }

    public Stream? OpenRead(Guid coverId) => _storage.OpenRead(coverId);

    public Task<bool> BelongsToAsync(Guid userId, Guid coverId, CancellationToken ct) =>
        _db.Covers.AnyAsync(c => c.Id == coverId && c.UserId == userId, ct);

    /// <summary>Removes the row and the file. Callers save the context.</summary>
    public async Task RemoveAsync(Guid coverId, CancellationToken ct)
    {
        var cover = await _db.Covers.FindAsync([coverId], ct);
        if (cover is not null)
        {
            _db.Covers.Remove(cover);
        }

        _storage.Delete(coverId);
    }

    private async Task<OperationResult<CoverDto>> StoreAsync(Guid userId, Stream image, CancellationToken ct)
    {
        await using (image)
        {
            var webp = await _processor.ToWebpAsync(image, ct);
            if (webp is null)
            {
                return OperationResult.Fail<CoverDto>("invalid_image", "O arquivo enviado não é uma imagem válida.");
            }

            await using (webp)
            {
                var cover = new Cover { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow };
                await _storage.SaveAsync(cover.Id, webp, ct);
                _db.Covers.Add(cover);
                await _db.SaveChangesAsync(ct);
                return OperationResult.Ok(new CoverDto(cover.Id, $"/v1/covers/{cover.Id}"));
            }
        }
    }

    private static OperationResult<CoverDto> TooLarge() =>
        OperationResult.Fail<CoverDto>("image_too_large", "A capa pode ter no máximo 5 MB.");
}
