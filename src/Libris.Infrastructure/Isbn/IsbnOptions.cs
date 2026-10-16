namespace Libris.Infrastructure.Isbn;

public sealed class IsbnOptions
{
    public const string SectionName = "Isbn";

    /// <summary>Optional. Without it Google Books uses a shared anonymous quota that is often exhausted.</summary>
    public string? GoogleBooksApiKey { get; set; }
}
