using System.Text;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Services.Public;

/// <summary>Rules for the URL segment of a public page.</summary>
/// <remarks>
/// A slug reaches the outside world inside a route, so it is validated on write rather than escaped on read:
/// lowercase ASCII letters, digits and single hyphens, nothing else. <see cref="Normalize"/> turns a German title
/// into such a slug so an author never has to type one by hand.
/// </remarks>
public static partial class PublicPageSlug
{
    public const int MaxLength = 64;
    public const int MinLength = 2;

    /// <summary>True for a slug that may be stored and routed.</summary>
    public static bool IsValid(string? slug)
        => slug is not null
            && slug.Length is >= MinLength and <= MaxLength
            && Shape().IsMatch(slug);

    /// <summary>Folds a title into a slug; returns an empty string when nothing usable is left.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // umlauts first: lowercasing alone would leave "ü", and stripping it would glue the neighbours together
        var text = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            text.Append(char.ToLowerInvariant(c) switch
            {
                'ä' => "ae",
                'ö' => "oe",
                'ü' => "ue",
                'ß' => "ss",
                var lower => lower.ToString(),
            });
        }

        var hyphenated = Filler().Replace(text.ToString(), "-").Trim('-');
        return hyphenated.Length <= MaxLength ? hyphenated : hyphenated[..MaxLength].Trim('-');
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Shape();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Filler();
}
