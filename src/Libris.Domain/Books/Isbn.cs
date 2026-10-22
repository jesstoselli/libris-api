using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Libris.Domain.Books;

public static class Isbn
{
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? isbn13)
    {
        isbn13 = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var compact = new string(input.Where(c => !IsSeparator(c)).ToArray()).ToUpperInvariant();

        if (compact.Length == 13 && compact.All(char.IsAsciiDigit) && CheckDigit13(compact.AsSpan(0, 12)) == compact[12] - '0')
        {
            isbn13 = compact;
            return true;
        }

        if (compact.Length == 10 && IsValidIsbn10(compact))
        {
            var body = "978" + compact[..9];
            isbn13 = body + CheckDigit13(body).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Any space or hyphen, and the invisible marks a copy from a store page brings along (U+200E around the digits on
    /// Amazon); a letter in the middle still makes the ISBN invalid.
    /// </summary>
    private static bool IsSeparator(char c) =>
        char.IsWhiteSpace(c) || char.GetUnicodeCategory(c) is UnicodeCategory.DashPunctuation or UnicodeCategory.Format;

    private static int CheckDigit13(ReadOnlySpan<char> first12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - (sum % 10)) % 10;
    }

    private static bool IsValidIsbn10(string isbn10)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var c = isbn10[i];
            int value;
            if (char.IsAsciiDigit(c))
            {
                value = c - '0';
            }
            else if (c == 'X' && i == 9)
            {
                value = 10;
            }
            else
            {
                return false;
            }

            sum += (10 - i) * value;
        }

        return sum % 11 == 0;
    }
}
