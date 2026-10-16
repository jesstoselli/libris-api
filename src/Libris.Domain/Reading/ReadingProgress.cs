using Libris.Domain.Entities;

namespace Libris.Domain.Reading;

public sealed record LogDelta(ReadingLog Log, int PagesRead);

public static class ReadingProgress
{
    public static IReadOnlyList<LogDelta> Deltas(IEnumerable<ReadingLog> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        var previousPage = 0;
        var deltas = new List<LogDelta>();
        foreach (var log in Chronological(logs))
        {
            // A lower page is a correction, not negative reading.
            deltas.Add(new LogDelta(log, Math.Max(0, log.Page - previousPage)));
            previousPage = log.Page;
        }

        return deltas;
    }

    public static int CurrentPage(IEnumerable<ReadingLog> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        return Chronological(logs).LastOrDefault()?.Page ?? 0;
    }

    public static int Percent(int currentPage, int? totalPages)
    {
        if (totalPages is not > 0)
        {
            return 0;
        }

        return Math.Min(100, currentPage * 100 / totalPages.Value);
    }

    private static IOrderedEnumerable<ReadingLog> Chronological(IEnumerable<ReadingLog> logs) =>
        logs.OrderBy(l => l.Date).ThenBy(l => l.CreatedAt);
}
