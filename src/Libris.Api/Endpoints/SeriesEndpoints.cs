using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.Dtos.Books;
using Libris.Application.UseCases.Books;

namespace Libris.Api.Endpoints;

public static class SeriesEndpoints
{
    public static IEndpointRouteBuilder MapSeriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/series").WithTags("Books").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await books.SeriesSummaryAsync(userId, ct))
                : Results.Unauthorized());

        // The name travels in the query string: series names carry slashes and question marks.
        group.MapGet("/books", async (string name, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await books.SeriesBooksAsync(userId, name, ct))
                : Results.Unauthorized());

        group.MapPut("/", async (RenameSeriesRequest request, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await books.RenameSeriesAsync(userId, request, ct))
                : Results.Unauthorized());

        return app;
    }
}
