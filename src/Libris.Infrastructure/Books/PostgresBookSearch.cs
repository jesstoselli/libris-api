using Libris.Application.Abstractions.Books;
using Libris.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Libris.Infrastructure.Books;

public sealed class PostgresBookSearch : IBookSearch
{
    public IQueryable<Book> Matching(IQueryable<Book> books, string term)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(term);

        var pattern = "%" + Escape(term) + "%";
        return books.Where(b =>
            EF.Functions.ILike(EF.Functions.Unaccent(b.Title), EF.Functions.Unaccent(pattern), "\\")
            || b.Authors.Any(a => EF.Functions.ILike(EF.Functions.Unaccent(a), EF.Functions.Unaccent(pattern), "\\")));
    }

    // The user's text is matched literally: "%" or "_" must not act as wildcards.
    private static string Escape(string term) =>
        term.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
