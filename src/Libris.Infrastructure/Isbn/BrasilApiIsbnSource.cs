using System.Net;
using System.Text.Json;
using Libris.Application.Abstractions.Isbn;

namespace Libris.Infrastructure.Isbn;

/// <summary>
/// BrasilAPI's ISBN service, backed by the Câmara Brasileira do Livro catalogue: it knows the authors of Brazilian
/// editions that Open Library and Google Books often lack. It is registered last, so it only fills what is missing.
/// It can list translators among the authors, which is why it never overrides an earlier source.
/// </summary>
public sealed class BrasilApiIsbnSource : IIsbnSource
{
    private readonly HttpClient _http;

    public BrasilApiIsbnSource(HttpClient http)
    {
        _http = http;
    }

    public string Name => "brasilapi";

    public async Task<IsbnSourceResult> FindAsync(string isbn13, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(new Uri($"api/isbn/v1/{isbn13}", UriKind.Relative), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return IsbnSourceResult.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                return IsbnSourceResult.Failed;
            }

            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            var book = json.RootElement;
            return IsbnSourceResult.Found(new IsbnCandidate(
                Tidy(String(book, "title")),
                [.. Strings(book, "authors").Select(AuthorNames.Normalize).Where(a => a.Length > 0)],
                Int(book, "page_count") is > 0 and var pages ? pages : null,
                Tidy(String(book, "publisher")),
                Int(book, "year"),
                String(book, "synopsis"),
                String(book, "cover_url"),
                Strings(book, "subjects")));
        }
        // Any failure, including JSON in an unexpected shape, makes this source "down"; only the caller's own
        // cancellation propagates.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return IsbnSourceResult.Failed;
        }
    }

    /// <summary>The catalogue writes titles and publishers in capitals; shown as they would be on a cover.</summary>
    private static string? Tidy(string? text) =>
        text is null ? null : AuthorNames.IsShouting(text) ? AuthorNames.ProperCase(text) : text;

    // The catalogue sends null for unknown numbers, and TryGetInt32 throws on anything that isn't a number.
    private static int? Int(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : null;

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } s
            ? s.Trim()
            : null;

    private static List<string> Strings(JsonElement element, string property) =>
        element.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? [.. values.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : null).OfType<string>()]
            : [];
}
