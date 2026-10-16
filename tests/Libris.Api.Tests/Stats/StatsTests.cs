using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Reading;
using Libris.Application.Dtos.Stats;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Stats;

public sealed class StatsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public StatsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Year_SumsPagesByLogDate_AcrossBooks()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var dracula = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Drácula", TotalPages = 418 });
        var rosa = await LibraryTestData.CreateBookAsync(client);
        await LogAsync(client, dracula.Id, 100, new DateOnly(2026, 10, 3));
        await LogAsync(client, dracula.Id, 214, new DateOnly(2026, 10, 15));
        await LogAsync(client, rosa.Id, 40, new DateOnly(2026, 9, 28));

        var year = await YearAsync(client, 2026);

        year.Months.Should().HaveCount(12);
        year.Months[9].PagesRead.Should().Be(214);
        year.Months[8].PagesRead.Should().Be(40);
        year.PagesRead.Should().Be(254);
    }

    [Fact]
    public async Task Year_DeltaCrossingMonths_CountsInLogMonth()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var rosa = await LibraryTestData.CreateBookAsync(client);
        await LogAsync(client, rosa.Id, 40, new DateOnly(2026, 9, 28));
        await LogAsync(client, rosa.Id, 140, new DateOnly(2026, 10, 2));

        var year = await YearAsync(client, 2026);

        year.Months[8].PagesRead.Should().Be(40);
        year.Months[9].PagesRead.Should().Be(100);
    }

    [Fact]
    public async Task Year_CorrectionBackwards_DoesNotSubtract()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var rosa = await LibraryTestData.CreateBookAsync(client);
        await LogAsync(client, rosa.Id, 140, new DateOnly(2026, 10, 14));
        await LogAsync(client, rosa.Id, 120, new DateOnly(2026, 10, 15));

        var year = await YearAsync(client, 2026);

        year.Months[9].PagesRead.Should().Be(140);
    }

    [Fact]
    public async Task Year_OldBookWithoutLogs_CountsFinishedButNoPages()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Status = BookStatus.Read,
            StartedAt = new DateOnly(2024, 3, 1),
            FinishedAt = new DateOnly(2024, 3, 20),
        });

        var old = await YearAsync(client, 2024);
        var current = await YearAsync(client, 2026);

        old.BooksFinished.Should().Be(1);
        old.Months[2].BooksFinished.Should().Be(1);
        old.Months[2].PagesRead.Should().Be(0);
        current.BooksFinished.Should().Be(0);
    }

    [Fact]
    public async Task Year_AbandonedWithFinishedDate_IsNotCountedAsFinished()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Status = BookStatus.Abandoned,
            StartedAt = new DateOnly(2026, 2, 1),
            FinishedAt = new DateOnly(2026, 3, 1),
        });

        (await YearAsync(client, 2026)).BooksFinished.Should().Be(0);
    }

    [Fact]
    public async Task Year_ReadingNow_IgnoresYear()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Reading });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Drácula", Status = BookStatus.Reading });

        (await YearAsync(client, 2019)).ReadingNow.Should().Be(2);
    }

    [Fact]
    public async Task Month_Detail_ListsFinishedAndProgressed()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var frankenstein = await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Title = "Frankenstein", Authors = ["Mary Shelley"], TotalPages = 280,
        });
        var dracula = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Drácula", TotalPages = 418 });
        await LogAsync(client, frankenstein.Id, 148, new DateOnly(2026, 9, 30));
        await LogAsync(client, frankenstein.Id, 280, new DateOnly(2026, 10, 8));
        await LogAsync(client, dracula.Id, 214, new DateOnly(2026, 10, 15));

        var month = (await client.GetFromJsonAsync<MonthDetailDto>("/v1/stats/2026/10", LibraryTestData.Json))!;

        month.PagesRead.Should().Be(346);
        month.Finished.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            BookId = frankenstein.Id,
            Title = "Frankenstein",
            FinishedAt = new DateOnly(2026, 10, 8),
        });
        month.Progressed.Select(p => (p.Title, p.PagesRead)).Should().Equal(("Drácula", 214), ("Frankenstein", 132));
    }

    [Fact]
    public async Task Stats_OtherUsersData_IsNotCounted()
    {
        using var owner = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        using var stranger = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(owner, r => r with { Status = BookStatus.Reading });
        await LogAsync(owner, book.Id, 512, new DateOnly(2026, 10, 1));

        var year = await YearAsync(stranger, 2026);

        year.PagesRead.Should().Be(0);
        year.BooksFinished.Should().Be(0);
        year.ReadingNow.Should().Be(0);
    }

    [Theory]
    [InlineData("/v1/stats/1899")]
    [InlineData("/v1/stats/2101")]
    [InlineData("/v1/stats/2026/0")]
    [InlineData("/v1/stats/2026/13")]
    public async Task OutOfRange_Returns400(string url)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<YearStatsDto> YearAsync(HttpClient client, int year) =>
        (await client.GetFromJsonAsync<YearStatsDto>($"/v1/stats/{year}", LibraryTestData.Json))!;

    private static async Task LogAsync(HttpClient client, Guid bookId, int page, DateOnly date)
    {
        using var response = await client.PostAsJsonAsync($"/v1/books/{bookId}/logs", new ReadingLogRequest(date, page), LibraryTestData.Json);
        response.EnsureSuccessStatusCode();
    }
}
