namespace Libris.Infrastructure.Covers;

public sealed class CoverOptions
{
    public const string SectionName = "Covers";

    public string Path { get; set; } = "/data/covers";
    public int MaxHeight { get; set; } = 600;

    // Short startup delay: every deploy recreates the container, so a sweep that waited a full interval would never run.
    public int JanitorStartupDelaySeconds { get; set; } = 60;
    public int JanitorIntervalSeconds { get; set; } = 24 * 60 * 60;
    public IReadOnlyList<string> AllowedDownloadHosts { get; set; } =
        ["covers.openlibrary.org", "books.google.com", "books.googleusercontent.com"];
}
