using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class BookAcquireTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public BookAcquireTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Acquire_MovesToOwnedNotStarted_AddedToday()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await WishAsync(client, totalPages: 380);

        using var response = await AcquireAsync(client, book.Id, totalPages: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var owned = await response.ReadAsync<BookDto>();
        owned.Ownership.Should().Be(BookOwnership.Owned);
        owned.Status.Should().Be(BookStatus.NotStarted);
        owned.AddedAt.Should().Be(new DateOnly(2026, 10, 20));
    }

    [Fact]
    public async Task Acquire_WithoutPages_Returns400TotalPagesRequired()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await WishAsync(client, totalPages: null);

        using var response = await AcquireAsync(client, book.Id, totalPages: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("total_pages_required");
    }

    [Fact]
    public async Task Acquire_WithPagesInRequest_Succeeds()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await WishAsync(client, totalPages: null);

        using var response = await AcquireAsync(client, book.Id, totalPages: 380);

        (await response.ReadAsync<BookDto>()).TotalPages.Should().Be(380);
    }

    [Fact]
    public async Task Acquire_AlreadyOwned_Returns409()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var response = await AcquireAsync(client, book.Id, totalPages: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static Task<BookDto> WishAsync(HttpClient client, int? totalPages) =>
        LibraryTestData.CreateBookAsync(client, r => r with { Ownership = BookOwnership.Wishlist, TotalPages = totalPages });

    private static Task<HttpResponseMessage> AcquireAsync(HttpClient client, Guid id, int? totalPages) =>
        client.PostAsJsonAsync($"/v1/books/{id}/acquire", new AcquireRequest(new DateOnly(2026, 10, 20), totalPages), LibraryTestData.Json);
}
