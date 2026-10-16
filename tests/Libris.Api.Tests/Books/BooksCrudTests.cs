using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Genres;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class BooksCrudTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = LibraryTestData.Json;
    private readonly ApiWebApplicationFactory _factory;

    public BooksCrudTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_Owned_DefaultsNotStartedAndAddedToday()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook(), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var book = await response.ReadAsync<BookDto>();
        book.Status.Should().Be(BookStatus.NotStarted);
        book.Ownership.Should().Be(BookOwnership.Owned);
        book.AddedAt.Should().Be(LibraryTestData.Today);
        book.StartedAt.Should().BeNull();
        book.FinishedAt.Should().BeNull();
        book.CurrentPage.Should().Be(0);
    }

    [Fact]
    public async Task Create_SerializesEnumsAsSnakeCase()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook(), Json);

        var raw = await response.Content.ReadFromJsonAsync<JsonElement>();
        raw.GetProperty("status").GetString().Should().Be("not_started");
        raw.GetProperty("ownership").GetString().Should().Be("owned");
    }

    [Fact]
    public async Task Create_ReadWithPastDates_KeepsDatesAndCountsInThatYear()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Status = BookStatus.Read,
            StartedAt = new DateOnly(2024, 3, 1),
            FinishedAt = new DateOnly(2024, 3, 20),
        });

        book.Status.Should().Be(BookStatus.Read);
        book.StartedAt.Should().Be(new DateOnly(2024, 3, 1));
        book.FinishedAt.Should().Be(new DateOnly(2024, 3, 20));
    }

    [Fact]
    public async Task Create_ReadWithoutDates_LeavesDatesEmpty()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Read });

        book.StartedAt.Should().BeNull();
        book.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Create_WithGenreNames_ReusesExistingCaseInsensitive()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        (await client.PostAsJsonAsync("/v1/genres", new GenreRequest("Terror"))).EnsureSuccessStatusCode();

        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Genres = ["terror", "Gótico", " gótico "] });

        book.Genres.Select(g => g.Name).Should().BeEquivalentTo("Terror", "Gótico");
        var genres = await client.GetFromJsonAsync<List<GenreDto>>("/v1/genres");
        genres!.Select(g => g.Name).Should().BeEquivalentTo("Terror", "Gótico");
    }

    [Fact]
    public async Task Create_IsbnHyphenated_IsStoredAs13Digits()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Isbn = "0-15-144647-4" });

        book.Isbn.Should().Be("9780151446476");
    }

    [Fact]
    public async Task Create_InvalidIsbn_Returns400()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook() with { Isbn = "9788501012796" }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("invalid_isbn");
    }

    [Theory]
    [InlineData(BookOwnership.Owned)]
    [InlineData(BookOwnership.Wishlist)]
    public async Task Create_DuplicateIsbn_Returns409WithTitleInMessage(BookOwnership existingOwnership)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Isbn = "9788501012791",
            Ownership = existingOwnership,
        });

        using var response = await client.PostAsJsonAsync(
            "/v1/books",
            LibraryTestData.NewBook() with { Title = "Outro", Isbn = "978-85-01-01279-1" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("isbn_already_registered");
        body.GetProperty("message").GetString().Should().Contain("O Nome da Rosa");
    }

    [Fact]
    public async Task Create_SameIsbnAsAnotherUser_IsAllowed()
    {
        using var first = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var second = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(first, r => r with { Isbn = "9788501012791" });

        using var response = await second.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook() with { Isbn = "9788501012791" }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Wishlist_WithoutPages_IsAccepted_AndStatusNull()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Ownership = BookOwnership.Wishlist,
            TotalPages = null,
        });

        book.Ownership.Should().Be(BookOwnership.Wishlist);
        book.Status.Should().BeNull();
    }

    [Theory]
    [InlineData("owned-without-pages")]
    [InlineData("wishlist-with-status")]
    [InlineData("finished-before-started")]
    [InlineData("future-date")]
    [InlineData("no-authors")]
    public async Task Create_InvalidRequest_Returns400(string scenario)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var request = scenario switch
        {
            "owned-without-pages" => LibraryTestData.NewBook() with { TotalPages = null },
            "wishlist-with-status" => LibraryTestData.NewBook() with { Ownership = BookOwnership.Wishlist, Status = BookStatus.Reading },
            "finished-before-started" => LibraryTestData.NewBook() with
            {
                Status = BookStatus.Read,
                StartedAt = new DateOnly(2024, 3, 20),
                FinishedAt = new DateOnly(2024, 3, 1),
            },
            "future-date" => LibraryTestData.NewBook() with { Status = BookStatus.Reading, StartedAt = new DateOnly(2026, 10, 17) },
            _ => LibraryTestData.NewBook() with { Authors = [] },
        };

        using var response = await client.PostAsJsonAsync("/v1/books", request, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ReplacesFieldsAndGenres_KeepsStatus()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Genres = ["Mistério"], Status = BookStatus.Reading });

        using var response = await client.PutAsJsonAsync(
            $"/v1/books/{book.Id}",
            new UpdateBookRequest(
                Title: "Il nome della rosa",
                Authors: ["Umberto Eco"],
                Isbn: null,
                TotalPages: 530,
                Publisher: "Bompiani",
                PublishedYear: 1980,
                Synopsis: "Abadia, 1327.",
                CoverId: null,
                Genres: ["Histórico"],
                StartedAt: new DateOnly(2026, 10, 2),
                FinishedAt: null),
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsync<BookDto>();
        updated.Title.Should().Be("Il nome della rosa");
        updated.TotalPages.Should().Be(530);
        updated.Publisher.Should().Be("Bompiani");
        updated.Genres.Select(g => g.Name).Should().Equal("Histórico");
        updated.Status.Should().Be(BookStatus.Reading);
        updated.StartedAt.Should().Be(new DateOnly(2026, 10, 2));
    }

    [Fact]
    public async Task Delete_Returns204_AndGetReturns404()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var delete = await client.DeleteAsync(new Uri($"/v1/books/{book.Id}", UriKind.Relative));
        using var get = await client.GetAsync(new Uri($"/v1/books/{book.Id}", UriKind.Relative));

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OtherUsersBook_Returns404()
    {
        using var owner = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var stranger = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(owner);
        var wish = await LibraryTestData.CreateBookAsync(owner, r => r with { Ownership = BookOwnership.Wishlist });

        using var get = await stranger.GetAsync(new Uri($"/v1/books/{book.Id}", UriKind.Relative));
        using var put = await stranger.PutAsJsonAsync($"/v1/books/{book.Id}", ToUpdate(book), Json);
        using var status = await stranger.PatchAsJsonAsync(
            $"/v1/books/{book.Id}/status", new ChangeStatusRequest(BookStatus.Reading, LibraryTestData.Today), Json);
        using var acquire = await stranger.PostAsJsonAsync(
            $"/v1/books/{wish.Id}/acquire", new AcquireRequest(LibraryTestData.Today, 300), Json);
        using var delete = await stranger.DeleteAsync(new Uri($"/v1/books/{book.Id}", UriKind.Relative));

        new[] { get, put, status, acquire, delete }.Select(r => r.StatusCode)
            .Should().OnlyContain(s => s == HttpStatusCode.NotFound);
        (await owner.GetAsync(new Uri($"/v1/books/{book.Id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static UpdateBookRequest ToUpdate(BookDto book) => new(
        book.Title, book.Authors, book.Isbn, book.TotalPages, book.Publisher, book.PublishedYear,
        book.Synopsis, book.CoverId, [.. book.Genres.Select(g => g.Name)], book.StartedAt, book.FinishedAt);
}
