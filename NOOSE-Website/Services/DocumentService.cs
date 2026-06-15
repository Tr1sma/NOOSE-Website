using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDokumentService" />
public class DocumentService(IDbContextFactory<AppDbContext> dbFactory) : IDocumentService
{
    public async Task<List<DocumentListItem>> GetListAsync(bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Documents
            .Where(d => isLeadership || !d.IsClassified)
            // Angepinnte zuerst, danach die zuletzt geänderten. Die Seite trennt beide Gruppen optisch.
            .OrderByDescending(d => d.Pinned)
            .ThenByDescending(d => d.ModifiedAt ?? d.CreatedAt)
            .Select(d => new DocumentListItem(d.Id, d.Title, d.Category, d.IsClassified, d.ModifiedAt ?? d.CreatedAt, d.Pinned))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DocumentListItem>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Documents.Where(d => isLeadership || !d.IsClassified);

        var s = searchText?.Trim();
        if (!string.IsNullOrWhiteSpace(s))
        {
            query = query.Where(d => d.Title.Contains(s) || (d.Category != null && d.Category.Contains(s)));
        }

        return await query
            .OrderByDescending(d => d.ModifiedAt ?? d.CreatedAt)
            .Take(max)
            .Select(d => new DocumentListItem(d.Id, d.Title, d.Category, d.IsClassified, d.ModifiedAt ?? d.CreatedAt, d.Pinned))
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetAsync(string id, bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null || (document.IsClassified && !isLeadership))
        {
            // Kein Existenz-Leak: nicht vorhanden oder nicht sichtbar → null.
            return null;
        }
        return document;
    }

    public async Task<Document> CreateAsync(DocumentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var title = (input.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        var document = new Document
        {
            Title = title,
            Category = input.Category.TrimToNull(),
            // Maßgebliche Sicherheitskontrolle: HTML serverseitig bereinigen.
            ContentHtml = HtmlCleanup.Clean(input.ContentHtml),
            IsClassified = input.IsClassified,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task RefreshAsync(string id, DocumentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var title = (input.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Dokument nicht gefunden.");

        // Verschlusssachen darf nur die Führung bearbeiten (für Nicht-Führung ist es ohnehin unsichtbar).
        if (document.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Dieses Dokument ist eine Verschlusssache und nur der Führung zugänglich.");
        }

        document.Title = title;
        document.Category = input.Category.TrimToNull();
        document.ContentHtml = HtmlCleanup.Clean(input.ContentHtml);
        document.IsClassified = input.IsClassified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PinSetAsync(string id, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Anpinnen ist Bibliotheks-Kuratierung → der Führung vorbehalten (betrifft die Ansicht aller).
        Permission.RequireLeadership(actor);
        // ExecuteUpdate umgeht den SaveChanges-Interceptor → Nur-Lese-Sperre hier explizit durchsetzen
        // (ein hochrangiger Nur-Leser bestünde sonst den VerlangeFuehrung-Guard).
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Bewusst per ExecuteUpdate statt Laden+SaveChanges: setzt nur das Flag, ohne über den Audit-
        // Interceptor GeaendertAm/GeaendertVonId zu verändern (Anpinnen ist keine inhaltliche Bearbeitung).
        // Der Soft-Delete-Query-Filter greift weiterhin → Papierkorb-Dokumente werden nicht angefasst.
        var affected = await db.Documents
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Pinned, pinned), cancellationToken);
        if (affected == 0)
        {
            throw new InvalidOperationException("Dokument nicht gefunden.");
        }
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null)
        {
            return;
        }

        var isCreator = document.CreatedById is not null && document.CreatedById == actor.GetAgentId();
        if (!isCreator && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Nur der Ersteller oder die Führung darf dieses Dokument löschen.");
        }

        // Remove wird vom AuditSaveChangesInterceptor in einen Soft-Delete (Papierkorb) umgewandelt.
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<DocumentAttachment>> GetAttachmentsAsync(string documentId, bool isLeadership, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sources = await db.Sources
            .Where(q => q.Type == SourceType.Document && q.TargetType == nameof(Document) && q.TargetId == documentId)
            .Select(q => new { q.EntityType, q.EntityId })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sources.Count == 0)
        {
            return new();
        }

        var refs = sources.Select(q => (q.EntityType, q.EntityId)).ToList();
        // Fremde Taskforces gar nicht erst auflösen (meId/Mitgliedschaft) → tauchen unten nicht auf.
        var map = await RecordsReference.ResolveAsync(db, refs, cancellationToken, mayAllTaskforces: isLeadership, meId: meId);

        var result = new List<DocumentAttachment>();
        foreach (var q in sources)
        {
            if (map.TryGetValue((q.EntityType, q.EntityId), out var a))
            {
                // Verschlusssachen-Eltern-Akten für Nicht-Führung ausblenden (kein Namens-Leak).
                if (!isLeadership && a.Classified)
                {
                    continue;
                }
                result.Add(new DocumentAttachment(q.EntityType, q.EntityId, a.Display, a.Href));
            }
        }
        return result;
    }
}
