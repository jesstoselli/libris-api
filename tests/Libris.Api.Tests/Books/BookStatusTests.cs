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
