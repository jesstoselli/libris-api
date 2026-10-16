using FluentValidation;
using Libris.Application.Abstractions.Persistence;
using Libris.Application.Common;
using Libris.Application.Dtos.Genres;
using Libris.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Libris.Application.UseCases.Genres;

public class GenreService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<GenreRequest> _validator;

    public GenreService(IApplicationDbContext db, IValidator<GenreRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<OperationResult<IReadOnlyList<GenreDto>>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var genres = await _db.Genres
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .Select(g => new GenreDto(g.Id, g.Name, g.Books.Count))
            .ToListAsync(ct);

        IReadOnlyList<GenreDto> ordered = genres.OrderBy(g => g.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        return OperationResult.Ok(ordered);
    }

    public async Task<OperationResult<GenreDto>> CreateAsync(Guid userId, GenreRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<GenreDto>("validation_error", validation.Errors[0].ErrorMessage);
        }

        var name = request.Name.Trim();
        if (await FindByNameAsync(userId, name, excludeId: null, ct) is not null)
        {
            return AlreadyExists(name);
        }

        var now = DateTime.UtcNow;
        var genre = new Genre { Id = Guid.NewGuid(), UserId = userId, Name = name, CreatedAt = now, UpdatedAt = now };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(new GenreDto(genre.Id, genre.Name, 0));
    }

    public async Task<OperationResult<GenreDto>> UpdateAsync(Guid userId, Guid id, GenreRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return OperationResult.Fail<GenreDto>("validation_error", validation.Errors[0].ErrorMessage);
        }

        var genre = await _db.Genres.Include(g => g.Books).SingleOrDefaultAsync(g => g.Id == id && g.UserId == userId, ct);
        if (genre is null)
        {
            return NotFound<GenreDto>();
        }

        var name = request.Name.Trim();
        if (await FindByNameAsync(userId, name, excludeId: id, ct) is not null)
        {
            return AlreadyExists(name);
        }

        genre.Name = name;
        genre.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return OperationResult.Ok(new GenreDto(genre.Id, genre.Name, genre.Books.Count));
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var genre = await _db.Genres.SingleOrDefaultAsync(g => g.Id == id && g.UserId == userId, ct);
        if (genre is null)
        {
            return NotFound<bool>();
        }

        _db.Genres.Remove(genre);
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok(true);
    }

    /// <summary>Finds the user's genres by name, creating the missing ones (book form sends names, not ids).</summary>
    public async Task<IReadOnlyList<Genre>> ResolveByNamesAsync(Guid userId, IEnumerable<string> names, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        var wanted = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        var existing = await _db.Genres.Where(g => g.UserId == userId).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var resolved = new List<Genre>();
        foreach (var name in wanted)
        {
            var genre = existing.Find(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            if (genre is null)
            {
                genre = new Genre { Id = Guid.NewGuid(), UserId = userId, Name = name, CreatedAt = now, UpdatedAt = now };
                _db.Genres.Add(genre);
                existing.Add(genre);
            }

            resolved.Add(genre);
        }

        return resolved;
    }

    // Compared in memory: a user has a handful of genres, and Postgres lower() depends on the
    // server locale for accented letters ("Gótico" vs "GÓTICO").
    private async Task<Genre?> FindByNameAsync(Guid userId, string name, Guid? excludeId, CancellationToken ct)
    {
        var genres = await _db.Genres.AsNoTracking().Where(g => g.UserId == userId && g.Id != excludeId).ToListAsync(ct);
        return genres.Find(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static OperationResult<GenreDto> AlreadyExists(string name) =>
        OperationResult.Fail<GenreDto>("genre_already_exists", $"Já existe um gênero chamado \"{name}\".");

    private static OperationResult<T> NotFound<T>() =>
        OperationResult.Fail<T>("not_found", "Gênero não encontrado.");
}
