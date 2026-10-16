using Libris.Domain.Entities;

namespace Libris.Application.Abstractions.Books;

/// <summary>Accent- and case-insensitive title/author search; needs Postgres functions the Application layer cannot reference.</summary>
public interface IBookSearch
{
    IQueryable<Book> Matching(IQueryable<Book> books, string term);
}
