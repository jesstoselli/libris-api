using Libris.Application.Dtos.Auth;
using Libris.Application.UseCases.Auth;

namespace Libris.Api.Endpoints;

/// <summary>Tags the login endpoint for the per-IP limiter, which then matches /login, /login/ and any casing alike.</summary>
public sealed class LoginEndpointMarker
{
    public static LoginEndpointMarker Instance { get; } = new();

    private LoginEndpointMarker()
    {
    }
}

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth").WithTags("Auth").RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/signup", async (SignupRequest request, AuthService auth, CancellationToken ct) =>
            HttpResults.ToHttpResult(await auth.SignupAsync(request, ct)));

        group.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
            HttpResults.ToHttpResult(await auth.LoginAsync(request, ct)))
            .WithMetadata(LoginEndpointMarker.Instance);

        group.MapPost("/refresh", async (RefreshTokenRequest request, AuthService auth, CancellationToken ct) =>
            HttpResults.ToHttpResult(await auth.RefreshAsync(request, ct)));

        group.MapPost("/logout", async (LogoutRequest request, AuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LogoutAsync(request, ct);
            return result.IsSuccess ? Results.NoContent() : HttpResults.ToHttpResult(result);
        });

        return app;
    }
}
