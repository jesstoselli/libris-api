using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.UseCases.Stats;

namespace Libris.Api.Endpoints;

public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/stats").WithTags("Stats").RequireAuthorization();

        group.MapGet("/{year:int}", async (int year, ClaimsPrincipal principal, StatsService stats, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await stats.GetYearAsync(userId, year, ct))
                : Results.Unauthorized());

        group.MapGet("/{year:int}/days", async (int year, ClaimsPrincipal principal, StatsService stats, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await stats.GetDaysAsync(userId, year, ct))
                : Results.Unauthorized());

        group.MapGet("/{year:int}/{month:int}", async (int year, int month, ClaimsPrincipal principal, StatsService stats, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToHttpResult(await stats.GetMonthAsync(userId, year, month, ct))
                : Results.Unauthorized());

        return app;
    }
}
