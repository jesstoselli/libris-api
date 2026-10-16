using Libris.Application.Abstractions.Persistence;
using Libris.Application.Common;
using Libris.Application.Dtos.Reading;
using Libris.Application.UseCases.Books;
using Libris.Domain.Entities;
using Libris.Domain.Enums;
using Libris.Domain.Reading;

namespace Libris.Application.UseCases.Reading;

public class ReadingLogService
{
    private readonly IApplicationDbContext _db;
    private readonly BookService _books;

    public ReadingLogService(IApplicationDbContext db, BookService books)
    {
        _db = db;
        _books = books;
    }

    public async Task<OperationResult<IReadOnlyList<ReadingLogDto>>> ListAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        var book = await _books.FindAsync(userId, bookId, tracking: false, ct);
        return book is null
            ? OperationResult.Fail<IReadOnlyList<ReadingLogDto>>("not_found", BookNotFound)
            : OperationResult.Ok<IReadOnlyList<ReadingLogDto>>(ToDtos(book));
    }

    public async Task<OperationResult<ReadingLogResult>> CreateAsync(Guid userId, Guid bookId, ReadingLogRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var book = await _books.FindAsync(userId, bookId, tracking: true, ct);
        if (CheckWritable(book, request.Page) is { } error)
        {
            return error;
        }

        var now = DateTime.UtcNow;
        var log = new ReadingLog
        {
            Id = Guid.NewGuid(),
            BookId = book!.Id,
            Date = request.Date,
            Page = request.Page,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // Added through the DbSet (a preset Guid found via navigation would be treated as an existing row);
        // EF's fixup puts it in book.Logs.
        _db.ReadingLogs.Add(log);
        book.ApplyNewLog(log);
        book.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(ToResult(book));
    }

    // Editing or deleting a log only recalculates progress: status and dates never roll back (spec).
    public async Task<OperationResult<ReadingLogResult>> UpdateAsync(Guid userId, Guid bookId, Guid logId, ReadingLogRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var book = await _books.FindAsync(userId, bookId, tracking: true, ct);
        var log = book?.Logs.FirstOrDefault(l => l.Id == logId);
        if (log is null)
        {
            return OperationResult.Fail<ReadingLogResult>("not_found", LogNotFound);
        }

        if (CheckWritable(book, request.Page) is { } error)
        {
            return error;
        }

        log.Date = request.Date;
        log.Page = request.Page;
        log.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(ToResult(book!));
    }

    public async Task<OperationResult<ReadingLogResult>> DeleteAsync(Guid userId, Guid bookId, Guid logId, CancellationToken ct = default)
    {
        var book = await _books.FindAsync(userId, bookId, tracking: true, ct);
        var log = book?.Logs.FirstOrDefault(l => l.Id == logId);
        if (log is null)
        {
            return OperationResult.Fail<ReadingLogResult>("not_found", LogNotFound);
        }

        book!.Logs.Remove(log);
        _db.ReadingLogs.Remove(log);

        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(ToResult(book));
    }

    private static OperationResult<ReadingLogResult>? CheckWritable(Book? book, int page)
    {
        if (book is null)
        {
            return OperationResult.Fail<ReadingLogResult>("not_found", BookNotFound);
        }

        if (book.Ownership != BookOwnership.Owned)
        {
            return OperationResult.Fail<ReadingLogResult>("not_owned", "Livro da wishlist não tem registro de leitura.");
        }

        if (page < 1 || page > book.TotalPages)
        {
            return OperationResult.Fail<ReadingLogResult>(
                "page_out_of_range", $"A página precisa estar entre 1 e {book.TotalPages}.");
        }

        return null;
    }

    private static ReadingLogResult ToResult(Book book) => new(BookService.ToDto(book), ToDtos(book));

    private static List<ReadingLogDto> ToDtos(Book book)
    {
        var dtos = ReadingProgress.Deltas(book.Logs)
            .Select(d => new ReadingLogDto(d.Log.Id, d.Log.Date, d.Log.Page, d.FromPage, d.PagesRead))
            .ToList();
        dtos.Reverse();
        return dtos;
    }

    private const string BookNotFound = "Livro não encontrado.";
    private const string LogNotFound = "Registro de leitura não encontrado.";
}
