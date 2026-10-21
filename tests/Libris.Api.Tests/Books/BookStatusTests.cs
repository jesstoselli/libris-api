using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class BookStatusTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public BookStatusTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Patch_ToRead_FillsFinishedWithRequestToday()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Reading });

        using var response = await PatchAsync(client, book.Id, BookStatus.Read, new DateOnly(2026, 10, 15));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsync<BookDto>();
        updated.Status.Should().Be(BookStatus.Read);
        updated.FinishedAt.Should().Be(new DateOnly(2026, 10, 15));
    }

    [Fact]
    public async Task Patch_ToReadWithoutDate_LeavesFinishedEmpty()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var response = await client.PatchAsJsonAsync(
            $"/v1/books/{book.Id}/status", new ChangeStatusRequest(BookStatus.Read, LibraryTestData.Today, WithoutDate: true), LibraryTestData.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsync<BookDto>();
        updated.Status.Should().Be(BookStatus.Read);
        updated.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Patch_ToReadOnAKnownDate_UsesThatDate()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var response = await client.PatchAsJsonAsync(
            $"/v1/books/{book.Id}/status", new ChangeStatusRequest(BookStatus.Read, LibraryTestData.Today, FinishedAt: new DateOnly(2019, 7, 2)), LibraryTestData.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<BookDto>()).FinishedAt.Should().Be(new DateOnly(2019, 7, 2));
    }

    [Fact]
    public async Task Patch_ToReadOnAFutureDate_IsRefused()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var response = await client.PatchAsJsonAsync(
            $"/v1/books/{book.Id}/status", new ChangeStatusRequest(BookStatus.Read, LibraryTestData.Today, FinishedAt: LibraryTestData.Today.AddDays(1)), LibraryTestData.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_AsRead_KeepsBothDates()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var created = await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Status = BookStatus.Read,
            StartedAt = new DateOnly(2026, 9, 1),
            FinishedAt = new DateOnly(2026, 9, 20),
        });
        var read = await client.GetFromJsonAsync<BookDto>($"/v1/books/{created.Id}", LibraryTestData.Json);

        created.StartedAt.Should().Be(new DateOnly(2026, 9, 1));
        created.FinishedAt.Should().Be(new DateOnly(2026, 9, 20));
        read!.StartedAt.Should().Be(new DateOnly(2026, 9, 1));
        read.FinishedAt.Should().Be(new DateOnly(2026, 9, 20));
        read.Status.Should().Be(BookStatus.Read);
    }

    [Fact]
    public async Task Update_AddingTheStartToAReadBook_KeepsBothDates()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Read, FinishedAt = new DateOnly(2009, 1, 29) });

        var request = new UpdateBookRequest(
            book.Title, book.Authors, book.Isbn, book.TotalPages, book.Publisher, book.PublishedYear, book.Synopsis,
            book.CoverId, [.. book.Genres.Select(g => g.Name)], new DateOnly(2009, 1, 10), new DateOnly(2009, 1, 29));
        using var response = await client.PutAsJsonAsync($"/v1/books/{book.Id}", request, LibraryTestData.Json);
        var read = await client.GetFromJsonAsync<BookDto>($"/v1/books/{book.Id}", LibraryTestData.Json);

        response.EnsureSuccessStatusCode();
        read!.StartedAt.Should().Be(new DateOnly(2009, 1, 10));
        read.FinishedAt.Should().Be(new DateOnly(2009, 1, 29));
    }

    [Fact]
    public async Task Patch_ToNotStarted_ClearsDates()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Status = BookStatus.Read,
            StartedAt = new DateOnly(2026, 10, 1),
            FinishedAt = new DateOnly(2026, 10, 9),
        });

        using var response = await PatchAsync(client, book.Id, BookStatus.NotStarted, LibraryTestData.Today);

        var updated = await response.ReadAsync<BookDto>();
        updated.StartedAt.Should().BeNull();
        updated.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Patch_OnWishlist_Returns400()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Ownership = BookOwnership.Wishlist });

        using var response = await PatchAsync(client, book.Id, BookStatus.Reading, LibraryTestData.Today);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, Guid id, BookStatus status, DateOnly today) =>
        client.PatchAsJsonAsync($"/v1/books/{id}/status", new ChangeStatusRequest(status, today), LibraryTestData.Json);
}
