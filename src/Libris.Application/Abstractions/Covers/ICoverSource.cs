namespace Libris.Application.Abstractions.Covers;

public enum CoverDownloadStatus
{
    Ok,
    HostNotAllowed,
    Failed,
    TooLarge,
}

public sealed record CoverDownload(CoverDownloadStatus Status, byte[]? Bytes);

public interface ICoverSource
{
    Task<CoverDownload> DownloadAsync(string url, CancellationToken ct);
}
