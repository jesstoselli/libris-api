using System.Globalization;

namespace Libris.Infrastructure.Isbn;

/// <summary>
/// Library catalogues (the CBL behind BrasilAPI) write names as "OZ, AMOS"; the app shows them as they are read,
/// "Amos Oz". Names already in reading order and mixed case are left alone.
/// </summary>
public static class AuthorNames
{
    private static readonly HashSet<string> Particles = new(StringComparer.OrdinalIgnoreCase) { "de", "da", "do", "das", "dos", "e", "di", "du", "van", "von" };

    public static string Normalize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var name = raw.Trim();
        var parts = name.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            name = $"{parts[1]} {parts[0]}";
        }

        return IsShouting(name) ? ProperCase(name) : name;
    }

    /// <summary>All-caps text ("COMPANHIA DAS LETRAS") in proper case, keeping short particles lowercase.</summary>
    public static string ProperCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((w, i) =>
        {
            var lower = w.ToLower(CultureInfo.GetCultureInfo("pt-BR"));
            return i > 0 && Particles.Contains(lower) ? lower : char.ToUpper(lower[0], CultureInfo.GetCultureInfo("pt-BR")) + lower[1..];
        });
        return string.Join(' ', words);
    }

    public static bool IsShouting(string text) => text.Any(char.IsLetter) && !text.Any(char.IsLower);
}
