using System.Net;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Services;

/// <summary>Renders recruiting message templates for the applicant. The applicant NAME is ALWAYS replaced
/// by a redaction block; this is the single place that guarantees the real name never reaches the applicant.</summary>
public static partial class BewerbungTemplateRenderer
{
    /// <summary>Fixed-width redaction block shown instead of a name.</summary>
    public const string Redaction = "███████";

    /// <summary>Prepare a template body for insertion into an applicant letter: redact NAME, fill DIENSTGRAD
    /// (the sender's rank); DATUM/UHRZEIT stay as tokens for the HRB member to fill in.</summary>
    public static string RenderForApplicant(string html, string? dienstgrad)
    {
        html = NameToken().Replace(html, Redaction);
        if (!string.IsNullOrWhiteSpace(dienstgrad))
        {
            html = RankToken().Replace(html, WebUtility.HtmlEncode(dienstgrad));
        }
        return html;
    }

    /// <summary>Defense in depth before persisting an applicant-facing message: redact the NAME token and any
    /// literal occurrence of the applicant's real name. HTML-safe — only visible text is rewritten, never tags.</summary>
    public static string Redact(string html, string? applicantName)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html ?? string.Empty;
        }
        var name = applicantName?.Trim();
        // rewrite only the text segments, never the inside of < ... > tags, so markup/attributes stay intact
        return TagOrText().Replace(html, m =>
        {
            if (m.Value.StartsWith('<'))
            {
                return m.Value;
            }
            var text = NameToken().Replace(m.Value, Redaction);
            if (!string.IsNullOrWhiteSpace(name))
            {
                text = Regex.Replace(text, Regex.Escape(name), Redaction, RegexOptions.IgnoreCase);
            }
            return text;
        });
    }

    /// <summary>Flatten template HTML to plain text (paragraph/break aware) for the plain-text conversation.</summary>
    public static string HtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }
        var text = BlockBreak().Replace(html, "\n");
        text = TagStrip().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = ExcessBlankLines().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"</p>|<br\s*/?>|</li>|</h[1-6]>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreak();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagStrip();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessBlankLines();

    [GeneratedRegex(@"\bNAME\b")]
    private static partial Regex NameToken();

    [GeneratedRegex(@"\bDIENSTGRAD\b")]
    private static partial Regex RankToken();

    [GeneratedRegex(@"<[^>]+>|[^<]+")]
    private static partial Regex TagOrText();
}
