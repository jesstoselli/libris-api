using Libris.Application.Abstractions.Persistence;
using Libris.Application.Common;
using Libris.Application.Dtos.Stats;
using Libris.Domain.Entities;
using Libris.Domain.Enums;
using Libris.Domain.Reading;
using Microsoft.EntityFrameworkCore;

namespace Libris.Application.UseCases.Stats;

/// <summary>
/// Finished books count by FinishedAt (read books only); pages by the deltas of logs dated in the period, or a whole
/// unlogged book on its finish day.
/// Computed in memory: a single reader's whole history is a few thousand rows at most.
/// </summary>
public class StatsService
{
    private const int MinYear = 1900;
    private const int MaxYear = 2100;

    private readonly IApplicationDbContext _db;

    public StatsService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult<YearStatsDto>> GetYearAsync(Guid userId, int year, CancellationToken ct = default)
    {
        if (year is < MinYear or > MaxYear)
        {
            return OperationResult.Fail<YearStatsDto>("validation_error", "Ano inválido.");
        }

        var books = await LoadAsync(userId, ct);
        var deltas = Deltas(books).Where(d => d.Date.Year == year).ToList();
        var finished = Finished(books).Where(b => b.FinishedAt!.Value.Year == year).ToList();

        var months = Enumerable.Range(1, 12)
            .Select(m => new MonthStatsDto(
                m,
                deltas.Where(d => d.Date.Month == m).Sum(d => d.PagesRead),
                finished.Count(b => b.FinishedAt!.Value.Month == m)))
            .ToList();

        return OperationResult.Ok(new YearStatsDto(
            year,
            finished.Count,
            deltas.Sum(d => d.PagesRead),
            books.Count(b => b.Status == BookStatus.Reading),
            months,
            books.Count(b => b.Status == BookStatus.Read && b.FinishedAt is null)));
    }

    public async Task<OperationResult<MonthDetailDto>> GetMonthAsync(Guid userId, int year, int month, CancellationToken ct = default)
    {
        if (year is < MinYear or > MaxYear || month is < 1 or > 12)
        {
            return OperationResult.Fail<MonthDetailDto>("validation_error", "Mês inválido.");
        }

        var books = await LoadAsync(userId, ct);
        var deltas = Deltas(books).Where(d => d.Date.Year == year && d.Date.Month == month).ToList();

        var finished = Finished(books)
            .Where(b => b.FinishedAt!.Value.Year == year && b.FinishedAt.Value.Month == month)
            .OrderByDescending(b => b.FinishedAt)
            .Select(b => new FinishedBookDto(b.Id, b.Title, b.Authors, b.FinishedAt!.Value))
            .ToList();

        var progressed = deltas
            .GroupBy(d => d.Book)
            .Select(g => new ProgressedBookDto(g.Key.Id, g.Key.Title, g.Sum(d => d.PagesRead)))
            .Where(p => p.PagesRead > 0)
            .OrderByDescending(p => p.PagesRead)
            .ToList();

        return OperationResult.Ok(new MonthDetailDto(year, month, deltas.Sum(d => d.PagesRead), finished, progressed));
    }

    private Task<List<Book>> LoadAsync(Guid userId, CancellationToken ct) =>
        _db.Books
            .AsNoTracking()
            .Include(b => b.Logs)
            .Where(b => b.UserId == userId && b.Ownership == BookOwnership.Owned)
            .ToListAsync(ct);

    private static IEnumerable<Book> Finished(IEnumerable<Book> books) =>
        books.Where(b => b.Status == BookStatus.Read && b.FinishedAt is not null);

    /// <summary>
    /// What the logs say was read. A book finished on a known day with no logs at all (read before it was registered)
    /// counts its whole length on that day, so old reads show up in the pages too.
    /// </summary>
    private static IEnumerable<BookDelta> Deltas(IEnumerable<Book> books) =>
        books.SelectMany(b => b.Logs.Count == 0
            ? Unlogged(b)
            : ReadingProgress.Deltas(b.Logs).Select(d => new BookDelta(b, d.Log.Date, d.PagesRead)));

    private static IEnumerable<BookDelta> Unlogged(Book book) =>
        book is { Status: BookStatus.Read, FinishedAt: { } finished, TotalPages: > 0 and var pages }
            ? [new BookDelta(book, finished, pages)]
            : [];

    private sealed record BookDelta(Book Book, DateOnly Date, int PagesRead);
}
