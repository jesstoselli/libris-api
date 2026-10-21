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
    IReadOnlyList<BookFormat> Formats,
    string Language);

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
    IReadOnlyList<BookFormat>? Formats = null,
    // A book registered mid-read: becomes its first reading log, dated on the start date (or today).
    int? CurrentPage = null,
    // Optional so older clients keep working: absent means Portuguese.
    string? Language = null);

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
    IReadOnlyList<BookFormat>? Formats = null,
    // Absent keeps the book's current language.
    string? Language = null);

public sealed record ChangeStatusRequest(
    BookStatus Status,
    DateOnly Today,
    // Optional so older clients keep working: absent means "Lido" is finished today when there is no finish yet.
    bool? WithoutDate = null,
    // "Lido" on a remembered day; it wins over WithoutDate.
    DateOnly? FinishedAt = null);

/// <summary>One spelling of a publisher and how many books carry it, in the Acervo and in the Wishlist.</summary>
public sealed record PublisherDto(string Name, int BookCount, int WishlistCount);

/// <summary>Every book written with <c>From</c> gets <c>To</c>; an existing <c>To</c> simply absorbs them.</summary>
public sealed record RenamePublisherRequest(string From, string To);

public sealed record RenamePublisherResult(int Updated);

public sealed record AcquireRequest(DateOnly Today, int? TotalPages);

/// <param name="Author">One author exactly as written on the book, as the book page links it.</param>
public sealed record BookListQuery(BookOwnership Ownership, BookStatus? Status, Guid? GenreId, string? Search, BookFormat? Format = null, string? Language = null, string? Author = null, string? Publisher = null);
