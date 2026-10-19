namespace Libris.Application.Dtos.Genres;

/// <summary>BookCount covers every book with the genre; WishlistCount is the part of it still on the wishlist.</summary>
public sealed record GenreDto(Guid Id, string Name, int BookCount, int WishlistCount);

public sealed record GenreRequest(string Name);
