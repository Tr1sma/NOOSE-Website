using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ILibraryService" />
public class LibraryService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILibraryStorageService storage) : ILibraryService
{
    public async Task<List<LibraryFile>> GetListAsync(DocumentViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // local vars so EF parameterizes the classification filters
        bool mayClassified = scope.MayClassified, isTru = scope.IsTru, isHrb = scope.IsHrb;
        return await db.LibraryFiles
            .Where(d => (!d.IsClassified && !d.IsTRUClassified && !d.IsHRBClassified)
                || mayClassified
                || (d.IsTRUClassified && isTru)
                || (d.IsHRBClassified && isHrb))
            .OrderByDescending(d => d.ModifiedAt ?? d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LibraryFile> UploadAsync(string title, string? category, DocumentClassification classification,
        Stream content, string originalName, string contentType, long sizeBytes,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        Permission.RequireMayAssignClassification(actor, classification);

        title = title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Bitte einen Titel angeben.");
        }
        if (!storage.IsAllowedType(contentType))
        {
            throw new InvalidOperationException("Dieser Dateityp ist nicht erlaubt (PDF, Bilder, Office-Dokumente, Text, ZIP).");
        }
        if (sizeBytes > storage.MaxBytes)
        {
            throw new InvalidOperationException($"Die Datei ist zu groß (max. {storage.MaxBytes / (1024 * 1024)} MB).");
        }

        var fileName = await storage.SaveAsync(content, originalName, cancellationToken);
        var file = new LibraryFile
        {
            Title = title,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            OriginalName = originalName,
            FileNameSaved = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Classification = classification,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.LibraryFiles.Add(file);
        await db.SaveChangesAsync(cancellationToken);
        return file;
    }

    public async Task RefreshAsync(string id, string title, string? category, DocumentClassification classification,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);

        title = title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Bitte einen Titel angeben.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.LibraryFiles.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Datei nicht gefunden.");

        // may edit only if allowed to see the current level
        if (!DocumentViewerScope.From(actor).CanSee(file.Classification))
        {
            throw new UnauthorizedAccessException("Diese Datei ist eine Verschlusssache und dir nicht zugänglich.");
        }
        if (file.Classification != classification)
        {
            Permission.RequireMayAssignClassification(actor, classification);
        }

        file.Title = title;
        file.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        file.Classification = classification;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.LibraryFiles.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Datei nicht gefunden.");

        // soft-delete; physical file kept so DB restore stays possible
        db.LibraryFiles.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LibraryFile?> GetForDownloadAsync(string id, DocumentViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.LibraryFiles.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (file is null || !scope.CanSee(file.Classification))
        {
            // no existence leak: missing or not visible -> null
            return null;
        }
        return file;
    }
}
