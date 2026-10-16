namespace Libris.Application.Abstractions.Covers;

public interface ICoverStorage
{
    Task SaveAsync(Guid coverId, Stream webp, CancellationToken ct);

    Stream? OpenRead(Guid coverId);

    void Delete(Guid coverId);
}
