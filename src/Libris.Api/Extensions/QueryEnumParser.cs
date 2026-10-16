using System.Text.Json;

namespace Libris.Api.Extensions;

public static class QueryEnumParser
{
    /// <summary>Parses the same snake_case names the JSON uses ("not_started"); minimal API binding only knows "NotStarted".</summary>
    public static bool TryParse<TEnum>(string? value, out TEnum? result)
        where TEnum : struct, Enum
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(JsonNamingPolicy.SnakeCaseLower.ConvertName(candidate.ToString()), value, StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        return false;
    }
}
