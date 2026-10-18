namespace Libris.Application.Abstractions.Isbn;

public sealed record IsbnCandidate(
    string? Title,
    IReadOnlyList<string> Authors,
    int? TotalPages,
    string? Publisher,
    int? PublishedYear,
    string? Synopsis,
    string? CoverUrl,
    IReadOnlyList<string> Subjects,
    string? Language = null);

public enum IsbnSourceStatus
{
    Found,
    NotFound,
    Failed,
}

public sealed record IsbnSourceResult(IsbnSourceStatus Status, IsbnCandidate? Candidate)
{
    public static IsbnSourceResult NotFound { get; } = new(IsbnSourceStatus.NotFound, null);
    public static IsbnSourceResult Failed { get; } = new(IsbnSourceStatus.Failed, null);

    public static IsbnSourceResult Found(IsbnCandidate candidate) => new(IsbnSourceStatus.Found, candidate);
}

/// <summary>A metadata source. Registration order is priority order: later sources only fill gaps.</summary>
public interface IIsbnSource
{
    string Name { get; }

    Task<IsbnSourceResult> FindAsync(string isbn13, CancellationToken ct);
}
