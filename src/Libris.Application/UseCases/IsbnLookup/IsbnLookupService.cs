using Libris.Application.Abstractions.Isbn;
using Libris.Application.Common;
using Libris.Application.Dtos.Isbn;
using Libris.Domain.Books;

namespace Libris.Application.UseCases.IsbnLookup;

public class IsbnLookupService
{
    private readonly IReadOnlyList<IIsbnSource> _sources;

    public IsbnLookupService(IEnumerable<IIsbnSource> sources)
    {
        _sources = [.. sources];
    }

    public async Task<OperationResult<IsbnLookupDto>> LookupAsync(string raw, CancellationToken ct = default)
    {
        if (!Isbn.TryNormalize(raw, out var isbn13))
        {
            return OperationResult.Fail<IsbnLookupDto>("invalid_isbn", "ISBN inválido.");
        }

        IsbnCandidate? merged = null;
        var used = new List<string>();
        var anyFailed = false;
        foreach (var source in _sources)
        {
            // Complete means title, pages and at least one author; a book without its author is not done yet.
            if (merged is { Title: not null, TotalPages: not null, Authors.Count: > 0 })
            {
                break;
            }

            var result = await source.FindAsync(isbn13, ct);
            if (result.Status == IsbnSourceStatus.Failed)
            {
                anyFailed = true;
                continue;
            }

            if (result.Candidate is { } candidate)
            {
                merged = merged is null ? candidate : Fill(merged, candidate);
                used.Add(source.Name);
            }
        }

        if (merged?.Title is null)
        {
            // "Not found" only when every source answered; a source that was down might have had it.
            return anyFailed
                ? OperationResult.Fail<IsbnLookupDto>("isbn_lookup_unavailable", "A busca por ISBN está indisponível agora. Tente de novo ou preencha à mão.")
                : OperationResult.Fail<IsbnLookupDto>("isbn_not_found", "Nenhum livro encontrado com esse ISBN.");
        }

        return OperationResult.Ok(new IsbnLookupDto(
            isbn13,
            merged.Title,
            merged.Authors,
            merged.TotalPages,
            merged.Publisher,
            merged.PublishedYear,
            merged.Synopsis,
            merged.CoverUrl,
            GenreSuggestions.From(merged.Subjects),
            string.Join('+', used),
            merged.Language));
    }

    private static IsbnCandidate Fill(IsbnCandidate primary, IsbnCandidate fallback) => new(
        primary.Title ?? fallback.Title,
        primary.Authors.Count > 0 ? primary.Authors : fallback.Authors,
        primary.TotalPages ?? fallback.TotalPages,
        primary.Publisher ?? fallback.Publisher,
        primary.PublishedYear ?? fallback.PublishedYear,
        primary.Synopsis ?? fallback.Synopsis,
        primary.CoverUrl ?? fallback.CoverUrl,
        [.. primary.Subjects, .. fallback.Subjects],
        primary.Language ?? fallback.Language);
}
