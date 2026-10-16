using Libris.Application.Dtos.Books;

namespace Libris.Application.Dtos.Reading;

/// <param name="FromPage">The furthest page reached before this log (0 for the first); PagesRead counts from it.</param>
public sealed record ReadingLogDto(Guid Id, DateOnly Date, int Page, int FromPage, int PagesRead);

public sealed record ReadingLogRequest(DateOnly Date, int Page);

public sealed record ReadingLogResult(BookDto Book, IReadOnlyList<ReadingLogDto> Logs);
