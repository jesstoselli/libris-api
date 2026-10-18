namespace Libris.Infrastructure.Isbn;

/// <summary>Catalogues name languages differently ("pt-BR" at Google, "/languages/por" at Open Library); the app keeps ISO 639-1.</summary>
public static class LanguageCodes
{
    // MARC/ISO 639-2 codes Open Library uses, for the languages a reader here is likely to meet.
    private static readonly Dictionary<string, string> FromThreeLetters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["por"] = "pt", ["eng"] = "en", ["spa"] = "es", ["fre"] = "fr", ["fra"] = "fr", ["ita"] = "it",
        ["ger"] = "de", ["deu"] = "de", ["jpn"] = "ja", ["rus"] = "ru", ["chi"] = "zh", ["zho"] = "zh",
        ["kor"] = "ko", ["ara"] = "ar", ["lat"] = "la", ["cat"] = "ca", ["dut"] = "nl", ["nld"] = "nl",
        ["pol"] = "pl", ["swe"] = "sv", ["gre"] = "el", ["ell"] = "el", ["heb"] = "he", ["tur"] = "tr",
    };

    /// <summary>"pt-BR", "PT" or "en" → two lowercase letters; anything else is unknown.</summary>
    public static string? FromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var primary = tag.Trim().Split('-', '_')[0];
        return primary.Length == 2 && primary.All(char.IsAsciiLetter) ? primary.ToLowerInvariant() : null;
    }

    /// <summary>"/languages/eng" or "eng" → "en"; unknown codes are dropped rather than guessed.</summary>
    public static string? FromOpenLibraryKey(string? key) =>
        key is null ? null : FromThreeLetters.GetValueOrDefault(key.Split('/')[^1]);
}
