namespace Libris.Application.UseCases.IsbnLookup;

public static class GenreSuggestions
{
    private const int MaxSuggestions = 5;
    private const int MaxLength = 40;

    private static readonly string[] NoiseFragments =
        ["Accessible book", "Protected DAISY", "In library", "nyt:", "Large type"];

    /// <summary>Turns noisy catalogue subjects into a few genre suggestions the user accepts or dismisses.</summary>
    public static IReadOnlyList<string> From(IEnumerable<string> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        return subjects
            .SelectMany(s => s.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(s => s.Length <= MaxLength)
            .Where(s => !string.Equals(s, "Fiction", StringComparison.OrdinalIgnoreCase))
            .Where(s => !NoiseFragments.Any(n => s.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .ToList();
    }
}
