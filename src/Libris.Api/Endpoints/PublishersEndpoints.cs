using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.UseCases.Books;

namespace Libris.Api.Endpoints;

public static class PublishersEndpoints
{
    public static IEndpointRouteBuilder MapPublishersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/publishers", async (ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
                principal.TryGetUserId(out var userId)
                    ? HttpResults.ToHttpResult(await books.ListPublishersAsync(userId, ct))
                    : Results.Unauthorized())
            .WithTags("Books")
            .RequireAuthorization();

        return app;
    }
}
