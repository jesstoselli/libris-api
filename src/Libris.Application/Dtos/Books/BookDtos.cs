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
    string Language,
    string? Series = null,
    decimal? SeriesNumber = null,
    decimal? Rating = null);

/// <summary>Sent on create and update; absent keeps the series the book has (older clients), a blank name removes it.</summary>
public sealed record BookSeriesInput(string? Name, decimal? Number);

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
    string? Language = null,
    BookSeriesInput? Series = null);

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
    string? Language = null,
    // Absent keeps the book's current series.
    BookSeriesInput? Series = null);

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

/// <summary>One series and how many of its books are in the Acervo and in the Wishlist.</summary>
public sealed record SeriesDto(string Name, int BookCount, int WishlistCount);

/// <summary>Every book in series <c>From</c> moves to <c>To</c>, keeping its number; an existing <c>To</c> absorbs them.</summary>
public sealed record RenameSeriesRequest(string From, string To);

public sealed record RenameSeriesResult(int Updated);

/// <summary>Null clears the rating.</summary>
public sealed record RateBookRequest(decimal? Rating);

public sealed record AcquireRequest(DateOnly Today, int? TotalPages);

/// <param name="Author">One author exactly as written on the book, as the book page links it.</param>
public sealed record BookListQuery(
    BookOwnership Ownership,
    BookStatus? Status,
    Guid? GenreId,
    string? Search,
    BookFormat? Format = null,
    string? Language = null,
    string? Author = null,
    string? Publisher = null,
    string? Series = null,
    decimal? MinRating = null,
    BookSort Sort = BookSort.Title);

/// <summary>Title is the shelf order; Rating puts the best rated first and the unrated last.</summary>
public enum BookSort
{
    Title,
    Rating,
}
