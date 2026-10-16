using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Libris.Application.Dtos.Books;
using Libris.Domain.Enums;

namespace Libris.Api.Tests.Infrastructure;

public static class LibraryTestData
{
    public static readonly DateOnly Today = new(2026, 10, 16);

    public static readonly JsonSerializerOptions Json = CreateJson();

    public static CreateBookRequest NewBook() => new(
        Title: "O Nome da Rosa",
        Authors: ["Umberto Eco"],
        Isbn: null,
        TotalPages: 512,
        Publisher: null,
        PublishedYear: null,
        Synopsis: null,
        CoverId: null,
        Genres: [],
        Ownership: BookOwnership.Owned,
        Status: null,
        StartedAt: null,
        FinishedAt: null,
        Today: Today);

    public static async Task<BookDto> CreateBookAsync(HttpClient client, Func<CreateBookRequest, CreateBookRequest>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var request = configure is null ? NewBook() : configure(NewBook());
        using var response = await client.PostAsJsonAsync("/v1/books", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookDto>(Json))!;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
