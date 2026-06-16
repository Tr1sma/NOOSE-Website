using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IQuelleService" />
public class SourceService(IDbContextFactory<AppDbContext> dbFactory, ISourcesStorageService storage) : ISourceService
{
    public async Task<List<Source>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Verschlusssache-Schutz: Sichtbarkeit der Eltern-Akte prüfen (Person/Fraktion/Gruppe – keine
        // FK-Navigation bei polymorphen Assoziationen → zentraler Sichtbarkeits-Helfer). meId mitgeben,
        // damit Taskforce-Mitglieder (Nicht-Führung) ihre Quellen sehen (Mitgliedschafts-Sichtbarkeit).
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, isLeadership, cancellationToken, meId))
        {
            return new();
        }

        return await db.Sources
            .Where(q => q.EntityType == entityType && q.EntityId == entityId)
            // Angepinnte zuerst, danach die zuletzt erstellten.
            .OrderByDescending(q => q.Pinned)
            .ThenByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Source> CreateAsync(string entityType, string entityId, SourceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var title = input.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var visDb = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(visDb, entityType, entityId, actor.IsLeadership(), cancellationToken))
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
        };

        switch (input.Type)
        {
            case SourceType.Link:
                if (string.IsNullOrWhiteSpace(input.Url))
                {
                    throw new InvalidOperationException("Bei einer Link-Quelle ist eine URL erforderlich.");
                }
                var url = input.Url.Trim();
                // Nur http(s) zulassen – verhindert z. B. javascript:-Links (stored-XSS-Vektor in der Anzeige).
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
                // Größenlimit serverseitig erzwingen (nicht nur in der UI).
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
                // Inhalt steckt in der Beschreibung – nichts weiter nötig.
                break;

            case SourceType.Document:
                if (string.IsNullOrWhiteSpace(input.TargetId))
                {
                    throw new InvalidOperationException("Bei einer Dokument-Quelle ist ein Ziel-Dokument erforderlich.");
                }
                // Existenz + Sichtbarkeit des referenzierten Dokuments prüfen (kein Verweis auf VS-Dokumente
                // für Nicht-Führung; kein Existenz-Leak).
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
            // DB-Insert fehlgeschlagen → bereits geschriebene Datei wieder entfernen (kein verwaister Anhang).
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
        if (!await Visibility.IsRecordVisibleAsync(db, source.EntityType, source.EntityId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }
        // Soft-Delete via Interceptor. Die physische Datei bleibt erhalten (Wiederherstellung möglich);
        // endgültiges Entfernen erst beim Hard-Delete (späterer Cleanup, siehe Plan).
        db.Sources.Remove(source);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PinSetAsync(string sourceId, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // ExecuteUpdate umgeht den Audit-/Nur-Lese-Interceptor → Schreibrecht hier explizit durchsetzen
        // (ein hochrangiger Nur-Leser bestünde sonst keine Mutations-Sperre). Anpinnen ist an das
        // Schreibrecht der Akte gekoppelt – wer Quellen hinzufügen/löschen darf, darf auch anpinnen.
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Source laden, um EntityType/EntityId für den VS-Check zu kennen.
        var source = await db.Sources.FirstOrDefaultAsync(q => q.Id == sourceId, cancellationToken)
            ?? throw new InvalidOperationException("Quelle nicht gefunden.");
        if (!await Visibility.IsRecordVisibleAsync(db, source.EntityType, source.EntityId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }
        // Bewusst per ExecuteUpdate statt SaveChanges: setzt nur das Flag ohne Audit-Interceptor
        // (Anpinnen ist keine inhaltliche Bearbeitung → kein GeaendertAm/Von-Stempel).
        await db.Sources
            .Where(q => q.Id == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.Pinned, pinned), cancellationToken);
    }

    public async Task<Source?> GetForDownloadAsync(string sourceId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Sources.FirstOrDefaultAsync(q => q.Id == sourceId, cancellationToken);
        if (source is null || source.Type != SourceType.Upload || string.IsNullOrEmpty(source.FileNameSaved))
        {
            return null;
        }
        // meId für Taskforce-Mitgliedschafts-Sichtbarkeit (sonst sehen Nicht-Führungs-Mitglieder ihre
        // hochgeladenen Anhänge nicht).
        return await Visibility.IsRecordVisibleAsync(db, source.EntityType, source.EntityId, isLeadership, cancellationToken, meId)
            ? source
            : null;
    }

    /// <summary>Lässt nur absolute http(s)-URLs zu (Schutz vor javascript:/data:-Links in der Anzeige).</summary>
    private static bool IsSafeUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

}
