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

        books = query.Format switch
        {
            BookFormat.Physical => books.Where(b => b.IsPhysical),
            BookFormat.Digital => books.Where(b => b.IsDigital),
            _ => books,
        };

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
            .OrderBy(b => b.Title, StringComparer.InvariantCultureIgnoreCase)
            .Select(ToDto)
            .ToList();
        return OperationResult.Ok(dtos);
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
        ApplyFields(book, request.Title, request.Authors, isbn.Value, request.TotalPages, request.Publisher,
            request.PublishedYear, request.Synopsis, request.CoverId, now);
        foreach (var genre in await _genres.ResolveByNamesAsync(userId, request.Genres ?? [], ct))
        {
            book.Genres.Add(genre);
        }

        _db.Books.Add(book);
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

        book.Genres.Clear();
        foreach (var genre in await _genres.ResolveByNamesAsync(userId, request.Genres ?? [], ct))
        {
            book.Genres.Add(genre);
        }

        await _db.SaveChangesAsync(ct);

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

        book.ChangeStatus(request.Status, request.Today);
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
            book.Formats);
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
