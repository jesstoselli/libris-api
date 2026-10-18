using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.Dtos.Books;
using Libris.Application.UseCases.Books;
using Libris.Domain.Enums;

namespace Libris.Api.Endpoints;

public static class BooksEndpoints
{
    public static IEndpointRouteBuilder MapBooksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/books").WithTags("Books").RequireAuthorization();

        group.MapGet("/", async (
            string? ownership,
            string? status,
            Guid? genreId,
            string? q,
            string? format,
            ClaimsPrincipal principal,
            BookService books,
            CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            if (!QueryEnumParser.TryParse<BookOwnership>(ownership, out var parsedOwnership)
                || !QueryEnumParser.TryParse<BookStatus>(status, out var parsedStatus)
                || !QueryEnumParser.TryParse<BookFormat>(format, out var parsedFormat))
            {
                return Results.Json(new { error = "validation_error", message = "Filtro inválido." }, statusCode: StatusCodes.Status400BadRequest);
            }

            var query = new BookListQuery(parsedOwnership ?? BookOwnership.Owned, parsedStatus, genreId, q, parsedFormat);
            return HttpResults.ToHttpResult(await books.ListAsync(userId, query, ct));
        });

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await books.GetAsync(userId, id, ct))
                : Results.Unauthorized());

        group.MapPost("/", async (CreateBookRequest request, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToCreatedResult(await books.CreateAsync(userId, request, ct), b => $"/v1/books/{b.Id}")
                : Results.Unauthorized());

        group.MapPut("/{id:guid}", async (Guid id, UpdateBookRequest request, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await books.UpdateAsync(userId, id, request, ct))
                : Results.Unauthorized());

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToNoContentResult(await books.DeleteAsync(userId, id, ct))
                : Results.Unauthorized());

        group.MapPatch("/{id:guid}/status", async (Guid id, ChangeStatusRequest request, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await books.ChangeStatusAsync(userId, id, request, ct))
                : Results.Unauthorized());

        group.MapPost("/{id:guid}/acquire", async (Guid id, AcquireRequest request, ClaimsPrincipal principal, BookService books, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await books.AcquireAsync(userId, id, request, ct))
                : Results.Unauthorized());

        return app;
    }
}
