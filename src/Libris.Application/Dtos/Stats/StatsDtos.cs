namespace Libris.Application.Dtos.Stats;

public sealed record MonthStatsDto(int Month, int PagesRead, int BooksFinished);

public sealed record YearStatsDto(int Year, int BooksFinished, int PagesRead, int ReadingNow, IReadOnlyList<MonthStatsDto> Months);

public sealed record FinishedBookDto(Guid BookId, string Title, IReadOnlyList<string> Authors, DateOnly FinishedAt);

public sealed record ProgressedBookDto(Guid BookId, string Title, int PagesRead);

public sealed record MonthDetailDto(
    int Year,
    int Month,
    int PagesRead,
    IReadOnlyList<FinishedBookDto> Finished,
    IReadOnlyList<ProgressedBookDto> Progressed);
