using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>What the KI-Kurzbrief panel renders: the cached summary plus whether the source changed since.</summary>
public sealed record DossierSummaryView(
    bool Configured,
    bool Exists,
    string? TldrHtml,
    string? SummaryHtml,
    string? Model,
    DateTime? GeneratedAt,
    bool IsStale);

/// <summary>Cached AI dossier summaries per record. The LLM is called only when the source content changed or a
/// regenerate is forced — an unchanged content hash reuses the stored summary.</summary>
public interface IDossierSummaryService
{
    bool IsConfigured { get; }

    /// <summary>Cached summary for a record; never calls the LLM. Null when the viewer may not see the record.</summary>
    Task<DossierSummaryView?> GetAsync(string entityType, string entityId, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Generate the summary, reusing the cache when the content hash is unchanged (unless force=true).</summary>
    Task<DossierSummaryView> GenerateAsync(string entityType, string entityId, ClaimsPrincipal actor, bool force, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IDossierSummaryService" />
public sealed class DossierSummaryService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILlmService llm,
    IOptions<LlmOptions> options) : IDossierSummaryService
{
    private readonly LlmOptions _o = options.Value;

    private const string SystemPrompt =
        "Du bist Auswerter des NOOSE (National Office of Security Enforcement), einer fiktiven Geheimdienst-Behörde " +
        "auf einem GTA-Rollenspiel-Server. Erstelle einen sachlichen Akten-Kurzbrief AUSSCHLIESSLICH aus den unten " +
        "gelieferten Fakten. Erfinde nichts, spekuliere nicht, füge kein Wissen von außerhalb hinzu. Wenn etwas " +
        "unbekannt ist, lass es weg. Antworte auf Deutsch, in Markdown, mit GENAU dieser Struktur:\n\n" +
        "## TL;DR\nZwei bis drei Sätze mit der Kernaussage der Akte.\n\n" +
        "## Zusammenfassung\nAusführliche, gegliederte Zusammenfassung mit Zwischenüberschriften und Aufzählungen: " +
        "wer/was, Einstufung und Gefährdung, wichtige Verbindungen, Verlauf/Ereignisse, offene Punkte.";

    public bool IsConfigured => _o.IsConfigured;

    public async Task<DossierSummaryView?> GetAsync(string entityType, string entityId, ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var scope = ViewerScope.From(viewer);
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, scope, cancellationToken))
        {
            return null;
        }

        var row = await db.DossierSummaries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityType == entityType && x.EntityId == entityId, cancellationToken);
        if (row is null)
        {
            return new DossierSummaryView(IsConfigured, false, null, null, null, null, false);
        }

        // Staleness: rebuild the context hash (DB reads only, no LLM call) and compare.
        var context = await DossierContextBuilder.BuildAsync(db, entityType, entityId, cancellationToken);
        var stale = context is null || Hash(PromptRedactor.Clip(context.Value.Text)) != row.ContentHash;
        return View(row, stale);
    }

    public async Task<DossierSummaryView> GenerateAsync(string entityType, string entityId, ClaimsPrincipal actor, bool force, CancellationToken cancellationToken = default)
    {
        Permission.RequireLlmUse(actor);
        Permission.RequireWriteAccess(actor);
        if (!_o.IsConfigured)
        {
            throw new InvalidOperationException("Der KI-Assistent ist nicht konfiguriert.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var scope = ViewerScope.From(actor);
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, scope, cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht sichtbar.");
        }

        var context = await DossierContextBuilder.BuildAsync(db, entityType, entityId, cancellationToken)
            ?? throw new InvalidOperationException("Akte nicht gefunden.");

        // Last gate before egress: classified content never leaves unless explicitly allowed.
        PromptRedactor.GuardClassified(context.IsClassified, _o);

        var userPrompt = PromptRedactor.Clip(context.Text);
        var hash = Hash(userPrompt);

        var existing = await db.DossierSummaries
            .FirstOrDefaultAsync(x => x.EntityType == entityType && x.EntityId == entityId, cancellationToken);

        // Unchanged source and no force → reuse, no LLM call.
        if (!force && existing is not null && existing.ContentHash == hash)
        {
            return View(existing, stale: false);
        }

        var answer = await llm.ChatAsync(SystemPrompt, userPrompt, actor, cancellationToken);
        var (tldr, body) = SplitSections(answer);

        if (existing is null)
        {
            existing = new DossierSummary { EntityType = entityType, EntityId = entityId };
            db.DossierSummaries.Add(existing);
        }
        existing.ContentHash = hash;
        existing.TldrHtml = Nullify(MarkdownRenderer.ToSafeHtml(tldr));
        existing.SummaryHtml = Nullify(MarkdownRenderer.ToSafeHtml(body));
        existing.Model = llm.Model;
        existing.GeneratedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return View(existing, stale: false);
    }

    private DossierSummaryView View(DossierSummary r, bool stale)
        => new(IsConfigured, true, r.TldrHtml, r.SummaryHtml, r.Model, r.GeneratedAt, stale);

    private static string? Nullify(string? html) => string.IsNullOrWhiteSpace(html) ? null : html;

    // the score-recalculation timestamp advances on every daily sweep even when nothing changed;
    // excluding it keeps a brief from being falsely marked stale after each nightly recompute
    private static readonly Regex VolatileHashLines = new(@"(?im)^Score berechnet am:.*$\r?\n?", RegexOptions.Compiled);

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(VolatileHashLines.Replace(text, string.Empty))));

    /// <summary>Split the model's "## TL;DR" / "## Zusammenfassung" markdown into (tldr, body); falls back to (null, whole).</summary>
    public static (string? Tldr, string Body) SplitSections(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return (null, string.Empty);
        }
        var text = markdown.Replace("\r\n", "\n").Trim();

        var bodyHeading = Regex.Match(text, @"(?im)^\s{0,3}#{1,3}\s*(zusammenfassung|details|langfassung|langtext)\b.*$");
        if (!bodyHeading.Success)
        {
            return (null, text);
        }

        var body = text[(bodyHeading.Index + bodyHeading.Length)..].Trim();
        var head = text[..bodyHeading.Index];
        var tldrHeading = Regex.Match(head, @"(?im)^\s{0,3}#{1,3}\s*(tl;?dr|kurzfassung|kurz)\b.*$");
        var tldr = tldrHeading.Success
            ? head[(tldrHeading.Index + tldrHeading.Length)..].Trim()
            : head.Trim();
        return (string.IsNullOrWhiteSpace(tldr) ? null : tldr, body);
    }
}
