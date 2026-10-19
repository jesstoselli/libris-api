using System.Net.Http.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Books;
using Libris.Application.Dtos.Genres;
using Libris.Domain.Entities;
using Libris.Domain.Enums;
using Libris.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Libris.Api.Tests.Books;

public sealed class BooksListTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public BooksListTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_DefaultOwnership_ReturnsAllOwnedOrderedByTitle()
    {
        using var client = await SeedAsync();

        var books = await ListAsync(client, "");

        books.Select(b => b.Title).Should().Equal("A Assombração da Casa da Colina", "Drácula", "O Nome da Rosa");
    }

    [Fact]
    public async Task List_FilesTitlesWithoutTheLeadingArticleOfTheirLanguage()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "O Colecionador de Ossos" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Bird Box", Language = "en" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "The Shining", Language = "en" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "Drácula" });
        await LibraryTestData.CreateBookAsync(client, r => r with { Title = "The Hobbit" });

        var books = await ListAsync(client, "");

        books.Select(b => b.Title).Should().Equal("Bird Box", "O Colecionador de Ossos", "Drácula", "The Shining", "The Hobbit");
    }

    [Fact]
    public async Task List_StatusFilter()
    {
        using var client = await SeedAsync();

        var books = await ListAsync(client, "?status=reading");

        books.Select(b => b.Title).Should().Equal("O Nome da Rosa");
    }

    [Fact]
    public async Task List_GenreFilter()
    {
        using var client = await SeedAsync();
        var terror = (await client.GetFromJsonAsync<List<GenreDto>>("/v1/genres"))!.Single(g => g.Name == "Terror");

        var books = await ListAsync(client, $"?genreId={terror.Id}");

        books.Select(b => b.Title).Should().Equal("A Assombração da Casa da Colina", "Drácula");
    }

    [Fact]
    public async Task List_FiltersCombine()
    {
        using var client = await SeedAsync();
        var terror = (await client.GetFromJsonAsync<List<GenreDto>>("/v1/genres"))!.Single(g => g.Name == "Terror");

        var books = await ListAsync(client, $"?genreId={terror.Id}&status=not_started");

        books.Select(b => b.Title).Should().Equal("A Assombração da Casa da Colina");
    }

    [Fact]
    public async Task List_Wishlist_ReturnsOnlyWishlist()
    {
        using var client = await SeedAsync();

        var books = await ListAsync(client, "?ownership=wishlist");

        books.Select(b => b.Title).Should().Equal("Rebecca");
    }

    [Theory]
    [InlineData("assombracao", "A Assombração da Casa da Colina")]
    [InlineData("ECO", "O Nome da Rosa")]
    [InlineData("drac", "Drácula")]
    public async Task List_Search_IgnoresCaseAndAccents(string q, string expected)
    {
        using var client = await SeedAsync();

        var books = await ListAsync(client, $"?q={Uri.EscapeDataString(q)}");

        books.Select(b => b.Title).Should().Equal(expected);
    }

    [Fact]
    public async Task List_Search_TreatsWildcardsLiterally()
    {
        using var client = await SeedAsync();

        var books = await ListAsync(client, "?q=%25");

        books.Should().BeEmpty();
    }

    [Fact]
    public async Task List_IncludesProgress()
    {
        using var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        var book = await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Reading });
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ReadingLogs.Add(new ReadingLog
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                Date = new DateOnly(2026, 10, 14),
                Page = 140,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var listed = (await ListAsync(client, "")).Single();

        listed.CurrentPage.Should().Be(140);
        listed.Percent.Should().Be(27);
    }

    [Fact]
    public async Task List_NeverShowsOtherUsersBooks()
    {
        using var owner = await SeedAsync();
        using var stranger = await AuthTestClient.CreateAuthorizedClientAsync(_factory);

        (await ListAsync(stranger, "")).Should().BeEmpty();
        (await ListAsync(stranger, "?ownership=wishlist")).Should().BeEmpty();
    }

    private async Task<HttpClient> SeedAsync()
    {
        var client = await AuthTestClient.CreateAuthorizedClientAsync(_factory);
        await LibraryTestData.CreateBookAsync(client, r => r with { Status = BookStatus.Reading, Genres = ["Mistério"] });
        await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Title = "Drácula", Authors = ["Bram Stoker"], TotalPages = 418, Status = BookStatus.Read, Genres = ["Terror"],
        });
        await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Title = "A Assombração da Casa da Colina", Authors = ["Shirley Jackson"], TotalPages = 246, Genres = ["Terror"],
        });
        await LibraryTestData.CreateBookAsync(client, r => r with
        {
            Title = "Rebecca", Authors = ["Daphne du Maurier"], TotalPages = null, Ownership = BookOwnership.Wishlist,
        });
        return client;
    }

    private static async Task<List<BookDto>> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<List<BookDto>>("/v1/books" + query, LibraryTestData.Json))!;
}
