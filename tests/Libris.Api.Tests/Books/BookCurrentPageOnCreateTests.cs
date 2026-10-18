using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Reading;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

/// <summary>A book registered mid-read starts with its progress: the page becomes the first reading log.</summary>
public sealed class BookCurrentPageOnCreateTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = LibraryTestData.Json;
    private readonly ApiWebApplicationFactory _factory;

    public BookCurrentPageOnCreateTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reading_WithCurrentPage_StartsWithALogOnTheStartDate()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Status = BookStatus.Reading,
            StartedAt = new DateOnly(2026, 10, 1),
            CurrentPage = 128,
        });

        book.CurrentPage.Should().Be(128);
        book.Percent.Should().Be(25);
        book.Status.Should().Be(BookStatus.Reading);
        var logs = await client.GetFromJsonAsync<List<ReadingLogDto>>($"/v1/books/{book.Id}/logs", Json);
        logs!.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Date = new DateOnly(2026, 10, 1), Page = 128 });
    }

    [Fact]
    public async Task CurrentPage_WithoutAStartDate_IsDatedToday()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Abandoned, CurrentPage = 40 });

        book.Status.Should().Be(BookStatus.Abandoned);
        var logs = await client.GetFromJsonAsync<List<ReadingLogDto>>($"/v1/books/{book.Id}/logs", Json);
        logs!.Single().Date.Should().Be(LibraryTestData.Today);
    }

    [Fact]
    public async Task WithoutCurrentPage_NoLogIsCreated()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Reading });

        (await client.GetFromJsonAsync<List<ReadingLogDto>>($"/v1/books/{book.Id}/logs", Json))!.Should().BeEmpty();
    }

    [Theory]
    [InlineData(BookStatus.NotStarted)]
    [InlineData(BookStatus.Read)]
    public async Task CurrentPage_OnlyForBooksBeingReadOrAbandoned(BookStatus status)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook() with { Status = status, CurrentPage = 10 }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()
            .Should().Be("A página lida só vale para livros em leitura ou abandonados.");
    }

    [Fact]
    public async Task CurrentPage_MustFitTheBook()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", LibraryTestData.NewBook() with { Status = BookStatus.Reading, CurrentPage = 9999 }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()
            .Should().Be("A página precisa estar entre 1 e 512.");
    }
}
