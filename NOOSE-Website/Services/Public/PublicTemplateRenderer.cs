using System.Text.RegularExpressions;

namespace NOOSE_Website.Services.Public;

/// <summary>The fourth token system: templates for citizen-facing messages.</summary>
/// <remarks>
/// Deliberately not built on BewerbungTemplateRenderer. That one HTML-encodes every substitution because an applicant
/// letter is markup; here the target is a plain-text message column rendered as text, so encoding would deliver
/// "Müller &amp; Sohn" to the citizen. Nothing is shared with PlaceholderService ({{…}}) or MentionParser (@{Typ:GUID})
/// either — a template carrying one of their tokens is refused on save rather than half-expanded on send.
/// </remarks>
public static partial class PublicTemplateRenderer
{
    /// <summary>Block shown instead of a sender name, same width as the recruiting path.</summary>
    public const string Redaction = "███████";

    /// <summary>Salutation when no citizen name may be used — an anonymous tip is why this exists.</summary>
    public const string CitizenFallback = "Bürger/in";

    /// <summary>Stands in for a case number that has not been minted yet; only the preview ever shows it.</summary>
    private const string CaseNumberFallback = "—";

    /// <summary>Fills the public token set. Tokens without a value fall back; unknown words stay untouched.</summary>
    public static string Render(string? text, PublicTemplateContext context)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var citizen = string.IsNullOrWhiteSpace(context.CitizenName) ? CitizenFallback : context.CitizenName.Trim();
        var caseNumber = string.IsNullOrWhiteSpace(context.CaseNumber) ? CaseNumberFallback : context.CaseNumber.Trim();
        var now = context.Now ?? DateTime.Now;

        // redaction first, like the recruiting path: nothing that was substituted in can be hit by it afterwards
        text = NameToken().Replace(text, Redaction);
        // no HTML encoding anywhere here: the result is stored and shown as text
        text = CitizenToken().Replace(text, _ => citizen);
        text = CaseNumberToken().Replace(text, _ => caseNumber);
        text = DateToken().Replace(text, _ => now.ToString("dd.MM.yyyy"));
        return TimeToken().Replace(text, _ => now.ToString("HH:mm"));
    }

    /// <summary>True when the text carries a token of one of the three other systems.</summary>
    /// <remarks>
    /// DATUM and UHRZEIT are shared with the recruiting set on purpose — same spelling, same meaning, and this
    /// renderer fills them. Everything else would travel to a citizen unexpanded.
    /// </remarks>
    public static bool HasForeignToken(string? text)
        => !string.IsNullOrEmpty(text)
            && (PlaceholderToken().IsMatch(text)
                || MentionParser.Parse(text).Count > 0
                || RecruitingToken().IsMatch(text));

    /// <summary>Sample values for the editor's preview; never used on a real message.</summary>
    public static PublicTemplateContext SampleContext(DateTime? now = null)
        => new("Max Mustermann", "NOOSE-T-2026-0001", now ?? DateTime.Now);

    [GeneratedRegex(@"\bBUERGER\b")]
    private static partial Regex CitizenToken();

    [GeneratedRegex(@"\bAKTENZEICHEN\b")]
    private static partial Regex CaseNumberToken();

    [GeneratedRegex(@"\bDATUM\b")]
    private static partial Regex DateToken();

    [GeneratedRegex(@"\bUHRZEIT\b")]
    private static partial Regex TimeToken();

    [GeneratedRegex(@"\bNAME\b")]
    private static partial Regex NameToken();

    [GeneratedRegex(@"\{\{[^}]*\}\}")]
    private static partial Regex PlaceholderToken();

    [GeneratedRegex(@"\b(BEWERBER|DIENSTGRAD)\b")]
    private static partial Regex RecruitingToken();
}

/// <summary>What a template is filled with; a null name is the anonymous case, not an error.</summary>
public sealed record PublicTemplateContext(string? CitizenName, string? CaseNumber, DateTime? Now = null);
