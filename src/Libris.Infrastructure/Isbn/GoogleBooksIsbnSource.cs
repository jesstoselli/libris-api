using System.Text.Json;
using Libris.Application.Abstractions.Isbn;
using Microsoft.Extensions.Options;

namespace Libris.Infrastructure.Isbn;

public sealed class GoogleBooksIsbnSource : IIsbnSource
{
    private readonly HttpClient _http;
    private readonly IOptions<IsbnOptions> _options;

    public GoogleBooksIsbnSource(HttpClient http, IOptions<IsbnOptions> options)
    {
        _http = http;
        _options = options;
    }

    public string Name => "google_books";

    public async Task<IsbnSourceResult> FindAsync(string isbn13, CancellationToken ct)
    {
        var path = $"volumes?q=isbn:{isbn13}";
        if (_options.Value.GoogleBooksApiKey is { Length: > 0 } key)
        {
            path += "&key=" + Uri.EscapeDataString(key);
        }

        try
        {
            // Quota (429) and auth errors count as failures, not as "no such book".
            using var response = await _http.GetAsync(new Uri(path, UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
            {
                return IsbnSourceResult.Failed;
            }

            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            if (!json.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            {
                return IsbnSourceResult.NotFound;
            }

            var info = items[0].GetProperty("volumeInfo");
            return IsbnSourceResult.Found(new IsbnCandidate(
                String(info, "title"),
                Strings(info, "authors"),
                info.TryGetProperty("pageCount", out var pages) && pages.TryGetInt32(out var p) && p > 0 ? p : null,
                String(info, "publisher"),
                PublishedYear.Parse(String(info, "publishedDate")),
                String(info, "description"),
                info.TryGetProperty("imageLinks", out var images) && String(images, "thumbnail") is { } thumbnail
                    ? thumbnail.Replace("http://", "https://", StringComparison.Ordinal)
                    : null,
                Strings(info, "categories"),
                LanguageCodes.FromTag(String(info, "language"))));
        }
        // Any failure, including catalogue JSON in an unexpected shape, makes this source "down" so the next one
        // still gets a chance; only the caller's own cancellation propagates.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return IsbnSourceResult.Failed;
        }
    }

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> Strings(JsonElement element, string property) =>
        element.TryGetProperty(property, out var values)
            ? [.. values.EnumerateArray().Select(v => v.GetString()).OfType<string>()]
            : [];
}
