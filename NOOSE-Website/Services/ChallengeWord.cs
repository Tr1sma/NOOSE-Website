namespace NOOSE_Website.Services;

/// <summary>Random type-to-confirm words for destructive dialogs.</summary>
public static class ChallengeWord
{
    // no I/O/Q — indistinguishable from 1/0 in most fonts
    private const string Letters = "ABCDEFGHJKLMNPRSTUVWXYZ";

    /// <summary>An uppercase word of the given length drawn from non-confusable letters.</summary>
    public static string Generate(int length = 6)
    {
        if (length < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Die Länge muss mindestens 1 betragen.");
        }
        return string.Concat(Enumerable.Range(0, length).Select(_ => Letters[Random.Shared.Next(Letters.Length)]));
    }

    /// <summary>True if the typed input matches the challenge, ignoring case and surrounding whitespace.</summary>
    public static bool Matches(string? typed, string challenge)
        => string.Equals(typed?.Trim(), challenge, StringComparison.OrdinalIgnoreCase);
}
