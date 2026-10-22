namespace Libris.Application.Dtos.Stats;

public sealed record MonthStatsDto(int Month, int PagesRead, int BooksFinished);

// ReadWithoutDate is every year's: read books whose finish nobody remembers, so no month counts them.
public sealed record YearStatsDto(int Year, int BooksFinished, int PagesRead, int ReadingNow, IReadOnlyList<MonthStatsDto> Months, int ReadWithoutDate);

public sealed record FinishedBookDto(Guid BookId, string Title, IReadOnlyList<string> Authors, DateOnly FinishedAt);

public sealed record ProgressedBookDto(Guid BookId, string Title, int PagesRead);

/// <summary>A day of the reading calendar: the pages read and which books they came from.</summary>
public sealed record DayStatsDto(DateOnly Date, int PagesRead, IReadOnlyList<ProgressedBookDto> Books);

public sealed record MonthDetailDto(
    int Year,
    int Month,
    int PagesRead,
    IReadOnlyList<FinishedBookDto> Finished,
    IReadOnlyList<ProgressedBookDto> Progressed);
