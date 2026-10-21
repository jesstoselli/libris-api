using FluentValidation;
using Libris.Application.Abstractions.Books;
using Libris.Application.Abstractions.Persistence;
using Libris.Application.Common;
using Libris.Application.Dtos.Books;
using Libris.Application.UseCases.Covers;
using Libris.Application.UseCases.Genres;
using Libris.Domain.Books;
using Libris.Domain.Entities;
using Libris.Domain.Enums;
using Libris.Domain.Reading;
using Microsoft.EntityFrameworkCore;

namespace Libris.Application.UseCases.Books;

public class BookService
{
    private const int MaxPublisherLength = 200;

    private readonly IApplicationDbContext _db;
    private readonly GenreService _genres;
    private readonly IBookSearch _search;
    private readonly CoverService _covers;
    private readonly IValidator<CreateBookRequest> _createValidator;
    private readonly IValidator<UpdateBookRequest> _updateValidator;
    private readonly IValidator<AcquireRequest> _acquireValidator;

    public BookService(
        IApplicationDbContext db,
        GenreService genres,
        IBookSearch search,
        CoverService covers,
        IValidator<CreateBookRequest> createValidator,
        IValidator<UpdateBookRequest> updateValidator,
        IValidator<AcquireRequest> acquireValidator)
    {
        _db = db;
        _genres = genres;
        _search = search;
        _covers = covers;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _acquireValidator = acquireValidator;
    }

    public async Task<OperationResult<IReadOnlyList<BookDto>>> ListAsync(Guid userId, BookListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var books = WithDetails(_db.Books.AsNoTracking())
            .Where(b => b.UserId == userId && b.Ownership == query.Ownership);

        if (query.Status is { } status)
        {
            books = books.Where(b => b.Status == status);
        }

        if (query.Language is { } language)
        {
            var code = language.ToLowerInvariant();
            books = books.Where(b => b.Language == code);
        }

        books = query.Format switch
        {
            BookFormat.Physical => books.Where(b => b.IsPhysical),
            BookFormat.Digital => books.Where(b => b.IsDigital),
            _ => books,
        };

        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            var author = query.Author.Trim();
            books = books.Where(b => b.Authors.Contains(author));
        }

        if (!string.IsNullOrWhiteSpace(query.Publisher))
        {
            var publisher = query.Publisher.Trim();
            books = books.Where(b => b.Publisher == publisher);
        }

        if (query.GenreId is { } genreId)
        {
            books = books.Where(b => b.Genres.Any(g => g.Id == genreId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            books = _search.Matching(books, query.Search.Trim());
        }

        var list = await books.ToListAsync(ct);
        IReadOnlyList<BookDto> dtos = list
            .OrderBy(b => TitleSort.Key(b.Title, b.Language), StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(b => b.Title, StringComparer.InvariantCultureIgnoreCase)
            .Select(ToDto)
            .ToList();
        return OperationResult.Ok(dtos);
    }

    /// <summary>Every author on the user's books, owned or wished for, once and by name; feeds the form's suggestions.</summary>
    public async Task<OperationResult<IReadOnlyList<string>>> ListAuthorsAsync(Guid userId, CancellationToken ct = default)
    {
        var lists = await _db.Books.AsNoTracking().Where(b => b.UserId == userId).Select(b => b.Authors).ToListAsync(ct);
        IReadOnlyList<string> authors = lists
            .SelectMany(a => a)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
        return OperationResult.Ok(authors);
    }

    /// <summary>Every publisher on the user's books, once and by name; feeds the form's suggestions.</summary>
    public async Task<OperationResult<IReadOnlyList<string>>> ListPublishersAsync(Guid userId, CancellationToken ct = default)
    {
        var names = await _db.Books.AsNoTracking()
            .Where(b => b.UserId == userId && b.Publisher != null)
            .Select(b => b.Publisher!)
            .Distinct()
            .ToListAsync(ct);
        IReadOnlyList<string> publishers = names.OrderBy(p => p, StringComparer.InvariantCultureIgnoreCase).ToList();
        return OperationResult.Ok(publishers);
    }

    /// <summary>Each spelling apart with its counts, so the publishers manager shows which variants to merge.</summary>
    public async Task<OperationResult<IReadOnlyList<PublisherDto>>> PublisherSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.Books.AsNoTracking()
            .Where(b => b.UserId == userId && b.Publisher != null)
            .GroupBy(b => b.Publisher!)
            .Select(g => new PublisherDto(
                g.Key,
                g.Count(b => b.Ownership == BookOwnership.Owned),
                g.Count(b => b.Ownership == BookOwnership.Wishlist)))
            .ToListAsync(ct);
        IReadOnlyList<PublisherDto> summary = rows.OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        return OperationResult.Ok(summary);
    }

    public async Task<OperationResult<RenamePublisherResult>> RenamePublisherAsync(Guid userId, RenamePublisherRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var to = request.To?.Trim();
        if (string.IsNullOrEmpty(to) || to.Length > MaxPublisherLength)
        {
            return OperationResult.Fail<RenamePublisherResult>("validation_error", $"A editora precisa ter entre 1 e {MaxPublisherLength} caracteres.");
        }

        var now = DateTime.UtcNow;
        var books = await _db.Books.Where(b => b.UserId == userId && b.Publisher == request.From).ToListAsync(ct);
        foreach (var book in books)
        {
            book.Publisher = to;
            book.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(new RenamePublisherResult(books.Count));
    }

    public async Task<OperationResult<BookDto>> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var book = await FindAsync(userId, id, tracking: false, ct);
        return book is null ? NotFound() : OperationResult.Ok(ToDto(book));
    }

    public async Task<OperationResult<BookDto>> CreateAsync(Guid userId, CreateBookRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<BookDto>("validation_error", validation.Errors[0].ErrorMessage);
        }

        var isbn = await CheckIsbnAsync(userId, request.Isbn, excludeId: null, ct);
        if (!isbn.IsSuccess)
        {
            return OperationResult.Fail<BookDto>(isbn.ErrorCode!, isbn.ErrorMessage!);
        }

        if (request.CoverId is { } coverId && !await _covers.BelongsToAsync(userId, coverId, ct))
        {
            return CoverNotFound();
        }

        var now = DateTime.UtcNow;
        var owned = request.Ownership == BookOwnership.Owned;
        var book = new Book
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Ownership = request.Ownership,
            // Creation never fills dates: an old book registered as read keeps the dates typed in the form.
            Status = owned ? request.Status ?? BookStatus.NotStarted : null,
            StartedAt = request.StartedAt,
            FinishedAt = request.FinishedAt,
            AddedAt = request.Today,
            CreatedAt = now,
        };
        book.SetFormats(request.Formats ?? [BookFormat.Physical]);
        book.Language = (request.Language ?? "pt").ToLowerInvariant();
        ApplyFields(book, request.Title, request.Authors, isbn.Value, request.TotalPages, request.Publisher,
            request.PublishedYear, request.Synopsis, request.CoverId, now);
        foreach (var genre in await _genres.ResolveByNamesAsync(userId, request.Genres ?? [], ct))
        {
            book.Genres.Add(genre);
        }

