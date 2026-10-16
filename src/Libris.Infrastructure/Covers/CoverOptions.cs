namespace Libris.Infrastructure.Covers;

public sealed class CoverOptions
{
    public const string SectionName = "Covers";

    public string Path { get; set; } = "/data/covers";
    public int MaxHeight { get; set; } = 600;
    public IReadOnlyList<string> AllowedDownloadHosts { get; set; } =
        ["covers.openlibrary.org", "books.google.com", "books.googleusercontent.com"];
}
