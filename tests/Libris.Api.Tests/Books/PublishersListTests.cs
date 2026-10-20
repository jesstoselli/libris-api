using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class PublishersListTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public PublishersListTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_EveryPublisherOnce_FromTheAcervoAndTheWishlist_ByName()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "A", Publisher = "Rocco" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "B", Publisher = "Companhia das Letras" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "C", Publisher = "Rocco" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "D", Publisher = null });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "E", Publisher = "Ática", Ownership = BookOwnership.Wishlist, Status = null });

        var publishers = await client.GetFromJsonAsync<List<string>>("/v1/publishers");

        publishers.Should().Equal("Ática", "Companhia das Letras", "Rocco");
    }

    [Fact]
    public async Task List_OnlyTheUsersOwnPublishers()
    {
        using var other = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(other, r => r with { Publisher = "Record" });
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var publishers = await client.GetFromJsonAsync<List<string>>("/v1/publishers");

        publishers.Should().BeEmpty();
    }

    [Fact]
    public async Task List_NeedsSignIn()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/publishers", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
