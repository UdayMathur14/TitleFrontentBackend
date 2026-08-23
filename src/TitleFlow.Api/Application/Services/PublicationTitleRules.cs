using System.Text.RegularExpressions;

namespace TitleFlow.Api.Application.Services;

public static partial class PublicationTitleRules
{
    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex NonAlphaNumeric();

    public static string Normalize(string? title) => string.IsNullOrWhiteSpace(title)
        ? string.Empty
        : NonAlphaNumeric().Replace(title, string.Empty).ToLowerInvariant();

    public static string ComboKey(string? paperId, string? lotNumber) =>
        $"{paperId?.Trim()}_{lotNumber?.Trim()}";

    public static bool IsFinancialYear(string? year)
    {
        if (string.IsNullOrWhiteSpace(year)) return false;
        var parts = year.Split('-');
        return parts.Length == 2 && int.TryParse(parts[0], out var start) &&
            int.TryParse(parts[1], out var end) && start is >= 1999 and <= 2099 &&
            end == (start + 1) % 100;
    }
}
