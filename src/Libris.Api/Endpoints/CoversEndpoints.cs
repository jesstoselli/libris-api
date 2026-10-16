using System.Security.Claims;
using Libris.Api.Extensions;
using Libris.Application.Dtos.Covers;
using Libris.Application.UseCases.Covers;

namespace Libris.Api.Endpoints;

public static class CoversEndpoints
{
    public static IEndpointRouteBuilder MapCoversEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/covers").WithTags("Covers");

        group.MapPost("/", async (IFormFile? file, ClaimsPrincipal principal, CoverService covers, CancellationToken ct) =>
        {
            if (!principal.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            if (file is null)
            {
                return Results.Json(new { error = "validation_error", message = "Envie a imagem no campo \"file\"." }, statusCode: StatusCodes.Status400BadRequest);
            }

            await using var stream = file.OpenReadStream();
            return HttpResults.ToCreatedResult(await covers.UploadAsync(userId, stream, ct), c => c.Url);
        })
        .RequireAuthorization()
        // Bearer-token API with no cookies, so there is no ambient credential for a forged form to ride on.
        .DisableAntiforgery();

        group.MapPost("/from-url", async (CoverFromUrlRequest request, ClaimsPrincipal principal, CoverService covers, CancellationToken ct) =>
            principal.TryGetUserId(out var userId)
                ? HttpResults.ToCreatedResult(await covers.FromUrlAsync(userId, request, ct), c => c.Url)
                : Results.Unauthorized())
        .RequireAuthorization();

        // Anonymous so <img src> works without an auth header; the id is an unguessable UUID.
        group.MapGet("/{id:guid}", (Guid id, HttpContext context, CoverService covers) =>
        {
            var stream = covers.OpenRead(id);
            if (stream is null)
            {
                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.Stream(stream, "image/webp");
        });

        return app;
    }
}
