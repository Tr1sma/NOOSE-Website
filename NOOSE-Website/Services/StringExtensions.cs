namespace NOOSE_Website.Services;

/// <summary>Shared string helpers for the service layer.</summary>
public static class StringExtensions
{
    /// <summary>Trims the value; returns null if empty or whitespace.</summary>
    public static string? TrimToNull(this string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
