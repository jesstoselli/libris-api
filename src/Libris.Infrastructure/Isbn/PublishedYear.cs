using System.Globalization;
using System.Text.RegularExpressions;

namespace Libris.Infrastructure.Isbn;

internal static partial class PublishedYear
{
    /// <summary>Reads the year out of "1994", "October 1, 1994" or "2003-02-27".</summary>
    public static int? Parse(string? date)
    {
        if (date is null)
        {
            return null;
        }

        var match = YearPattern().Match(date);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"\b\d{4}\b", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex YearPattern();
}
