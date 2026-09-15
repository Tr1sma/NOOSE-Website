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
                if (property.CurrentValue is not string html || !NeedsRewrite(html))
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
            var replaced = RichTextImageFields.For(type).Contains(field)
                ? await ReplaceImagesAsync(ctx, storage, type.Name, entityId, stored, cancellationToken)
                : stored;
            if (ReferenceEquals(stored, html) && ReferenceEquals(replaced, stored))
            {
                continue;
            }
            entry.Property(field).CurrentValue = HtmlCleanup.Clean(replaced);
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

    // a base64 image inside src, single quotes included because hand-written html uses them
    [GeneratedRegex("""\ssrc\s*=\s*["']data:(?<type>image/[^;"']+);base64,(?<data>[^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex ImagePattern();
}
