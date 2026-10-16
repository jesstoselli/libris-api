using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Auth;

namespace Libris.Api.Tests.Auth;

public sealed class LoginRateLimitTests : IAsyncLifetime
{
    private readonly ApiWebApplicationFactory _factory = ApiWebApplicationFactory.Create(
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Auth:LoginPermitPerMinute"] = "3",
        });

    public Task InitializeAsync() => ((IAsyncLifetime)_factory).InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Login_OverPerIpLimit_Returns429()
    {
        using var client = _factory.CreateClient();
        var email = AuthTestClient.NewEmail();
        await AuthTestClient.SignupAsync(client, email);
        var wrong = new LoginRequest(email, "wrong-password-1");

        for (var i = 0; i < 3; i++)
        {
            using var rejected = await client.PostAsJsonAsync("/v1/auth/login", wrong);
            rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var limited = await client.PostAsJsonAsync("/v1/auth/login", wrong);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
