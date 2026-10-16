namespace Libris.Application.Dtos.Covers;

public sealed record CoverDto(Guid CoverId, string Url);

public sealed record CoverFromUrlRequest(string Url);
