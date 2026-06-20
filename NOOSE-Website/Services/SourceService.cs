using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISourceService" />
public class SourceService(IDbContextFactory<AppDbContext> dbFactory, ISourcesStorageService storage) : ISourceService
{
    public async Task<List<Source>> GetForRecordAsync(string entityType, string entityId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // parent visibility (scope carries meId for taskforce membership)
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, scope, cancellationToken))
        {
            return new();
        }

        var sources = await db.Sources
            .Where(q => q.EntityType == entityType && q.EntityId == entityId)
            .OrderByDescending(q => q.Pinned)
            .ThenByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
        if (scope.PartnerAgency is { } agency)
        {
            // partners: drop cross-ref source types, then apply child-release filter
            sources = sources.Where(q => q.Type != SourceType.Internal && q.Type != SourceType.Document).ToList();
            sources = await PartnerVisibility.FilterChildrenAsync(db, entityType, entityId, nameof(Source), sources, q => q.Id, agency, scope.MeId, cancellationToken);
        }

        // taskforce-internal sources: only taskforce members may see them, never partners
        if (entityType == nameof(Taskforce))
        {
            bool isMember = scope.MeId is not null && !scope.IsPartner &&
                await db.TaskforceAgents.AnyAsync(
                    ta => ta.TaskforceId == entityId && ta.AgentId == scope.MeId, cancellationToken);
            if (!isMember)
                sources = sources.Where(s => !s.IsInternalOnly).ToList();
        }

        return sources;
    }

    public async Task<Source> CreateAsync(string entityType, string entityId, SourceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var title = input.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var visDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(visDb, entityType, entityId, ViewerScope.From(actor), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }

        var source = new Source
        {
            EntityType = entityType,
            EntityId = entityId,
            Type = input.Type,
            Title = title,
            Description = input.Description.TrimToNull(),
            IsInternalOnly = input.IsInternalOnly,
        };

        switch (input.Type)
        {
            case SourceType.Link:
                if (string.IsNullOrWhiteSpace(input.Url))
                {
                    throw new InvalidOperationException("Bei einer Link-Quelle ist eine URL erforderlich.");
                }
                var url = input.Url.Trim();
                // http(s) only, blocks stored-XSS via javascript: links
                if (!IsSafeUrl(url))
                {
                    throw new InvalidOperationException("Bitte eine gültige http(s)-URL angeben.");
                }
                source.Url = url;
                break;

            case SourceType.Internal:
                if (string.IsNullOrWhiteSpace(input.TargetType) || string.IsNullOrWhiteSpace(input.TargetId))
                {
                    throw new InvalidOperationException("Bei einer internen Quelle ist eine Ziel-Akte erforderlich.");
                }
                source.TargetType = input.TargetType;
                source.TargetId = input.TargetId;
                break;

            case SourceType.Upload:
                if (input.FileContent is null || input.FileContent.Length == 0)
                {
                    throw new InvalidOperationException("Es wurde keine Datei ausgewählt.");
                }
                // enforce size limit server-side
                if (input.FileContent.Length > storage.MaxBytes)
                {
                    throw new InvalidOperationException($"Datei zu groß (max. {storage.MaxBytes / (1024 * 1024)} MB).");
                }
                if (!storage.IsAllowedType(input.ContentType ?? string.Empty))
                {
                    throw new InvalidOperationException($"Dateityp „{input.ContentType}“ ist nicht erlaubt.");
                }
                await using (var ms = new MemoryStream(input.FileContent))
                {
                    source.FileNameSaved = await storage.SaveAsync(ms, input.OriginalName ?? "datei", cancellationToken);
                }
                source.OriginalName = input.OriginalName;
                source.ContentType = input.ContentType;
                source.SizeBytes = input.SizeBytes;
                break;

            case SourceType.FreeText:
                break;

            case SourceType.Document:
                if (string.IsNullOrWhiteSpace(input.TargetId))
                {
                    throw new InvalidOperationException("Bei einer Dokument-Quelle ist ein Ziel-Dokument erforderlich.");
                }
                // verify referenced document is visible; no existence leak of classified docs
                await using (var checkDb = await dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    var classifiedFlag = await checkDb.Documents
                        .Where(d => d.Id == input.TargetId)
                        .Select(d => (bool?)d.IsClassified)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (classifiedFlag is null || (classifiedFlag == true && !actor.IsLeadership()))
                    {
                        throw new InvalidOperationException("Das gewählte Dokument wurde nicht gefunden.");
                    }
                }
                source.TargetType = nameof(Document);
                source.TargetId = input.TargetId;
                break;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Sources.Add(source);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch when (source.Type == SourceType.Upload && source.FileNameSaved is not null)
        {
            // insert failed, remove the already-written file
            storage.Delete(source.FileNameSaved);
            throw;
        }
        return source;
    }

    public async Task RemoveAsync(string sourceId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Sources.FirstOrDefaultAsync(q => q.Id == sourceId, cancellationToken);
        if (source is null)
        {
            return;
        }
        if (!await Visibility.IsRecordVisibleAsync(db, source.EntityType, source.EntityId, ViewerScope.From(actor), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }
        // soft delete keeps the file for restore; removed on hard delete
        db.Sources.Remove(source);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PinSetAsync(string sourceId, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // ExecuteUpdate bypasses the interceptors, so enforce write access here
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Sources.FirstOrDefaultAsync(q => q.Id == sourceId, cancellationToken)
            ?? throw new InvalidOperationException("Quelle nicht gefunden.");
        if (!await Visibility.IsRecordVisibleAsync(db, source.EntityType, source.EntityId, ViewerScope.From(actor), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }
        // ExecuteUpdate on purpose: pinning is not a content edit, skip the audit stamp
        await db.Sources
            .Where(q => q.Id == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.Pinned, pinned), cancellationToken);
    }

    public async Task<Source?> GetForDownloadAsync(string sourceId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Sources.FirstOrDefaultAsync(q => q.Id == sourceId, cancellationToken);
        if (source is null || source.Type != SourceType.Upload || string.IsNullOrEmpty(source.FileNameSaved))
        {
            return null;
        }
        if (scope.PartnerAgency is { } agency)
        {
            // partners: parent visible AND (whole-record or this source released)
            return await PartnerVisibility.IsChildVisibleToPartnerAsync(db, source.EntityType, source.EntityId, nameof(Source), sourceId, agency, scope.MeId, cancellationToken)
                ? source
                : null;
        }
        return await Visibility.IsRecordVisibleAsync(db, source.EntityType, source.EntityId, scope, cancellationToken)
            ? source
            : null;
    }

    /// <summary>Allows only absolute http(s) URLs (blocks javascript:/data: links).</summary>
    private static bool IsSafeUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

}
