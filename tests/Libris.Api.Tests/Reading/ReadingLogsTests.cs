using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Reading;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Reading;

public sealed class ReadingLogsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public ReadingLogsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FirstLog_OnNotStarted_StartsReadingWithLogDate()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var response = await LogAsync(client, book.Id, 20, new DateOnly(2026, 10, 2));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.ReadAsync<ReadingLogResult>();
        result.Book.Status.Should().Be(BookStatus.Reading);
        result.Book.StartedAt.Should().Be(new DateOnly(2026, 10, 2));
        result.Book.CurrentPage.Should().Be(20);
        result.Logs.Should().ContainSingle();
    }

    [Fact]
    public async Task LogAtLastPage_MarksReadWithLogDate()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { TotalPages = 246, Status = BookStatus.Reading });

        using var response = await LogAsync(client, book.Id, 246, new DateOnly(2026, 10, 8));

        var result = await response.ReadAsync<ReadingLogResult>();
        result.Book.Status.Should().Be(BookStatus.Read);
        result.Book.FinishedAt.Should().Be(new DateOnly(2026, 10, 8));
        result.Book.Percent.Should().Be(100);
    }

    [Fact]
    public async Task List_ReturnsNewestFirst_WithFromAndPagesRead()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);
        (await LogAsync(client, book.Id, 20, new DateOnly(2026, 10, 2))).EnsureSuccessStatusCode();
        (await LogAsync(client, book.Id, 86, new DateOnly(2026, 10, 11))).EnsureSuccessStatusCode();
        (await LogAsync(client, book.Id, 51, new DateOnly(2026, 10, 6))).EnsureSuccessStatusCode();

        var logs = (await client.GetFromJsonAsync<List<ReadingLogDto>>($"/v1/books/{book.Id}/logs", LibraryTestData.Json))!;

        logs.Select(l => l.Page).Should().Equal(86, 51, 20);
        logs[0].Should().BeEquivalentTo(new { Page = 86, FromPage = 51, PagesRead = 35 });
        logs[2].Should().BeEquivalentTo(new { Page = 20, FromPage = 0, PagesRead = 20 });
    }

    [Fact]
    public async Task List_AfterBackwardsCorrection_FromIsPreviousPage()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);
        (await LogAsync(client, book.Id, 140, new DateOnly(2026, 10, 14))).EnsureSuccessStatusCode();
        (await LogAsync(client, book.Id, 120, new DateOnly(2026, 10, 15))).EnsureSuccessStatusCode();

        var logs = (await client.GetFromJsonAsync<List<ReadingLogDto>>($"/v1/books/{book.Id}/logs", LibraryTestData.Json))!;

        logs[0].Should().BeEquivalentTo(new { Page = 120, FromPage = 140, PagesRead = 0 });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(513)]
    public async Task Log_PageOutOfRange_Returns400(int page)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);

        using var response = await LogAsync(client, book.Id, page, LibraryTestData.Today);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Log_OnWishlistBook_Returns400()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Ownership = BookOwnership.Wishlist, TotalPages = 300 });

        using var response = await LogAsync(client, book.Id, 10, LibraryTestData.Today);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_LastLog_RecalculatesProgress_KeepsStatus()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { TotalPages = 246 });
        (await LogAsync(client, book.Id, 100, new DateOnly(2026, 10, 2))).EnsureSuccessStatusCode();
        using var last = await LogAsync(client, book.Id, 246, new DateOnly(2026, 10, 8));
        var lastLog = (await last.ReadAsync<ReadingLogResult>()).Logs[0];

        using var response = await client.DeleteAsync(new Uri($"/v1/books/{book.Id}/logs/{lastLog.Id}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadAsync<ReadingLogResult>();
        result.Book.CurrentPage.Should().Be(100);
        result.Book.Status.Should().Be(BookStatus.Read);
        result.Book.FinishedAt.Should().Be(new DateOnly(2026, 10, 8));
    }

    [Fact]
    public async Task Update_ChangesPageAndRecalculates()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client);
        using var created = await LogAsync(client, book.Id, 140, new DateOnly(2026, 10, 14));
        var log = (await created.ReadAsync<ReadingLogResult>()).Logs[0];

        using var response = await client.PutAsJsonAsync(
            $"/v1/books/{book.Id}/logs/{log.Id}", new ReadingLogRequest(new DateOnly(2026, 10, 13), 150), LibraryTestData.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadAsync<ReadingLogResult>();
        result.Book.CurrentPage.Should().Be(150);
        result.Logs.Single().Date.Should().Be(new DateOnly(2026, 10, 13));
    }

    [Fact]
    public async Task Log_OnOtherUsersBook_Returns404()
    {
        using var owner = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var stranger = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(owner);
        using var created = await LogAsync(owner, book.Id, 10, LibraryTestData.Today);
        var log = (await created.ReadAsync<ReadingLogResult>()).Logs[0];

        using var post = await LogAsync(stranger, book.Id, 20, LibraryTestData.Today);
        using var list = await stranger.GetAsync(new Uri($"/v1/books/{book.Id}/logs", UriKind.Relative));
        using var put = await stranger.PutAsJsonAsync(
            $"/v1/books/{book.Id}/logs/{log.Id}", new ReadingLogRequest(LibraryTestData.Today, 30), LibraryTestData.Json);
        using var delete = await stranger.DeleteAsync(new Uri($"/v1/books/{book.Id}/logs/{log.Id}", UriKind.Relative));

        new[] { post, list, put, delete }.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_LogOfAnotherBook_Returns404()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var rosa = await LibraryTestData.CreateBookAsync(client);
        var dracula = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Drácula", TotalPages = 418 });
        using var created = await LogAsync(client, rosa.Id, 10, LibraryTestData.Today);
        var log = (await created.ReadAsync<ReadingLogResult>()).Logs[0];

        using var response = await client.PutAsJsonAsync(
            $"/v1/books/{dracula.Id}/logs/{log.Id}", new ReadingLogRequest(LibraryTestData.Today, 30), LibraryTestData.Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> LogAsync(HttpClient client, Guid bookId, int page, DateOnly date) =>
        client.PostAsJsonAsync($"/v1/books/{bookId}/logs", new ReadingLogRequest(date, page), LibraryTestData.Json);
}
