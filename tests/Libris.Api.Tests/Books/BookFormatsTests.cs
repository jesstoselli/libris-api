using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class BookFormatsTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = LibraryTestData.Json;
    private readonly ApiWebApplicationFactory _factory;

    public BookFormatsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithoutFormats_IsPhysical()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client);

        book.Formats.Should().Equal(BookFormat.Physical);
    }

    [Fact]
    public async Task Create_DigitalAndBoth_AreKept_PhysicalFirst()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var digital = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Digital", Formats = [BookFormat.Digital] });
        var both = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Ambos", Formats = [BookFormat.Digital, BookFormat.Physical] });

        digital.Formats.Should().Equal(BookFormat.Digital);
        both.Formats.Should().Equal(BookFormat.Physical, BookFormat.Digital);
    }

    [Fact]
    public async Task Formats_TravelAsSnakeCaseStrings()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook() with { Formats = [BookFormat.Digital] }, Json);

        var raw = await response.Content.ReadFromJsonAsync<JsonElement>();
        raw.GetProperty("formats").EnumerateArray().Select(f => f.GetString()).Should().Equal("digital");
    }

    [Fact]
    public async Task Create_WithNoFormat_IsRejected()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook() with { Formats = [] }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadFromJsonAsync<JsonElement>();
        raw.GetProperty("message").GetString().Should().Be("Escolha pelo menos um formato.");
    }

    [Fact]
    public async Task Update_WithoutFormats_KeepsTheCurrentOnes()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Formats = [BookFormat.Digital] });

        using var response = await client.PutAsJsonAsync($"/v1/books/{book.Id}", UpdateOf(book), Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<BookDto>()).Formats.Should().Equal(BookFormat.Digital);
    }

    [Fact]
    public async Task Update_ChangesFormats()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var response = await client.PutAsJsonAsync($"/v1/books/{book.Id}", UpdateOf(book) with { Formats = [BookFormat.Physical, BookFormat.Digital] }, Json);

        (await response.ReadAsync<BookDto>()).Formats.Should().Equal(BookFormat.Physical, BookFormat.Digital);
    }

    [Fact]
    public async Task List_FormatFilter_MatchesBooksThatHaveThatFormat()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Papel" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Tela", Formats = [BookFormat.Digital] });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Ambos", Formats = [BookFormat.Physical, BookFormat.Digital] });

        var digital = await client.GetFromJsonAsync<List<BookDto>>("/v1/books?format=digital", Json);
        var physical = await client.GetFromJsonAsync<List<BookDto>>("/v1/books?format=physical", Json);

        digital!.Select(b => b.Title).Should().Equal("Ambos", "Tela");
        physical!.Select(b => b.Title).Should().Equal("Ambos", "Papel");
    }

    [Fact]
    public async Task List_UnknownFormat_IsRejected()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.GetAsync("/v1/books?format=vinyl");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Acquire_KeepsTheWishlistFormat()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var wish = await LibraryTestData.CreateBookAsync(client, r => r with { Ownership = BookOwnership.Wishlist, Status = null, Formats = [BookFormat.Digital] });

        using var response = await client.PostAsJsonAsync($"/v1/books/{wish.Id}/acquire", new AcquireRequest(LibraryTestData.Today, null), Json);

        (await response.ReadAsync<BookDto>()).Formats.Should().Equal(BookFormat.Digital);
    }

    private static UpdateBookRequest UpdateOf(BookDto book) => new(
        book.Title, book.Authors, book.Isbn, book.TotalPages, book.Publisher, book.PublishedYear, book.Synopsis,
        book.CoverId, [.. book.Genres.Select(g => g.Name)], book.StartedAt, book.FinishedAt);
}
