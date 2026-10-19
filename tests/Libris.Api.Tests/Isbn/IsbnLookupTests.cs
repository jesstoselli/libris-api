using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Libris.Api.Tests.Infrastructure;
using Libris.Application.Dtos.Isbn;
using Libris.Application.UseCases.IsbnLookup;
using Libris.Infrastructure.Isbn;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Libris.Api.Tests.Isbn;

public sealed class IsbnLookupTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "Isbn", "Fixtures");
    private readonly ApiWebApplicationFactory _factory;

    public IsbnLookupTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Lookup_OpenLibraryComplete_DoesNotCallGoogle()
    {
        var (openLibrary, google) = (FixtureHandler(), FixtureHandler());
        using var client = await ClientAsync(openLibrary, google);

        using var response = await client.GetAsync(new Uri("/v1/isbn/9780156001311", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadAsync<IsbnLookupDto>();
        result.Should().BeEquivalentTo(new
        {
            Isbn = "9780156001311",
            Title = "The Name of the Rose",
            TotalPages = 536,
            PublishedYear = 1994,
            Source = "open_library",
        });
        result.Authors.Should().Equal("Umberto Eco");
        result.Language.Should().Be("en", "Open Library's /languages/eng becomes the two-letter code");
        result.Publisher.Should().Be("Harcourt Brace");
        result.Synopsis.Should().NotBeNullOrWhiteSpace();
        result.CoverUrl.Should().StartWith("https://covers.openlibrary.org/b/id/").And.EndWith("-L.jpg");
        result.SuggestedGenres.Should().HaveCountLessThanOrEqualTo(5).And.NotContain("Fiction");
        google.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Lookup_WithHyphens_AsksTheSourcesForTheBareIsbn()
    {
        var (openLibrary, google) = (FixtureHandler(), FixtureHandler());
        using var client = await ClientAsync(openLibrary, google);

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/978-0-15-600131-1", LibraryTestData.Json);

        result!.Title.Should().Be("The Name of the Rose");
        openLibrary.Requests.First().AbsolutePath.Should().Be("/isbn/9780156001311.json");
    }

    [Fact]
    public async Task Lookup_OpenLibraryWithoutPages_FillsFromGoogle()
    {
        var (openLibrary, google) = (FixtureHandler(), FixtureHandler());
        using var client = await ClientAsync(openLibrary, google);

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9780141439846", LibraryTestData.Json);

        result!.Source.Should().Be("open_library+google_books");
        result.Title.Should().Be("Dracula");
        result.TotalPages.Should().Be(488);
        result.SuggestedGenres.Should().Contain("Horror");
    }

    [Fact]
    public async Task Lookup_OpenLibraryWithoutPages_GoogleFails_ReturnsWhatOpenLibraryHas()
    {
        var google = new StubHandler(_ => Json(HttpStatusCode.TooManyRequests, "gb-quota.json"));
        using var client = await ClientAsync(FixtureHandler(), google);

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9780141439846", LibraryTestData.Json);

        result!.Source.Should().Be("open_library");
        result.Title.Should().Be("Dracula");
        result.TotalPages.Should().BeNull();
    }

    [Fact]
    public async Task Lookup_OpenLibraryUnexpectedShape_FallsBackToGoogle()
    {
        var fixtures = FixtureHandler();
        var openLibrary = new StubHandler(request =>
        {
            if (!request.RequestUri!.AbsolutePath.StartsWith("/isbn/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            // Catalogue data seen in the wild: pages as text, publishers as objects.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "title": "Dracula", "number_of_pages": "about 466", "publishers": [{ "name": "Penguin" }], "covers": ["x"] }""",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };
        });
        using var client = await ClientAsync(openLibrary, fixtures);

        using var response = await client.GetAsync(new Uri("/v1/isbn/9780141439846", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadAsync<IsbnLookupDto>();
        result.Title.Should().Be("Dracula");
        result.TotalPages.Should().Be(488);
    }

    [Fact]
    public async Task Lookup_GoogleUnexpectedShape_IsUnavailableNotServerError()
    {
        var google = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "totalItems": 1, "items": [{ "id": "x" }] }""", System.Text.Encoding.UTF8, "application/json"),
        });
        using var client = await ClientAsync(FixtureHandler(), google);

        using var response = await client.GetAsync(new Uri("/v1/isbn/9788501012791", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Lookup_AuthorsOnlyOnWork_AreRead()
    {
        using var client = await ClientAsync(FixtureHandler(), FixtureHandler());

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9788535914849", LibraryTestData.Json);

        result!.Authors.Should().Equal("George Orwell");
    }

    [Fact]
    public async Task Lookup_OnlyGoogleHas_ReturnsGoogle()
    {
        using var client = await ClientAsync(FixtureHandler(), FixtureHandler());

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9788501012791", LibraryTestData.Json);

        result!.Source.Should().Be("google_books");
        result.Title.Should().Be("O nome da rosa");
        result.TotalPages.Should().Be(562);
        result.PublishedYear.Should().Be(1983);
        result.CoverUrl.Should().StartWith("https://books.google.com/");
    }

    [Fact]
    public async Task Lookup_NeitherHas_Returns404()
    {
        using var client = await ClientAsync(FixtureHandler(), FixtureHandler());

        using var response = await client.GetAsync(new Uri("/v1/isbn/9780306406157", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorAsync(response)).Should().Be("isbn_not_found");
    }

    [Fact]
    public async Task Lookup_OpenLibraryNotFound_GoogleQuota_Returns503()
    {
        var google = new StubHandler(_ => Json(HttpStatusCode.TooManyRequests, "gb-quota.json"));
        using var client = await ClientAsync(FixtureHandler(), google);

        using var response = await client.GetAsync(new Uri("/v1/isbn/9788501012791", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ErrorAsync(response)).Should().Be("isbn_lookup_unavailable");
    }

    [Fact]
    public async Task Lookup_Isbn10_IsNormalizedBeforeQuerying()
    {
        var openLibrary = FixtureHandler();
        using var client = await ClientAsync(openLibrary, FixtureHandler());

        using var response = await client.GetAsync(new Uri("/v1/isbn/0-15-600131-4", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        openLibrary.Requests.First().AbsolutePath.Should().Be("/isbn/9780156001311.json");
    }

    [Fact]
    public async Task Lookup_Invalid_Returns400()
    {
        using var client = await ClientAsync(FixtureHandler(), FixtureHandler());

        using var response = await client.GetAsync(new Uri("/v1/isbn/12345", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(response)).Should().Be("invalid_isbn");
    }

    [Fact]
    public async Task Lookup_BothSourcesThrow_Returns503()
    {
        var down = new StubHandler(_ => throw new HttpRequestException("network down"));
        using var client = await ClientAsync(down, new StubHandler(_ => throw new HttpRequestException("network down")));

        using var response = await client.GetAsync(new Uri("/v1/isbn/9780156001311", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Lookup_GoogleApiKeyConfigured_IsSent()
    {
        var google = FixtureHandler();
        var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Isbn:GoogleBooksApiKey", "test-key"));
        using var client = await ClientAsync(FixtureHandler(), google, factory);

        (await client.GetAsync(new Uri("/v1/isbn/9788501012791", UriKind.Relative))).EnsureSuccessStatusCode();

        google.Requests.Single().Query.Should().Contain("key=test-key");
    }

    [Fact]
    public async Task Lookup_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/isbn/9780156001311", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void Suggestions_FilterNoiseAndLimitToFive()
    {
        var suggestions = GenreSuggestions.From(
        [
            "Fiction / Horror", "Fiction", "Accessible book", "Protected DAISY", "In library", "nyt:hardcover-fiction=2001",
            "Large type books", "horror", "Gothic fiction", "A subject that is definitely far too long to be a genre name",
            "Vampires", "Transylvania (Romania)", "Epistolary fiction", "Classics",
        ]);

        suggestions.Should().Equal("Horror", "Gothic fiction", "Vampires", "Transylvania (Romania)", "Epistolary fiction");
    }

    [Fact]
    public async Task Lookup_NoEarlierSourceHasTheAuthor_BrasilApiFillsIt_InReadingOrder()
    {
        var brasil = new StubHandler(_ => Json(HttpStatusCode.OK, "br-9788571649125.json"));
        using var client = await ClientAsync(FixtureHandler(), FixtureHandler(), brasilApi: brasil);

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9788571649125", LibraryTestData.Json);

        result!.Title.Should().Be("Pantera No Porão");
        result.Publisher.Should().Be("Companhia das Letras");
        result.Authors.Should().Equal("Amos Oz");
        result.Source.Should().Be("open_library+brasilapi");
    }

    [Fact]
    public async Task Lookup_TitleAndPagesWithoutAuthor_StillAsksTheNextSource()
    {
        var openLibrary = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"title":"Pantera No Porão","number_of_pages":159,"publishers":["Companhia das Letras"]}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var google = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"items":[{"volumeInfo":{"title":"Pantera no porão","authors":["Amós Oz"]}}]}""", System.Text.Encoding.UTF8, "application/json"),
        });
        using var client = await ClientAsync(openLibrary, google);

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9788571649125", LibraryTestData.Json);

        result!.Authors.Should().Equal("Amós Oz");
        result.TotalPages.Should().Be(159);
        result.Source.Should().Be("open_library+google_books");
    }

    [Fact]
    public async Task Lookup_CompleteFromEarlierSources_DoesNotAskBrasilApi()
    {
        var brasil = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = await ClientAsync(FixtureHandler(), FixtureHandler(), brasilApi: brasil);

        using var response = await client.GetAsync(new Uri("/v1/isbn/9780156001311", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        brasil.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Lookup_BrasilApiDown_ReturnsWhatTheOthersHave()
    {
        var brasil = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = await ClientAsync(FixtureHandler(), FixtureHandler(), brasilApi: brasil);

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9788571649125", LibraryTestData.Json);

        result!.Title.Should().Be("Pantera No Porão");
        result.Authors.Should().BeEmpty();
        result.Source.Should().Be("open_library");
    }

    private async Task<HttpClient> ClientAsync(
        StubHandler openLibrary, StubHandler google, WebApplicationFactory<Program>? baseFactory = null, StubHandler? brasilApi = null)
    {
        brasilApi ??= new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var factory = (baseFactory ?? _factory).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<OpenLibraryIsbnSource>().ConfigurePrimaryHttpMessageHandler(() => openLibrary);
            services.AddHttpClient<GoogleBooksIsbnSource>().ConfigurePrimaryHttpMessageHandler(() => google);
            services.AddHttpClient<BrasilApiIsbnSource>().ConfigurePrimaryHttpMessageHandler(() => brasilApi);
        }));
        return await AuthTestClient.CreateAuthorizedClientAsync(factory);
    }

    /// <summary>Serves the recorded responses by URL, 404 (Open Library) or an empty result (Google) for anything else.</summary>
    private static StubHandler FixtureHandler() => new(request =>
    {
        var uri = request.RequestUri!;
        if (uri.Host.EndsWith("googleapis.com", StringComparison.Ordinal))
        {
            var isbn = uri.Query.Split("isbn:")[1].Split('&')[0];
            var file = File.Exists(Path.Combine(FixturesDir, $"gb-{isbn}.json")) ? $"gb-{isbn}.json" : "gb-empty.json";
            return Json(HttpStatusCode.OK, file);
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/');
        var name = $"ol-{parts[0]}-{Path.GetFileNameWithoutExtension(parts[1])}.json";
        return File.Exists(Path.Combine(FixturesDir, name))
            ? Json(HttpStatusCode.OK, name)
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    });

    private static HttpResponseMessage Json(HttpStatusCode status, string file) => new(status)
    {
        Content = new StringContent(File.ReadAllText(Path.Combine(FixturesDir, file)), System.Text.Encoding.UTF8, "application/json"),
    };

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();

    [Fact]
    public async Task Lookup_GoogleRegionalLanguage_BecomesTheTwoLetterCode()
    {
        var google = new StubHandler(_ => Json(HttpStatusCode.OK, "gb-9788501012791.json"));
        var openLibrary = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = await ClientAsync(openLibrary, google);

        var result = await client.GetFromJsonAsync<IsbnLookupDto>("/v1/isbn/9788501012791", LibraryTestData.Json);

        result!.Language.Should().Be("pt");
    }
}
