using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;

namespace Libris.Api.Tests.Books;

public sealed class BookLanguageTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = LibraryTestData.Json;
    private readonly ApiWebApplicationFactory _factory;

    public BookLanguageTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithoutLanguage_IsPortuguese()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        (await LibraryTestData.CreateBookAsync(client)).Language.Should().Be("pt");
    }

    [Fact]
    public async Task Create_KeepsTheLanguage_InLowerCase()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        (await LibraryTestData.CreateBookAsync(client, r => r with { Language = "EN" })).Language.Should().Be("en");
    }

    [Theory]
    [InlineData("english")]
    [InlineData("e")]
    [InlineData("1a")]
    public async Task Create_RejectsAnythingButATwoLetterCode(string language)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook() with { Language = language }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString().Should().Be("Idioma inválido.");
    }

    [Fact]
    public async Task Update_WithoutLanguage_KeepsIt_AndWithLanguage_ChangesIt()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Language = "fr" });

        using (var kept = await client.PutAsJsonAsync($"/v1/books/{book.Id}", UpdateOf(book), Json))
        {
            (await kept.ReadAsync<BookDto>()).Language.Should().Be("fr");
        }

        using var changed = await client.PutAsJsonAsync($"/v1/books/{book.Id}", UpdateOf(book) with { Language = "it" }, Json);
        (await changed.ReadAsync<BookDto>()).Language.Should().Be("it");
    }

    [Fact]
    public async Task List_LanguageFilter()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Rosa" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Rose", Language = "en" });

        var english = await client.GetFromJsonAsync<List<BookDto>>("/v1/books?language=en", Json);

        english!.Select(b => b.Title).Should().Equal("Rose");
    }

    [Fact]
    public async Task List_InvalidLanguageFilter_IsRejected()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.GetAsync("/v1/books?language=english");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static UpdateBookRequest UpdateOf(BookDto book) => new(
        book.Title, book.Authors, book.Isbn, book.TotalPages, book.Publisher, book.PublishedYear, book.Synopsis,
        book.CoverId, [.. book.Genres.Select(g => g.Name)], book.StartedAt, book.FinishedAt);
}
