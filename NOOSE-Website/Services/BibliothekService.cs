using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Storage;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBibliothekService" />
public class BibliothekService(
    IDbContextFactory<AppDbContext> dbFactory,
    IBibliothekStorageService storage) : IBibliothekService
{
    public async Task<List<BibliothekDatei>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BibliothekDateien
            .Where(d => istFuehrung || !d.IstVerschlusssache)
            .OrderByDescending(d => d.GeaendertAm ?? d.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<BibliothekDatei> HochladenAsync(string titel, string? kategorie, bool istVerschlusssache,
        Stream inhalt, string originalName, string contentType, long groesseBytes,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeSchreibrecht(handelnder);
        if (istVerschlusssache)
        {
            Berechtigung.VerlangeFuehrung(handelnder);
        }

        titel = titel.Trim();
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new InvalidOperationException("Bitte einen Titel angeben.");
        }
        if (!storage.IstErlaubterTyp(contentType))
        {
            throw new InvalidOperationException("Dieser Dateityp ist nicht erlaubt (PDF, Bilder, Office-Dokumente, Text, ZIP).");
        }
        if (groesseBytes > storage.MaxBytes)
        {
            throw new InvalidOperationException($"Die Datei ist zu groß (max. {storage.MaxBytes / (1024 * 1024)} MB).");
        }

        var dateiname = await storage.SpeichernAsync(inhalt, originalName, cancellationToken);
        var datei = new BibliothekDatei
        {
            Titel = titel,
            Kategorie = string.IsNullOrWhiteSpace(kategorie) ? null : kategorie.Trim(),
            OriginalName = originalName,
            DateinameGespeichert = dateiname,
            ContentType = contentType,
            GroesseBytes = groesseBytes,
            IstVerschlusssache = istVerschlusssache,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.BibliothekDateien.Add(datei);
        await db.SaveChangesAsync(cancellationToken);
        return datei;
    }

    public async Task AktualisierenAsync(string id, string titel, string? kategorie, bool istVerschlusssache,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeSchreibrecht(handelnder);

        titel = titel.Trim();
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new InvalidOperationException("Bitte einen Titel angeben.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var datei = await db.BibliothekDateien.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Datei nicht gefunden.");

        if (datei.IstVerschlusssache != istVerschlusssache)
        {
            Berechtigung.VerlangeFuehrung(handelnder);
        }

        datei.Titel = titel;
        datei.Kategorie = string.IsNullOrWhiteSpace(kategorie) ? null : kategorie.Trim();
        datei.IstVerschlusssache = istVerschlusssache;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var datei = await db.BibliothekDateien.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Datei nicht gefunden.");

        // Soft-Delete (Interceptor wandelt Remove um); die physische Datei bleibt erhalten,
        // damit eine Wiederherstellung über die DB möglich bleibt.
        db.BibliothekDateien.Remove(datei);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BibliothekDatei?> GetFuerDownloadAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var datei = await db.BibliothekDateien.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (datei is null || (datei.IstVerschlusssache && !istFuehrung))
        {
            // Kein Existenz-Leak: nicht vorhanden oder nicht sichtbar → null.
            return null;
        }
        return datei;
    }
}
