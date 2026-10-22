using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Reading;
using Libris.Application.Dtos.Stats;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Books;

public sealed class SeriesAndRatingTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public SeriesAndRatingTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static Func<CreateBookRequest, CreateBookRequest> InSeries(string title, string series, decimal? number, BookOwnership ownership = BookOwnership.Owned) =>
        r => r with
        {
            Title = title,
            Series = new BookSeriesInput(series, number),
            Ownership = ownership,
            Status = ownership == BookOwnership.Owned ? r.Status : null,
        };

    [Fact]
    public async Task Create_KeepsSeriesAndNumber_TrimmingTheName()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        var book = await LibraryTestData.CreateBookAsync(client, InSeries("Tudo É Vaidade", "  Bórgia ", 4));

        book.Series.Should().Be("Bórgia");
        book.SeriesNumber.Should().Be(4);
    }

    [Fact]
    public async Task Update_WithoutSeries_KeepsIt_AndABlankNameRemovesIt()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, InSeries("Vol. 1", "1Q84", 1));
        UpdateBookRequest Update(BookSeriesInput? series) => new(
            book.Title, book.Authors, book.Isbn, book.TotalPages, book.Publisher, book.PublishedYear, book.Synopsis,
            book.CoverId, book.Genres.Select(g => g.Name).ToList(), book.StartedAt, book.FinishedAt, Series: series);

        // An older app knows nothing about series, so its edits must not erase one.
        using (var kept = await client.PutAsJsonAsync($"/v1/books/{book.Id}", Update(null), LibraryTestData.Json))
        {
            (await kept.ReadAsync<BookDto>()).Series.Should().Be("1Q84");
        }

        using var removed = await client.PutAsJsonAsync($"/v1/books/{book.Id}", Update(new BookSeriesInput(" ", 3)), LibraryTestData.Json);
        var after = await removed.ReadAsync<BookDto>();
        after.Series.Should().BeNull();
        after.SeriesNumber.Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    [InlineData(1.555)]
    public async Task Create_WithAnInvalidNumber_IsRefused(decimal number)
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        using var response = await client.PostAsJsonAsync("/v1/books", InSeries("X", "Série", number)(LibraryTestData.NewBook()), LibraryTestData.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SeriesBooks_ListsOwnedAndWished_InReadingOrder_NumberlessLast()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, InSeries("Tudo É Vaidade", "Bórgia", 4, BookOwnership.Wishlist));
        await LibraryTestData.CreateBookAsync(client, InSeries("Sangue para o Papa", "Bórgia", 1));
        await LibraryTestData.CreateBookAsync(client, InSeries("Extra", "Bórgia", null));
        await LibraryTestData.CreateBookAsync(client, InSeries("Interlúdio", "Bórgia", 1.5m));
        await LibraryTestData.CreateBookAsync(client, InSeries("Outra", "Outra série", 1));

        var books = await client.GetFromJsonAsync<List<BookDto>>("/v1/series/books?name=B%C3%B3rgia", LibraryTestData.Json);

        books!.Select(b => b.Title).Should().Equal("Sangue para o Papa", "Interlúdio", "Tudo É Vaidade", "Extra");
    }

    [Fact]
    public async Task SeriesSummary_AndRename_MergeIntoAnExistingSeries_KeepingNumbers()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var a = await LibraryTestData.CreateBookAsync(client, InSeries("A", "Heroes of Olympus", 2));
        await LibraryTestData.CreateBookAsync(client, InSeries("B", "The Heroes of Olympus", 1));
        await LibraryTestData.CreateBookAsync(client, InSeries("C", "The Heroes of Olympus", 3, BookOwnership.Wishlist));

        var before = await client.GetFromJsonAsync<List<SeriesDto>>("/v1/series", LibraryTestData.Json);
        using var response = await client.PutAsJsonAsync("/v1/series", new RenameSeriesRequest("Heroes of Olympus", "The Heroes of Olympus"), LibraryTestData.Json);
        var after = await client.GetFromJsonAsync<List<SeriesDto>>("/v1/series", LibraryTestData.Json);
        var moved = await client.GetFromJsonAsync<BookDto>($"/v1/books/{a.Id}", LibraryTestData.Json);

        before.Should().Equal(new SeriesDto("Heroes of Olympus", 1, 0), new SeriesDto("The Heroes of Olympus", 1, 1));
        (await response.ReadAsync<RenameSeriesResult>()).Updated.Should().Be(1);
        after.Should().Equal(new SeriesDto("The Heroes of Olympus", 2, 1));
        moved!.SeriesNumber.Should().Be(2);
    }

    [Fact]
    public async Task List_FiltersBySeries_InItsOrder()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, InSeries("Zz segundo", "Peregrine", 2));
        await LibraryTestData.CreateBookAsync(client, InSeries("Aa primeiro", "Peregrine", 1));
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Fora" });

        var books = await client.GetFromJsonAsync<List<BookDto>>("/v1/books?series=Peregrine", LibraryTestData.Json);

        books!.Select(b => b.Title).Should().Equal("Aa primeiro", "Zz segundo");
    }

    [Fact]
    public async Task Rate_ReadBook_InHalves_FiltersAndSorts()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var good = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Bom", Status = BookStatus.Read });
        var great = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Ótimo", Status = BookStatus.Read });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Sem nota", Status = BookStatus.Read });

        (await RateAsync(client, good.Id, 3.5m)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await RateAsync(client, great.Id, 5m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var sorted = await client.GetFromJsonAsync<List<BookDto>>("/v1/books?sort=rating", LibraryTestData.Json);
        var atLeastFour = await client.GetFromJsonAsync<List<BookDto>>("/v1/books?minRating=4", LibraryTestData.Json);

        sorted!.Select(b => (b.Title, b.Rating)).Should().Equal(("Ótimo", 5m), ("Bom", 3.5m), ("Sem nota", (decimal?)null));
        atLeastFour!.Select(b => b.Title).Should().Equal("Ótimo");
    }

    [Fact]
    public async Task Rate_ClearsWithNull_AndRefusesBadValuesAndUnreadBooks()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var read = await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Read });
        var reading = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Lendo", Status = BookStatus.Reading });

        (await RateAsync(client, read.Id, 4.3m)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RateAsync(client, read.Id, 0m)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RateAsync(client, reading.Id, 4m)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await RateAsync(client, read.Id, 4m);
        using var cleared = await RateAsync(client, read.Id, null);

        (await cleared.ReadAsync<BookDto>()).Rating.Should().BeNull();
    }

    [Fact]
    public async Task Days_SumPagesPerDay_WithTheirBooks_AndUnloggedFinishes()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var dracula = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Drácula", TotalPages = 418 });
        var rosa = await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Rosa" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Antigo", TotalPages = 200, Status = BookStatus.Read, FinishedAt = new DateOnly(2026, 3, 2) });
        await LogAsync(client, dracula.Id, 40, new DateOnly(2026, 10, 3));
        await LogAsync(client, rosa.Id, 25, new DateOnly(2026, 10, 3));
        await LogAsync(client, dracula.Id, 90, new DateOnly(2026, 10, 5));

        var days = await client.GetFromJsonAsync<List<DayStatsDto>>("/v1/stats/2026/days", LibraryTestData.Json);

        days!.Select(d => (d.Date, d.PagesRead)).Should().Equal(
            (new DateOnly(2026, 3, 2), 200), (new DateOnly(2026, 10, 3), 65), (new DateOnly(2026, 10, 5), 50));
        days![1].Books.Select(b => (b.Title, b.PagesRead)).Should().Equal(("Drácula", 40), ("Rosa", 25));
    }

    private static Task<HttpResponseMessage> RateAsync(HttpClient client, Guid id, decimal? rating) =>
        client.PatchAsJsonAsync($"/v1/books/{id}/rating", new RateBookRequest(rating), LibraryTestData.Json);

    private static async Task LogAsync(HttpClient client, Guid bookId, int page, DateOnly date)
    {
        using var response = await client.PostAsJsonAsync($"/v1/books/{bookId}/logs", new ReadingLogRequest(date, page), LibraryTestData.Json);
        response.EnsureSuccessStatusCode();
    }
}
