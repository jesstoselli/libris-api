using Libris.Domain.Enums;

namespace Libris.Application.Dtos.Books;

public sealed record GenreRefDto(Guid Id, string Name);

public sealed record BookDto(
    Guid Id,
    string Title,
    IReadOnlyList<string> Authors,
    string? Isbn,
    int? TotalPages,
    string? Publisher,
    int? PublishedYear,
    string? Synopsis,
    Guid? CoverId,
    string? CoverUrl,
    IReadOnlyList<GenreRefDto> Genres,
    BookOwnership Ownership,
    BookStatus? Status,
    DateOnly? StartedAt,
    DateOnly? FinishedAt,
    DateOnly AddedAt,
    int CurrentPage,
    int Percent,
    IReadOnlyList<BookFormat> Formats);

public sealed record CreateBookRequest(
    string Title,
    IReadOnlyList<string> Authors,
    string? Isbn,
    int? TotalPages,
    string? Publisher,
    int? PublishedYear,
    string? Synopsis,
    Guid? CoverId,
    IReadOnlyList<string> Genres,
    BookOwnership Ownership,
    BookStatus? Status,
    DateOnly? StartedAt,
    DateOnly? FinishedAt,
    DateOnly Today,
    // Optional so older clients keep working: absent means physical.
    IReadOnlyList<BookFormat>? Formats = null);

public sealed record UpdateBookRequest(
    string Title,
    IReadOnlyList<string> Authors,
    string? Isbn,
    int? TotalPages,
    string? Publisher,
    int? PublishedYear,
    string? Synopsis,
    Guid? CoverId,
    IReadOnlyList<string> Genres,
    DateOnly? StartedAt,
    DateOnly? FinishedAt,
    // Optional so older clients keep working: absent keeps the book's current formats.
    IReadOnlyList<BookFormat>? Formats = null);

public sealed record ChangeStatusRequest(BookStatus Status, DateOnly Today);

public sealed record AcquireRequest(DateOnly Today, int? TotalPages);

public sealed record BookListQuery(BookOwnership Ownership, BookStatus? Status, Guid? GenreId, string? Search, BookFormat? Format = null);
