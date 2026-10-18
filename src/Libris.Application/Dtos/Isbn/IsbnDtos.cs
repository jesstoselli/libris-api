namespace Libris.Application.Dtos.Isbn;

public sealed record IsbnLookupDto(
    string Isbn,
    string Title,
    IReadOnlyList<string> Authors,
    int? TotalPages,
    string? Publisher,
    int? PublishedYear,
    string? Synopsis,
    string? CoverUrl,
    IReadOnlyList<string> SuggestedGenres,
    string Source,
    string? Language = null);
