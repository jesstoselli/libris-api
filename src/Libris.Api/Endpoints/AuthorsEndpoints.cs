using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.UseCases.Books;

namespace Libris.Api.Endpoints;

public static class AuthorsEndpoints
{
    public static IEndpointRouteBuilder MapAuthorsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/authors", async (ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
                principal.TryGetUserId(out var userId)
                    ? HttpResults.ToHttpResult(await books.ListAuthorsAsync(userId, ct))
                    : Results.Unauthorized())
            .WithTags("Books")
            .RequireAuthorization();

        return app;
    }
}
