using System.Text.RegularExpressions;

namespace TitleFlow.Api.Application.Services;

public static partial class TitleRules
{
    public const int MaximumReferenceTitleLength = 700;

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex NonAlphaNumeric();
    public static string Normalize(string? title) => string.IsNullOrWhiteSpace(title) ? string.Empty : NonAlphaNumeric().Replace(title, string.Empty).ToLowerInvariant();
    public static bool IsFinancialYear(string? year)
    {
        if (string.IsNullOrWhiteSpace(year)) return false;
        var parts = year.Split('-');
        return parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end) && start is >= 1999 and <= 2099 && end == (start + 1) % 100;
    }

    public static string? Validate(string? title, string? invoice, string? code, string? year)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Title is required.";
        if (string.IsNullOrWhiteSpace(invoice)) return "Invoice number is required.";
        if (string.IsNullOrWhiteSpace(code)) return "Code reference is required.";
        if (title.Trim().Length > 1200) return "Title cannot exceed 1200 characters.";
        if (invoice.Trim().Length > 250) return "Invoice number cannot exceed 250 characters.";
        if (code.Trim().Length > 220) return "Code reference cannot exceed 220 characters.";
        if (!IsFinancialYear(year)) return "Financial year must use the format 2026-27.";
        if (Normalize(title).Length > MaximumReferenceTitleLength) return "Normalized title cannot exceed 700 characters.";
        return null;
    }
}
