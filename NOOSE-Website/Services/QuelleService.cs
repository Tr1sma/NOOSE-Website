using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IQuelleService" />
public class QuelleService(IDbContextFactory<AppDbContext> dbFactory, IQuellenStorageService storage) : IQuelleService
{
    public async Task<List<Quelle>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Verschlusssache-Schutz: bei Personen-Akten die Sichtbarkeit der Eltern-Akte prüfen
        // (keine FK-Navigation bei polymorphen Assoziationen → expliziter Lookup).
        if (!await AkteSichtbarAsync(db, entitaetTyp, entitaetId, istFuehrung, cancellationToken))
        {
            return new();
        }

        return await db.Quellen
            .Where(q => q.EntitaetTyp == entitaetTyp && q.EntitaetId == entitaetId)
            .OrderByDescending(q => q.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Quelle> ErstellenAsync(string entitaetTyp, string entitaetId, QuelleEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var titel = eingabe.Titel?.Trim();
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        var quelle = new Quelle
        {
            EntitaetTyp = entitaetTyp,
            EntitaetId = entitaetId,
            Typ = eingabe.Typ,
            Titel = titel,
            Beschreibung = Leer(eingabe.Beschreibung),
        };

        switch (eingabe.Typ)
        {
            case QuelleTyp.Link:
                if (string.IsNullOrWhiteSpace(eingabe.Url))
                {
                    throw new InvalidOperationException("Bei einer Link-Quelle ist eine URL erforderlich.");
                }
                quelle.Url = eingabe.Url.Trim();
                break;

            case QuelleTyp.Intern:
                if (string.IsNullOrWhiteSpace(eingabe.ZielTyp) || string.IsNullOrWhiteSpace(eingabe.ZielId))
                {
                    throw new InvalidOperationException("Bei einer internen Quelle ist eine Ziel-Akte erforderlich.");
                }
                quelle.ZielTyp = eingabe.ZielTyp;
                quelle.ZielId = eingabe.ZielId;
                break;

            case QuelleTyp.Upload:
                if (eingabe.DateiInhalt is null || eingabe.DateiInhalt.Length == 0)
                {
                    throw new InvalidOperationException("Es wurde keine Datei ausgewählt.");
                }
                if (!storage.IstErlaubterTyp(eingabe.ContentType ?? string.Empty))
                {
                    throw new InvalidOperationException($"Dateityp „{eingabe.ContentType}“ ist nicht erlaubt.");
                }
                await using (var ms = new MemoryStream(eingabe.DateiInhalt))
                {
                    quelle.DateinameGespeichert = await storage.SpeichernAsync(ms, eingabe.OriginalName ?? "datei", cancellationToken);
                }
                quelle.OriginalName = eingabe.OriginalName;
                quelle.ContentType = eingabe.ContentType;
                quelle.GroesseBytes = eingabe.GroesseBytes;
                break;

            case QuelleTyp.Freitext:
                // Inhalt steckt in der Beschreibung – nichts weiter nötig.
                break;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Quellen.Add(quelle);
        await db.SaveChangesAsync(cancellationToken);
        return quelle;
    }

    public async Task EntfernenAsync(string quelleId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var quelle = await db.Quellen.FirstOrDefaultAsync(q => q.Id == quelleId, cancellationToken);
        if (quelle is null)
        {
            return;
        }
        // Soft-Delete via Interceptor. Die physische Datei bleibt erhalten (Wiederherstellung möglich);
        // endgültiges Entfernen erst beim Hard-Delete (späterer Cleanup, siehe Plan).
        db.Quellen.Remove(quelle);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Quelle?> GetFuerDownloadAsync(string quelleId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var quelle = await db.Quellen.FirstOrDefaultAsync(q => q.Id == quelleId, cancellationToken);
        if (quelle is null || quelle.Typ != QuelleTyp.Upload || string.IsNullOrEmpty(quelle.DateinameGespeichert))
        {
            return null;
        }
        return await AkteSichtbarAsync(db, quelle.EntitaetTyp, quelle.EntitaetId, istFuehrung, cancellationToken)
            ? quelle
            : null;
    }

    /// <summary>
    /// Prüft, ob die Eltern-Akte für den Aufrufer sichtbar ist. In Phase 3 nur Personen-Akten relevant:
    /// im Papierkorb → für alle „nicht vorhanden"; Verschlusssache → nur Führung. Andere Typen (später)
    /// gelten vorerst als sichtbar.
    /// </summary>
    private static async Task<bool> AkteSichtbarAsync(AppDbContext db, string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken)
    {
        if (entitaetTyp != nameof(Person))
        {
            return true;
        }

        var person = await db.Personen
            .Where(p => p.Id == entitaetId)
            .Select(p => new { p.IstVerschlusssache })
            .FirstOrDefaultAsync(cancellationToken);

        // null = Akte gelöscht (Query-Filter) oder unbekannt → wie nicht vorhanden behandeln.
        return person is not null && (istFuehrung || !person.IstVerschlusssache);
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
