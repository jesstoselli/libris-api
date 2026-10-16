namespace Libris.Application.Dtos.Genres;

public sealed record GenreDto(Guid Id, string Name, int BookCount);

public sealed record GenreRequest(string Name);
