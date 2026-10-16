using Libris.Domain.Entities;

namespace Libris.Domain.Reading;

/// <param name="FromPage">The furthest page reached before this log; pages are counted from there.</param>
public sealed record LogDelta(ReadingLog Log, int FromPage, int PagesRead);

public static class ReadingProgress
{
    public static IReadOnlyList<LogDelta> Deltas(IEnumerable<ReadingLog> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        // Counted against the furthest page reached, not the previous log: after a typo (140) and its
        // correction (120), reading on to 150 adds 10 pages, not 30 — the same pages never count twice.
        var furthest = 0;
        var deltas = new List<LogDelta>();
        foreach (var log in Chronological(logs))
        {
            deltas.Add(new LogDelta(log, furthest, Math.Max(0, log.Page - furthest)));
            furthest = Math.Max(furthest, log.Page);
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
