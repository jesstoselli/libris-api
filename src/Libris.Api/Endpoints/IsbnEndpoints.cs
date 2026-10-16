using Libris.Application.UseCases.IsbnLookup;

namespace Libris.Api.Endpoints;

public static class IsbnEndpoints
{
    public static IEndpointRouteBuilder MapIsbnEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/isbn/{isbn}", async (string isbn, IsbnLookupService lookup, CancellationToken ct) =>
            HttpResults.ToHttpResult(await lookup.LookupAsync(isbn, ct)))
        .WithTags("Isbn")
        .RequireAuthorization();

        return app;
    }
}
