using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.Dtos.Reading;
using Libris.Application.UseCases.Reading;

namespace Libris.Api.Endpoints;

public static class ReadingLogsEndpoints
{
    public static IEndpointRouteBuilder MapReadingLogsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/books/{bookId:guid}/logs").WithTags("Reading").RequireAuthorization();

        group.MapGet("/", async (Guid bookId, ClaimsPrincipal principal, ReadingLogService logs, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await logs.ListAsync(userId, bookId, ct))
                : Results.Unauthorized());

        group.MapPost("/", async (Guid bookId, ReadingLogRequest request, ClaimsPrincipal principal, ReadingLogService logs, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToCreatedResult(await logs.CreateAsync(userId, bookId, request, ct), r => $"/v1/books/{bookId}/logs/{r.Logs[0].Id}")
                : Results.Unauthorized());

        group.MapPut("/{logId:guid}", async (Guid bookId, Guid logId, ReadingLogRequest request, ClaimsPrincipal principal, ReadingLogService logs, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await logs.UpdateAsync(userId, bookId, logId, request, ct))
                : Results.Unauthorized());

        group.MapDelete("/{logId:guid}", async (Guid bookId, Guid logId, ClaimsPrincipal principal, ReadingLogService logs, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await logs.DeleteAsync(userId, bookId, logId, ct))
                : Results.Unauthorized());

        return app;
    }
}
