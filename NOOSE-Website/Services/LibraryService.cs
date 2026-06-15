using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBibliothekService" />
public class LibraryService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILibraryStorageService storage) : ILibraryService
{
    public async Task<List<LibraryFile>> GetListAsync(bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.LibraryFiles
            .Where(d => isLeadership || !d.IsClassified)
            .OrderByDescending(d => d.ModifiedAt ?? d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LibraryFile> UploadAsync(string title, string? category, bool isClassified,
        Stream content, string originalName, string contentType, long sizeBytes,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        if (isClassified)
        {
            Permission.RequireLeadership(actor);
        }

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
            IsClassified = isClassified,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.LibraryFiles.Add(file);
        await db.SaveChangesAsync(cancellationToken);
        return file;
    }

    public async Task RefreshAsync(string id, string title, string? category, bool isClassified,
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

        if (file.IsClassified != isClassified)
        {
            Permission.RequireLeadership(actor);
        }

        file.Title = title;
        file.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        file.IsClassified = isClassified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.LibraryFiles.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Datei nicht gefunden.");

        // Soft-Delete (Interceptor wandelt Remove um); die physische Datei bleibt erhalten,
        // damit eine Wiederherstellung über die DB möglich bleibt.
        db.LibraryFiles.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LibraryFile?> GetForDownloadAsync(string id, bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.LibraryFiles.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (file is null || (file.IsClassified && !isLeadership))
        {
            // Kein Existenz-Leak: nicht vorhanden oder nicht sichtbar → null.
            return null;
        }
        return file;
    }
}
