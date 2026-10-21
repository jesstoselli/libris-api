using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class PublishersManageTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public PublishersManageTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Summary_CountsAcervoAndWishlistApart_ByName()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "A", Publisher = "Rocco" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "B", Publisher = "Rocco" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "C", Publisher = "Rocco", Ownership = BookOwnership.Wishlist, Status = null });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "D", Publisher = "Ática" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "E", Publisher = null });

        var summary = await client.GetFromJsonAsync<List<PublisherDto>>("/v1/publishers/summary", LibraryTestData.Json);

        summary.Should().Equal(new PublisherDto("Ática", 1, 0), new PublisherDto("Rocco", 2, 1));
    }

    [Fact]
    public async Task Rename_ChangesEveryBookWithThatSpelling_AndMergesIntoAnExistingOne()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var a = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "A", Publisher = "Editora Rocco Ltda." });
        var b = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "B", Publisher = "Editora Rocco Ltda.", Ownership = BookOwnership.Wishlist, Status = null });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "C", Publisher = "Rocco" });
        var other = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "D", Publisher = "Ática" });

        using var response = await client.PutAsJsonAsync("/v1/publishers", new RenamePublisherRequest("Editora Rocco Ltda.", "  Rocco "), LibraryTestData.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<RenamePublisherResult>()).Updated.Should().Be(2);
        (await GetAsync(client, a.Id)).Publisher.Should().Be("Rocco");
        (await GetAsync(client, b.Id)).Publisher.Should().Be("Rocco");
        (await GetAsync(client, other.Id)).Publisher.Should().Be("Ática");
        var summary = await client.GetFromJsonAsync<List<PublisherDto>>("/v1/publishers/summary", LibraryTestData.Json);
        summary.Should().Equal(new PublisherDto("Ática", 1, 0), new PublisherDto("Rocco", 2, 1));
    }

    [Fact]
    public async Task Rename_ToBlankOrTooLong_IsRefused()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Publisher = "Rocco" });

        using var blank = await client.PutAsJsonAsync("/v1/publishers", new RenamePublisherRequest("Rocco", "  "), LibraryTestData.Json);
        using var tooLong = await client.PutAsJsonAsync("/v1/publishers", new RenamePublisherRequest("Rocco", new string('x', 201)), LibraryTestData.Json);

        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rename_LeavesOtherUsersBooksAlone()
    {
        using var other = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var theirs = await LibraryTestData.CreateBookAsync(other, r => r with { Publisher = "Rocco" });
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PutAsJsonAsync("/v1/publishers", new RenamePublisherRequest("Rocco", "Outra"), LibraryTestData.Json);

        (await response.ReadAsync<RenamePublisherResult>()).Updated.Should().Be(0);
        (await GetAsync(other, theirs.Id)).Publisher.Should().Be("Rocco");
    }

    [Fact]
    public async Task List_FiltersByPublisher_WrittenExactly()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "A", Publisher = "Rocco" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "B", Publisher = "Rocco Jovens Leitores" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "C", Publisher = "Ática" });

        var books = await client.GetFromJsonAsync<List<BookDto>>("/v1/books?publisher=Rocco", LibraryTestData.Json);

        books!.Select(b => b.Title).Should().Equal("A");
    }

    private static async Task<BookDto> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<BookDto>($"/v1/books/{id}", LibraryTestData.Json))!;
}
