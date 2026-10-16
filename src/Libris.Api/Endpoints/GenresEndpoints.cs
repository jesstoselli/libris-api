using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.Dtos.Genres;
using Libris.Application.UseCases.Genres;

namespace Libris.Api.Endpoints;

public static class GenresEndpoints
{
    public static IEndpointRouteBuilder MapGenresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/genres").WithTags("Genres").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, GenreService genres, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await genres.ListAsync(userId, ct))
                : Results.Unauthorized());

        group.MapPost("/", async (GenreRequest request, ClaimsPrincipal principal, GenreService genres, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToCreatedResult(await genres.CreateAsync(userId, request, ct), g => $"/v1/genres/{g.Id}")
                : Results.Unauthorized());

        group.MapPut("/{id:guid}", async (Guid id, GenreRequest request, ClaimsPrincipal principal, GenreService genres, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await genres.UpdateAsync(userId, id, request, ct))
                : Results.Unauthorized());

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, GenreService genres, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToNoContentResult(await genres.DeleteAsync(userId, id, ct))
                : Results.Unauthorized());

        return app;
    }
}
