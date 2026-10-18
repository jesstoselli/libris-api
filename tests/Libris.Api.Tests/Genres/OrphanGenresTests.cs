using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Genres;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Genres;

public sealed class OrphanGenresTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public OrphanGenresTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Update_RemovingTheLastBooksGenre_DeletesIt()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Genres = ["Terror", "Gótico"] });

        using var response = await client.PutAsJsonAsync($"/v1/books/{book.Id}", UpdateOf(book) with { Genres = ["Terror"] }, LibraryTestData.Json);
        response.EnsureSuccessStatusCode();

        (await GenreNamesAsync(client)).Should().Equal("Terror");
    }

    [Fact]
    public async Task Update_GenreStillUsedByAnotherBook_Stays()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Drácula", Genres = ["Terror"] });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Carmilla", Genres = ["Terror"] });

        using var response = await client.PutAsJsonAsync($"/v1/books/{book.Id}", UpdateOf(book) with { Genres = [] }, LibraryTestData.Json);
        response.EnsureSuccessStatusCode();

        (await GenreNamesAsync(client)).Should().Equal("Terror");
    }

    [Fact]
    public async Task Delete_LastBookOfAGenre_DeletesIt()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Genres = ["Terror"] });

        using var response = await client.DeleteAsync($"/v1/books/{book.Id}");
        response.EnsureSuccessStatusCode();

        (await GenreNamesAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_GenreKeptByAWishlistBook_Stays()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var owned = await LibraryTestData.CreateBookAsync(client, r => r with { Genres = ["Terror"] });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Desejo", Ownership = BookOwnership.Wishlist, Status = null, Genres = ["Terror"] });

        using var response = await client.DeleteAsync($"/v1/books/{owned.Id}");
        response.EnsureSuccessStatusCode();

        (await GenreNamesAsync(client)).Should().Equal("Terror");
    }

    [Fact]
    public async Task Delete_LeavesOtherUsersGenresAlone()
    {
        using var other = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var created = await other.PostAsJsonAsync("/v1/genres", new GenreRequest("Poesia"));
        created.EnsureSuccessStatusCode();
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Genres = ["Terror"] });

        using var response = await client.DeleteAsync($"/v1/books/{book.Id}");
        response.EnsureSuccessStatusCode();

        (await GenreNamesAsync(other)).Should().Equal("Poesia");
    }

    private static async Task<List<string>> GenreNamesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<GenreDto>>("/v1/genres"))!.Select(g => g.Name).ToList();

    private static UpdateBookRequest UpdateOf(BookDto book) => new(
        book.Title, book.Authors, book.Isbn, book.TotalPages, book.Publisher, book.PublishedYear, book.Synopsis,
        book.CoverId, [.. book.Genres.Select(g => g.Name)], book.StartedAt, book.FinishedAt);
}