        _db.Books.Add(book);
        if (request.CurrentPage is { } page)
        {
            // Same path as "Anotar" on the Ficha, so progress, history and stats agree from the start.
            var log = new ReadingLog
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                Date = request.StartedAt ?? request.Today,
                Page = page,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.ReadingLogs.Add(log);
            book.ApplyNewLog(log);
        }

        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(ToDto(book));
    }

    public async Task<OperationResult<BookDto>> UpdateAsync(Guid userId, Guid id, UpdateBookRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<BookDto>("validation_error", validation.Errors[0].ErrorMessage);
        }

        var book = await FindAsync(userId, id, tracking: true, ct);
        if (book is null)
        {
            return NotFound();
        }

        if (book.Ownership == BookOwnership.Owned && request.TotalPages is null)
        {
            return OperationResult.Fail<BookDto>("validation_error", "Número de páginas é obrigatório para livros do acervo.");
        }

        var isbn = await CheckIsbnAsync(userId, request.Isbn, excludeId: id, ct);
        if (!isbn.IsSuccess)
        {
            return OperationResult.Fail<BookDto>(isbn.ErrorCode!, isbn.ErrorMessage!);
        }

        var previousCoverId = book.CoverId;
        if (request.CoverId is { } coverId && coverId != previousCoverId && !await _covers.BelongsToAsync(userId, coverId, ct))
        {
            return CoverNotFound();
        }

        ApplyFields(book, request.Title, request.Authors, isbn.Value, request.TotalPages, request.Publisher,
            request.PublishedYear, request.Synopsis, request.CoverId, DateTime.UtcNow);
        book.StartedAt = request.StartedAt;
        book.FinishedAt = request.FinishedAt;
        if (request.Formats is { } formats)
        {
            book.SetFormats(formats);
        }

        if (request.Language is { } language)
        {
            book.Language = language.ToLowerInvariant();
        }

        book.Genres.Clear();
        foreach (var genre in await _genres.ResolveByNamesAsync(userId, request.Genres ?? [], ct))
        {
            book.Genres.Add(genre);
        }

        await _db.SaveChangesAsync(ct);
        await _genres.DeleteUnusedAsync(userId, ct);

        if (previousCoverId is { } replaced && replaced != book.CoverId)
        {
            await _covers.RemoveAsync(replaced, ct);
            await _db.SaveChangesAsync(ct);
        }

        return OperationResult.Ok(ToDto(book));
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var book = await _db.Books.SingleOrDefaultAsync(b => b.Id == id && b.UserId == userId, ct);
        if (book is null)
        {
            return OperationResult.Fail<bool>("not_found", NotFoundMessage);
        }

        _db.Books.Remove(book);
        if (book.CoverId is { } coverId)
        {
            await _covers.RemoveAsync(coverId, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _genres.DeleteUnusedAsync(userId, ct);
        return OperationResult.Ok(true);
    }

    public async Task<OperationResult<BookDto>> ChangeStatusAsync(Guid userId, Guid id, ChangeStatusRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var book = await FindAsync(userId, id, tracking: true, ct);
        if (book is null)
        {
            return NotFound();
        }

        if (book.Ownership != BookOwnership.Owned)
        {
            return NotOwned();
        }

        if (request.FinishedAt is { } finished && finished > request.Today)
        {
            return OperationResult.Fail<BookDto>("validation_error", "A data não pode estar no futuro.");
        }

        book.ChangeStatus(request.Status, request.Today, request.WithoutDate == true, request.FinishedAt);
        book.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(ToDto(book));
    }

    public async Task<OperationResult<BookDto>> AcquireAsync(Guid userId, Guid id, AcquireRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _acquireValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<BookDto>("validation_error", validation.Errors[0].ErrorMessage);
        }

        var book = await FindAsync(userId, id, tracking: true, ct);
        if (book is null)
        {
            return NotFound();
        }

        if (book.Ownership == BookOwnership.Owned)
        {
            return OperationResult.Fail<BookDto>("already_owned", "Esse livro já está no acervo.");
        }

        if (!book.Acquire(request.Today, request.TotalPages))
        {
            return OperationResult.Fail<BookDto>("total_pages_required", "Informe o número de páginas para trazer o livro ao acervo.");
        }

        book.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(ToDto(book));
    }

    public static BookDto ToDto(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        var currentPage = ReadingProgress.CurrentPage(book.Logs);
        return new BookDto(
            book.Id,
            book.Title,
            book.Authors,
            book.Isbn,
            book.TotalPages,
            book.Publisher,
            book.PublishedYear,
            book.Synopsis,
            book.CoverId,
            book.CoverId is { } coverId ? $"/v1/covers/{coverId}" : null,
            [.. book.Genres.OrderBy(g => g.Name, StringComparer.InvariantCultureIgnoreCase).Select(g => new GenreRefDto(g.Id, g.Name))],
            book.Ownership,
            book.Status,
            book.StartedAt,
            book.FinishedAt,
            book.AddedAt,
            currentPage,
            ReadingProgress.Percent(currentPage, book.TotalPages),
            book.Formats,
            book.Language);
    }

    public Task<Book?> FindAsync(Guid userId, Guid id, bool tracking, CancellationToken ct)
    {
        var books = tracking ? _db.Books : _db.Books.AsNoTracking();
        return WithDetails(books).SingleOrDefaultAsync(b => b.Id == id && b.UserId == userId, ct);
    }

    private static IQueryable<Book> WithDetails(IQueryable<Book> books) =>
        books.Include(b => b.Genres).Include(b => b.Logs);

    private static void ApplyFields(
        Book book,
        string title,
        IReadOnlyList<string> authors,
        string? isbn,
        int? totalPages,
        string? publisher,
        int? publishedYear,
        string? synopsis,
        Guid? coverId,
        DateTime now)
    {
        book.Title = title.Trim();
        book.Authors = [.. authors.Select(a => a.Trim())];
        book.Isbn = isbn;
        book.TotalPages = totalPages;
        book.Publisher = string.IsNullOrWhiteSpace(publisher) ? null : publisher.Trim();
        book.PublishedYear = publishedYear;
        book.Synopsis = string.IsNullOrWhiteSpace(synopsis) ? null : synopsis.Trim();
        book.CoverId = coverId;
        book.UpdatedAt = now;
    }

    /// <returns>The normalized ISBN-13 (null when none was given), or the error to return.</returns>
    private async Task<OperationResult<string?>> CheckIsbnAsync(Guid userId, string? raw, Guid? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return OperationResult.Ok<string?>(null);
        }

        if (!Isbn.TryNormalize(raw, out var isbn13))
        {
            return OperationResult.Fail<string?>("invalid_isbn", "ISBN inválido.");
        }

        var existing = await _db.Books.AsNoTracking()
            .Where(b => b.UserId == userId && b.Isbn == isbn13 && b.Id != excludeId)
            .Select(b => b.Title)
            .FirstOrDefaultAsync(ct);

        return existing is null
            ? OperationResult.Ok<string?>(isbn13)
            : OperationResult.Fail<string?>("isbn_already_registered", $"Esse ISBN já está cadastrado: {existing}.");
    }

    private const string NotFoundMessage = "Livro não encontrado.";

    private static OperationResult<BookDto> NotFound() => OperationResult.Fail<BookDto>("not_found", NotFoundMessage);

    private static OperationResult<BookDto> CoverNotFound() =>
        OperationResult.Fail<BookDto>("cover_not_found", "Capa não encontrada.");

    private static OperationResult<BookDto> NotOwned() =>
        OperationResult.Fail<BookDto>("not_owned", "Livro da wishlist não tem situação de leitura.");
}
