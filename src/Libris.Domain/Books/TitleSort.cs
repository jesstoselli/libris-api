namespace Libris.Domain.Books;

/// <summary>Where a title files in an alphabetical list: without the leading article of the book's language.</summary>
public static class TitleSort
{
    private static readonly Dictionary<string, string[]> Articles = new(StringComparer.Ordinal)
    {
        ["pt"] = ["o", "a", "os", "as", "um", "uma", "uns", "umas"],
        ["en"] = ["the", "a", "an"],
        ["es"] = ["el", "la", "los", "las", "un", "una", "unos", "unas"],
        ["fr"] = ["le", "la", "les", "un", "une"],
        ["it"] = ["il", "lo", "la", "i", "gli", "le", "un", "uno", "una"],
        ["de"] = ["der", "die", "das", "ein", "eine"],
    };

    // Elided before a vowel, with nothing between the article and the word.
    private static readonly Dictionary<string, string[]> Elided = new(StringComparer.Ordinal)
    {
        ["fr"] = ["l'", "l’"],
        ["it"] = ["l'", "l’", "un'", "un’"],
    };

    public static string Key(string title, string language)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (Elided.TryGetValue(language, out var elided))
        {
            foreach (var prefix in elided)
            {
                if (title.Length > prefix.Length && title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return title[prefix.Length..];
                }
            }
        }

        if (Articles.TryGetValue(language, out var articles))
        {
            var space = title.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0 && space < title.Length - 1 && articles.Contains(title[..space], StringComparer.OrdinalIgnoreCase))
            {
                return title[(space + 1)..].TrimStart();
            }
        }

        return title;
    }
}
