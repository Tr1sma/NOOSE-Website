using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure;

/// <summary>Moves base64 images of the registered rich-text columns into files during the same SaveChanges.</summary>
/// <remarks>
/// Registered after the write barrier but before the audit interceptor: the barrier has to veto first, and the
/// TextImage rows added here still need their stamp from the interceptor that follows. The column is sanitized
/// again afterwards, because the services clean before saving and this rewrites their output. A picture that
/// cannot be stored — too large, unknown type, broken base64 — stays inline, so nothing is ever lost. The same
/// pass folds an image line with its caption line into figure/figcaption, the stored shape.
/// </remarks>
public sealed partial class RichTextHtmlInterceptor(IServiceScopeFactory scopes) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var candidates = Candidates(eventData);
        if (candidates.Count > 0)
        {
            using var scope = scopes.CreateScope();
            ProcessAsync(eventData, candidates, scope.ServiceProvider.GetRequiredService<ITextImageStorageService>(), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var candidates = Candidates(eventData);
        if (candidates.Count > 0)
        {
            await using var scope = scopes.CreateAsyncScope();
            await ProcessAsync(eventData, candidates, scope.ServiceProvider.GetRequiredService<ITextImageStorageService>(), cancellationToken);
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        SweepAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await SweepAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Forget(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>Drops a pending sweep whose save never happened.</summary>
    /// <remarks>
    /// Load-bearing. The note holds the html as it would have been stored; left behind after a failed save, the
    /// next successful save on the same context would clean up against it and delete a picture the text does
    /// reference - the one case where housekeeping would cost data.
    /// </remarks>
    private static void Forget(DbContext? context)
    {
        if (context is not null)
        {
            Pending.Remove(context);
        }
    }

    /// <summary>What a finished save leaves to clean up: the column before and after, per carrier.</summary>
    /// <remarks>
    /// Both halves, not just the new one. A TextImage row names the record whose visibility governs it, and a
    /// comment hands that record's own type and id to the store (<c>CommentPanel</c> passes EntityType/EntityId
    /// straight through) - so a picture pasted into a comment on a document is filed under "Document". Sweeping
    /// everything the document's own column does not mention would have deleted exactly those, file included.
    /// Only what this column held before and no longer holds is this column's to remove.
    /// </remarks>
    private sealed record Sweep(string EntityType, string EntityId, string Before, string After);

    // Handed from SavingChanges to SavedChanges. Keyed on the context because this interceptor is a singleton
    // and every operation brings its own short-lived one; the table drops the entry with the context.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, List<Sweep>> Pending = new();

    /// <summary>Removes the pictures the saved text no longer points at, row and file.</summary>
    /// <remarks>
    /// After the commit and on a context of its own, deliberately: a query inside SavingChanges shares the save's
    /// connection, and a picture must not be deleted for a write that then fails. Nothing here may throw either -
    /// leftover bytes are a housekeeping problem, a broken save is not.
    /// <para>
    /// The row goes with ExecuteDelete rather than the soft-delete path: a picture nothing references any more is
    /// not record material, and its carrier's change is audited anyway. Leaving the row behind while deleting the
    /// file would be the worst of both - a record pointing at nothing.
    /// </para>
    /// </remarks>
    private async Task SweepAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !Pending.TryGetValue(context, out var sweeps))
        {
            return;
        }
        Pending.Remove(context);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var storage = scope.ServiceProvider.GetRequiredService<ITextImageStorageService>();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            foreach (var sweep in sweeps)
            {
                var gegangen = Referenced(sweep.Before)
                    .Where(id => !sweep.After.Contains(id, StringComparison.Ordinal))
                    .ToList();
                if (gegangen.Count == 0)
                {
                    continue;
                }
                // type and id still bound the query: an id out of one record's text must never reach another's row
                var verwaist = await db.TextImages.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.EntityType == sweep.EntityType && t.EntityId == sweep.EntityId
                        && gegangen.Contains(t.Id))
                    .Select(t => new { t.Id, t.FileNameSaved })
                    .ToListAsync(cancellationToken);
                if (verwaist.Count == 0)
                {
                    continue;
                }
                var ids = verwaist.Select(r => r.Id).ToList();
                await db.TextImages.IgnoreQueryFilters()
                    .Where(t => ids.Contains(t.Id))
                    .ExecuteDeleteAsync(cancellationToken);
                foreach (var row in verwaist)
                {
                    storage.Delete(row.FileNameSaved);
                }
            }
        }
        catch
        {
            /* best effort */
        }
    }

    /// <summary>Whether a column is worth a pass: a picture to file away, a caption to fold, or a heading to anchor.</summary>
    /// <remarks>
    /// The heading arm is load-bearing. Without it the anchor pass only ever saw texts that happened to carry an
    /// image, so a long document with a table of contents and no picture got no ids at all and every entry of its
    /// own table of contents pointed nowhere — silently, and exactly in the case the feature exists for.
    /// Each sub-pass returns its input unchanged when it finds nothing, so a false positive here costs one parse.
    /// </remarks>
    private static bool NeedsRewrite(string html)
        => html.Contains("data:image", StringComparison.OrdinalIgnoreCase)
        || html.Contains(RichTextFigure.CaptionClass, StringComparison.Ordinal)
        || html.Contains("<h1", StringComparison.OrdinalIgnoreCase)
        || html.Contains("<h2", StringComparison.OrdinalIgnoreCase)
        || html.Contains("<h3", StringComparison.OrdinalIgnoreCase);

    /// <summary>Changed rich-text columns that may hold a picture, a caption or a heading; empty in the common case.</summary>
    private static List<(EntityEntry Entry, string Field)> Candidates(DbContextEventData eventData)
    {
        var candidates = new List<(EntityEntry, string)>();
        if (eventData.Context is not AppDbContext ctx)
        {
            return candidates;
        }
        ctx.ChangeTracker.DetectChanges();
        foreach (var entry in ctx.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }
            // the wider list: anchors reach carriers whose images stay inline
            foreach (var field in RichTextAnchorFields.For(entry.Entity))
            {
                var property = entry.Property(field);
                if (entry.State is EntityState.Modified && !property.IsModified)
                {
                    continue;
                }
                // an image carrier is always worth the pass, even with nothing left to rewrite: removing the last
                // picture from a text leaves html that needs no rewriting at all, and the sweep afterwards is the
                // only thing that takes its file and its row with it
                var traegtBilder = RichTextImageFields.For(entry.Metadata.ClrType).Contains(field);
                if (property.CurrentValue is not string html || !(traegtBilder || NeedsRewrite(html)))
                {
                    continue;
                }
                if (entry.Property("Id").CurrentValue is not string id || id.Length == 0)
                {
                    continue;
                }
                candidates.Add((entry, field));
            }
        }
        return candidates;
    }

    private static async Task ProcessAsync(DbContextEventData eventData, List<(EntityEntry Entry, string Field)> candidates,
        ITextImageStorageService storage, CancellationToken cancellationToken)
    {
        var ctx = (AppDbContext)eventData.Context!;
        foreach (var (entry, field) in candidates)
        {
            var html = (string)entry.Property(field).CurrentValue!;
            var entityId = (string)entry.Property("Id").CurrentValue!;
            var type = entry.Metadata.ClrType;
            var stored = RichTextFigure.ToStored(html);
            stored = RichTextAnchors.ToStored(stored);
            // images only for the carriers whose pictures the internal endpoint may serve; anchors reach further,
            // so a handbook article gets its heading ids while its base64 deliberately stays inline
            var traegtBilder = RichTextImageFields.For(type).Contains(field);
            var replaced = traegtBilder
                ? await ReplaceImagesAsync(ctx, storage, type.Name, entityId, stored, cancellationToken)
                : stored;
            // CleanWithAnchors, not Clean: the ids RichTextAnchors has just written are dropped by the ordinary
            // pass, which is exactly what keeps a pasted anchor out of every other rich-text field
            var sauber = HtmlCleanup.CleanWithAnchors(replaced);
            // an image swapped out or deleted leaves its row and its file behind; the sweep after the commit
            // removes what this column pointed at before and no longer does. Added rows have no before, so
            // nothing of theirs can have gone missing.
            if (traegtBilder && entry.State == EntityState.Modified
                && entry.Property(field).OriginalValue is string vorher && vorher.Length > 0)
            {
                Pending.GetOrCreateValue(ctx).Add(new Sweep(type.Name, entityId, vorher, sauber));
            }
            if (ReferenceEquals(stored, html) && ReferenceEquals(replaced, stored))
            {
                continue;
            }
            entry.Property(field).CurrentValue = sauber;
        }
    }

    private static async Task<string> ReplaceImagesAsync(AppDbContext ctx, ITextImageStorageService storage,
        string entityType, string entityId, string html, CancellationToken cancellationToken)
    {
        var matches = ImagePattern().Matches(html);
        if (matches.Count == 0)
        {
            return html;
        }
        var builder = new StringBuilder(html.Length);
        var position = 0;
        foreach (Match match in matches)
        {
            builder.Append(html, position, match.Index - position);
            position = match.Index + match.Length;
            var url = await StoreFileAsync(ctx, storage, entityType, entityId,
                match.Groups["type"].Value, match.Groups["data"].Value, cancellationToken);
            // the match starts at the whitespace, so the replacement has to bring it back
            builder.Append(url is null ? match.Value : $" src=\"{url}\"");
        }
        builder.Append(html, position, html.Length - position);
        return builder.ToString();
    }

    /// <summary>Stores one picture and returns the URL for its img tag; null keeps the base64 in place.</summary>
    private static async Task<string?> StoreFileAsync(AppDbContext ctx, ITextImageStorageService storage,
        string entityType, string entityId, string contentType, string data, CancellationToken cancellationToken)
    {
        var type = contentType.Trim();
        if (!storage.IsAllowedType(type))
        {
            return null;
        }
        var buffer = new byte[data.Length / 4 * 3];
        if (!Convert.TryFromBase64String(data, buffer, out var written) || written == 0
            || written > storage.MaxBytes)
        {
            return null;
        }
        using var content = new MemoryStream(buffer, 0, written);
        var fileName = await storage.SaveAsync(content, type, cancellationToken);
        var row = new TextImage
        {
            EntityType = entityType,
            EntityId = entityId,
            FileNameSaved = fileName,
            ContentType = type,
            SizeBytes = written,
        };
        ctx.Add(row);
        return $"/dateien/textbilder/{row.Id}";
    }

    /// <summary>Ids of the stored pictures this html points at, by their delivery url.</summary>
    /// <remarks>By url only. The token form <c>@{TextImage:Id}</c> belongs to the plain-text fields, and a row
    /// reached that way is not this column's to account for even when it is filed under the same record.</remarks>
    private static List<string> Referenced(string html)
        => [.. StoredPattern().Matches(html).Select(m => m.Groups["id"].Value).Distinct(StringComparer.Ordinal)];

    // a base64 image inside src, single quotes included because hand-written html uses them
    [GeneratedRegex("""\ssrc\s*=\s*["']data:(?<type>image/[^;"']+);base64,(?<data>[^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex ImagePattern();

    [GeneratedRegex("/dateien/textbilder/(?<id>[0-9a-fA-F-]{36})")]
    private static partial Regex StoredPattern();
}
