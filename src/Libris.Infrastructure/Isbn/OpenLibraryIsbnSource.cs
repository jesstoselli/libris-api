using System.Net;
using System.Text.Json;
using Libris.Application.Abstractions.Isbn;

namespace Libris.Infrastructure.Isbn;

public sealed class OpenLibraryIsbnSource : IIsbnSource
{
    private readonly HttpClient _http;

    public OpenLibraryIsbnSource(HttpClient http)
    {
        _http = http;
    }

    public string Name => "open_library";

    public async Task<IsbnSourceResult> FindAsync(string isbn13, CancellationToken ct)
    {
        try
        {
            using var edition = await GetAsync($"isbn/{isbn13}.json", ct);
            if (edition is null)
            {
                return IsbnSourceResult.NotFound;
            }

            var root = edition.RootElement;
            using var work = root.TryGetProperty("works", out var works) && works.GetArrayLength() > 0
                ? await GetAsync(Relative(works[0].GetProperty("key").GetString()!) + ".json", ct)
                : null;

            var authorKeys = AuthorKeys(root);
            if (authorKeys.Count == 0 && work is not null)
            {
                authorKeys = WorkAuthorKeys(work.RootElement);
            }

            var authors = new List<string>();
            foreach (var key in authorKeys)
            {
                using var author = await GetAsync(Relative(key) + ".json", ct);
                if (author is not null && author.RootElement.TryGetProperty("name", out var name) && name.GetString() is { } n)
                {
                    authors.Add(n);
                }
            }

            return IsbnSourceResult.Found(new IsbnCandidate(
                String(root, "title"),
                authors,
                root.TryGetProperty("number_of_pages", out var pages) && pages.TryGetInt32(out var p) && p > 0 ? p : null,
                root.TryGetProperty("publishers", out var publishers) && publishers.GetArrayLength() > 0 ? publishers[0].GetString() : null,
                PublishedYear.Parse(String(root, "publish_date")),
                Description(root) ?? (work is null ? null : Description(work.RootElement)),
                CoverUrl(root),
                work is not null && work.RootElement.TryGetProperty("subjects", out var subjects)
                    ? [.. subjects.EnumerateArray().Select(s => s.GetString()).OfType<string>()]
                    : []));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return IsbnSourceResult.Failed;
        }
    }

    /// <returns>null on 404; throws HttpRequestException on any other failure.</returns>
    private async Task<JsonDocument?> GetAsync(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(new Uri(path, UriKind.Relative), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(body, cancellationToken: ct);
    }

    private static string Relative(string key) => key.TrimStart('/');

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> AuthorKeys(JsonElement edition) =>
        edition.TryGetProperty("authors", out var authors)
            ? [.. authors.EnumerateArray().Select(a => String(a, "key")).OfType<string>()]
            : [];

    private static List<string> WorkAuthorKeys(JsonElement work) =>
        work.TryGetProperty("authors", out var authors)
            ? [.. authors.EnumerateArray()
                .Select(a => a.TryGetProperty("author", out var author) ? String(author, "key") : null)
                .OfType<string>()]
            : [];

    // Open Library stores descriptions either as a plain string or as { "type": "/type/text", "value": "..." }.
    private static string? Description(JsonElement element)
    {
        if (!element.TryGetProperty("description", out var description))
        {
            return null;
        }

        return description.ValueKind == JsonValueKind.String ? description.GetString() : String(description, "value");
    }

    private static string? CoverUrl(JsonElement edition) =>
        edition.TryGetProperty("covers", out var covers)
        && covers.EnumerateArray().Select(c => c.TryGetInt64(out var id) ? id : -1).FirstOrDefault(id => id > 0) is var coverId and > 0
            ? $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg"
            : null;
}
