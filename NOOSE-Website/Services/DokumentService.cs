using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDokumentService" />
public class DokumentService(IDbContextFactory<AppDbContext> dbFactory) : IDokumentService
{
    public async Task<List<DokumentListeItem>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Dokumente
            .Where(d => istFuehrung || !d.IstVerschlusssache)
            .OrderByDescending(d => d.GeaendertAm ?? d.ErstelltAm)
            .Select(d => new DokumentListeItem(d.Id, d.Titel, d.Kategorie, d.IstVerschlusssache, d.GeaendertAm ?? d.ErstelltAm))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DokumentListeItem>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Dokumente.Where(d => istFuehrung || !d.IstVerschlusssache);

        var s = suchtext?.Trim();
        if (!string.IsNullOrWhiteSpace(s))
        {
            query = query.Where(d => d.Titel.Contains(s) || (d.Kategorie != null && d.Kategorie.Contains(s)));
        }

        return await query
            .OrderByDescending(d => d.GeaendertAm ?? d.ErstelltAm)
            .Take(max)
            .Select(d => new DokumentListeItem(d.Id, d.Titel, d.Kategorie, d.IstVerschlusssache, d.GeaendertAm ?? d.ErstelltAm))
            .ToListAsync(cancellationToken);
    }

    public async Task<Dokument?> GetAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dokument = await db.Dokumente.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dokument is null || (dokument.IstVerschlusssache && !istFuehrung))
        {
            // Kein Existenz-Leak: nicht vorhanden oder nicht sichtbar → null.
            return null;
        }
        return dokument;
    }

    public async Task<Dokument> ErstellenAsync(DokumentEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var titel = (eingabe.Titel ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        var dokument = new Dokument
        {
            Titel = titel,
            Kategorie = eingabe.Kategorie.TrimToNull(),
            // Maßgebliche Sicherheitskontrolle: HTML serverseitig bereinigen.
            InhaltHtml = HtmlBereinigung.Bereinige(eingabe.InhaltHtml),
            IstVerschlusssache = eingabe.IstVerschlusssache,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Dokumente.Add(dokument);
        await db.SaveChangesAsync(cancellationToken);
        return dokument;
    }

    public async Task AktualisierenAsync(string id, DokumentEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var titel = (eingabe.Titel ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dokument = await db.Dokumente.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Dokument nicht gefunden.");

        // Verschlusssachen darf nur die Führung bearbeiten (für Nicht-Führung ist es ohnehin unsichtbar).
        if (dokument.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Dieses Dokument ist eine Verschlusssache und nur der Führung zugänglich.");
        }

        dokument.Titel = titel;
        dokument.Kategorie = eingabe.Kategorie.TrimToNull();
        dokument.InhaltHtml = HtmlBereinigung.Bereinige(eingabe.InhaltHtml);
        dokument.IstVerschlusssache = eingabe.IstVerschlusssache;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dokument = await db.Dokumente.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dokument is null)
        {
            return;
        }

        var istErsteller = dokument.ErstelltVonId is not null && dokument.ErstelltVonId == handelnder.GetAgentId();
        if (!istErsteller && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Nur der Ersteller oder die Führung darf dieses Dokument löschen.");
        }

        // Remove wird vom AuditSaveChangesInterceptor in einen Soft-Delete (Papierkorb) umgewandelt.
        db.Dokumente.Remove(dokument);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<DokumentAnhang>> GetAnhaengeAsync(string dokumentId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var quellen = await db.Quellen
            .Where(q => q.Typ == QuelleTyp.Dokument && q.ZielTyp == nameof(Dokument) && q.ZielId == dokumentId)
            .Select(q => new { q.EntitaetTyp, q.EntitaetId })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (quellen.Count == 0)
        {
            return new();
        }

        var refs = quellen.Select(q => (q.EntitaetTyp, q.EntitaetId)).ToList();
        var map = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken);

        var ergebnis = new List<DokumentAnhang>();
        foreach (var q in quellen)
        {
            if (map.TryGetValue((q.EntitaetTyp, q.EntitaetId), out var a))
            {
                // Verschlusssachen-Eltern-Akten für Nicht-Führung ausblenden (kein Namens-Leak).
                if (!istFuehrung && a.Verschluss)
                {
                    continue;
                }
                ergebnis.Add(new DokumentAnhang(q.EntitaetTyp, q.EntitaetId, a.Anzeige, a.Href));
            }
        }
        return ergebnis;
    }
}
