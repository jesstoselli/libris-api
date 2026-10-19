using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class AuthorsListTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthorsListTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_EveryAuthorOnce_FromTheAcervoAndTheWishlist_ByName()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "A", Authors = ["Umberto Eco"] });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "B", Authors = ["Brian Christian", "Tom Griffiths"] });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "C", Authors = ["Umberto Eco"] });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "D", Authors = ["Émile Zola"], Ownership = BookOwnership.Wishlist, Status = null });

        var authors = await client.GetFromJsonAsync<List<string>>("/v1/authors");

        authors.Should().Equal("Brian Christian", "Émile Zola", "Tom Griffiths", "Umberto Eco");
    }

    [Fact]
    public async Task List_OnlyTheUsersOwnAuthors()
    {
        using var other = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(other, r => r with { Authors = ["Mary Shelley"] });
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var authors = await client.GetFromJsonAsync<List<string>>("/v1/authors");

        authors.Should().BeEmpty();
    }

    [Fact]
    public async Task List_NeedsSignIn()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/authors", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
