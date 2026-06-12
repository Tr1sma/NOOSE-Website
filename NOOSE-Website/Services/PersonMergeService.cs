using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonMergeService" />
public class PersonMergeService(IDbContextFactory<AppDbContext> dbFactory) : IPersonMergeService
{
    public async Task ZusammenfuehrenAsync(string quelleId, string zielId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);
        // ExecuteUpdate umgeht den SaveChanges-Interceptor → die Nur-Lese-Aufsicht hier explizit sperren.
        Berechtigung.VerlangeSchreibrecht(handelnder);

        if (string.IsNullOrWhiteSpace(quelleId) || string.IsNullOrWhiteSpace(zielId) || quelleId == zielId)
        {
            throw new InvalidOperationException("Bitte zwei unterschiedliche Akten wählen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var quelle = await db.Personen
            .Include(p => p.Aliase).Include(p => p.Telefonnummern).Include(p => p.Fahrzeuge)
            .Include(p => p.Orte).Include(p => p.Waffen)
            .FirstOrDefaultAsync(p => p.Id == quelleId, cancellationToken)
            ?? throw new InvalidOperationException("Die Quell-Akte wurde nicht gefunden.");
        var ziel = await db.Personen
            .Include(p => p.Aliase).Include(p => p.Telefonnummern).Include(p => p.Fahrzeuge)
            .Include(p => p.Orte).Include(p => p.Waffen)
            .FirstOrDefaultAsync(p => p.Id == zielId, cancellationToken)
            ?? throw new InvalidOperationException("Die Ziel-Akte wurde nicht gefunden.");

        // ---- Kind-Daten ohne Dubletten-Risiko: komplett umhängen (Bulk, umgeht das Change-Tracking
        //      der geladenen Person-Children nicht – Doks/Fotos/Observationen sind nicht geladen). ----
        await db.PersonDoks.Where(d => d.PersonId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.PersonId, zielId), cancellationToken);
        await db.Observationen.Where(o => o.PersonId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PersonId, zielId), cancellationToken);
        await db.PersonFotos.Where(f => f.PersonId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.PersonId, zielId), cancellationToken);

        // ---- Steckbrief-Kinder mit Dubletten-Abgleich (case-insensitiv über Trim/Lower). ----
        static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        var zielAliase = ziel.Aliase.Select(a => Norm(a.Aliasname)).ToHashSet();
        zielAliase.Add(Norm(ziel.Name));
        foreach (var alias in quelle.Aliase)
        {
            if (zielAliase.Add(Norm(alias.Aliasname)))
            {
                alias.PersonId = zielId;
            }
            else
            {
                db.PersonAliase.Remove(alias);
            }
        }
        // Der Name der Quell-Akte bleibt als Alias auffindbar.
        if (zielAliase.Add(Norm(quelle.Name)))
        {
            db.PersonAliase.Add(new PersonAlias { PersonId = zielId, Aliasname = quelle.Name });
        }

        var zielTelefone = ziel.Telefonnummern.Select(t => Norm(t.Nummer)).ToHashSet();
        foreach (var telefon in quelle.Telefonnummern)
        {
            if (zielTelefone.Add(Norm(telefon.Nummer)))
            {
                telefon.PersonId = zielId;
            }
            else
            {
                db.PersonTelefone.Remove(telefon);
            }
        }

        var zielFahrzeuge = ziel.Fahrzeuge.Select(f => Norm(f.Bezeichnung) + "|" + Norm(f.Kennzeichen)).ToHashSet();
        foreach (var fahrzeug in quelle.Fahrzeuge)
        {
            if (zielFahrzeuge.Add(Norm(fahrzeug.Bezeichnung) + "|" + Norm(fahrzeug.Kennzeichen)))
            {
                fahrzeug.PersonId = zielId;
            }
            else
            {
                db.PersonFahrzeuge.Remove(fahrzeug);
            }
        }

        var zielOrte = ziel.Orte.Select(o => Norm(o.Text)).ToHashSet();
        foreach (var ort in quelle.Orte)
        {
            if (zielOrte.Add(Norm(ort.Text)))
            {
                ort.PersonId = zielId;
            }
            else
            {
                db.PersonOrte.Remove(ort);
            }
        }

        var zielWaffen = ziel.Waffen.Select(w => Norm(w.Text)).ToHashSet();
        foreach (var waffe in quelle.Waffen)
        {
            if (zielWaffen.Add(Norm(waffe.Text)))
            {
                waffe.PersonId = zielId;
            }
            else
            {
                db.PersonWaffen.Remove(waffe);
            }
        }

        // ---- Person-zu-Person-Beziehungen: umhängen; Selbstbezüge entfernen. ----
        var beziehungen = await db.PersonBeziehungen
            .Where(b => b.PersonAId == quelleId || b.PersonBId == quelleId)
            .ToListAsync(cancellationToken);
        foreach (var beziehung in beziehungen)
        {
            if (beziehung.PersonAId == quelleId)
            {
                beziehung.PersonAId = zielId;
            }
            if (beziehung.PersonBId == quelleId)
            {
                beziehung.PersonBId = zielId;
            }
            if (beziehung.PersonAId == beziehung.PersonBId)
            {
                db.PersonBeziehungen.Remove(beziehung);
            }
        }

        // ---- Mitgliedschaften (aktive): umhängen, außer das Ziel ist in derselben Akte bereits Mitglied. ----
        var zielFraktionen = await db.FraktionMitglieder.Where(m => m.PersonId == zielId)
            .Select(m => m.FraktionId).ToListAsync(cancellationToken);
        foreach (var mitglied in await db.FraktionMitglieder.Where(m => m.PersonId == quelleId).ToListAsync(cancellationToken))
        {
            if (zielFraktionen.Contains(mitglied.FraktionId))
            {
                db.FraktionMitglieder.Remove(mitglied);
            }
            else
            {
                mitglied.PersonId = zielId;
            }
        }

        var zielGruppen = await db.PersonengruppeMitglieder.Where(m => m.PersonId == zielId)
            .Select(m => m.PersonengruppeId).ToListAsync(cancellationToken);
        foreach (var mitglied in await db.PersonengruppeMitglieder.Where(m => m.PersonId == quelleId).ToListAsync(cancellationToken))
        {
            if (zielGruppen.Contains(mitglied.PersonengruppeId))
            {
                db.PersonengruppeMitglieder.Remove(mitglied);
            }
            else
            {
                mitglied.PersonId = zielId;
            }
        }

        var zielParteien = await db.ParteiMitglieder.Where(m => m.PersonId == zielId)
            .Select(m => m.ParteiId).ToListAsync(cancellationToken);
        foreach (var mitglied in await db.ParteiMitglieder.Where(m => m.PersonId == quelleId).ToListAsync(cancellationToken))
        {
            if (zielParteien.Contains(mitglied.ParteiId))
            {
                db.ParteiMitglieder.Remove(mitglied);
            }
            else
            {
                mitglied.PersonId = zielId;
            }
        }

        // ---- Polymorphe Bezüge (EntitaetTyp/-Id == Person/quelleId) umhängen. ----
        const string typ = nameof(Person);

        // Einstufungs-Verlauf (append-only) – die Historie beider Akten wird zusammengeführt.
        await db.EinstufungVerlauf.Where(e => e.EntitaetTyp == typ && e.EntitaetId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.EntitaetId, zielId), cancellationToken);

        // Kommentare, Quellen, Wiedervorlagen: konfliktfrei umhängen.
        await db.Kommentare.Where(k => k.EntitaetTyp == typ && k.EntitaetId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.EntitaetId, zielId), cancellationToken);
        await db.Quellen.Where(q => q.EntitaetTyp == typ && q.EntitaetId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.EntitaetId, zielId), cancellationToken);
        await db.Quellen.Where(q => q.ZielTyp == typ && q.ZielId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.ZielId, zielId), cancellationToken);
        await db.Wiedervorlagen.Where(w => w.EntitaetTyp == typ && w.EntitaetId == quelleId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.EntitaetId, zielId), cancellationToken);

        // Tags: Unique-Index (TagId, Typ, Id) → nur umhängen, was das Ziel noch nicht trägt.
        var zielTagIds = await db.TagZuordnungen.Where(z => z.EntitaetTyp == typ && z.EntitaetId == zielId)
            .Select(z => z.TagId).ToListAsync(cancellationToken);
        foreach (var zuordnung in await db.TagZuordnungen.Where(z => z.EntitaetTyp == typ && z.EntitaetId == quelleId).ToListAsync(cancellationToken))
        {
            if (zielTagIds.Contains(zuordnung.TagId))
            {
                db.TagZuordnungen.Remove(zuordnung);
            }
            else
            {
                zuordnung.EntitaetId = zielId;
            }
        }

        // Custom-Felder: Unique-Index je Definition → vorhandene Ziel-Werte haben Vorrang.
        var zielFeldIds = await db.CustomFeldWerte.Where(w => w.EntitaetTyp == typ && w.EntitaetId == zielId)
            .Select(w => w.CustomFeldDefinitionId).ToListAsync(cancellationToken);
        foreach (var wert in await db.CustomFeldWerte.Where(w => w.EntitaetTyp == typ && w.EntitaetId == quelleId).ToListAsync(cancellationToken))
        {
            if (zielFeldIds.Contains(wert.CustomFeldDefinitionId))
            {
                db.CustomFeldWerte.Remove(wert);
            }
            else
            {
                wert.EntitaetId = zielId;
            }
        }

        // Watchlist: je Agent nur ein aktiver Eintrag pro Akte.
        var zielFolger = await db.Watchlisten.Where(w => w.EntitaetTyp == typ && w.EntitaetId == zielId)
            .Select(w => w.AgentId).ToListAsync(cancellationToken);
        foreach (var eintrag in await db.Watchlisten.Where(w => w.EntitaetTyp == typ && w.EntitaetId == quelleId).ToListAsync(cancellationToken))
        {
            if (zielFolger.Contains(eintrag.AgentId))
            {
                db.Watchlisten.Remove(eintrag);
            }
            else
            {
                eintrag.EntitaetId = zielId;
            }
        }

        // Verknüpfungen: beide Seiten umhängen; entstehende Selbst-Verknüpfungen entfernen.
        foreach (var verknuepfung in await db.Verknuepfungen
                     .Where(v => (v.VonTyp == typ && v.VonId == quelleId) || (v.NachTyp == typ && v.NachId == quelleId))
                     .ToListAsync(cancellationToken))
        {
            if (verknuepfung.VonTyp == typ && verknuepfung.VonId == quelleId)
            {
                verknuepfung.VonId = zielId;
            }
            if (verknuepfung.NachTyp == typ && verknuepfung.NachId == quelleId)
            {
                verknuepfung.NachId = zielId;
            }
            if (verknuepfung.VonTyp == verknuepfung.NachTyp && verknuepfung.VonId == verknuepfung.NachId)
            {
                db.Verknuepfungen.Remove(verknuepfung);
            }
        }

        // Offene Anträge (z. B. Hochstufung): auf die Ziel-Akte umbiegen, Bezeichnung aktualisieren.
        foreach (var antrag in await db.Antraege
                     .Where(a => a.ZielTyp == typ && a.ZielId == quelleId)
                     .ToListAsync(cancellationToken))
        {
            antrag.ZielId = zielId;
            antrag.ZielBezeichnung = $"{ziel.Name} ({ziel.Aktenzeichen})";
        }

        // ---- Steckbrief: fehlende Angaben der Ziel-Akte aus der Quelle übernehmen. ----
        if (string.IsNullOrWhiteSpace(ziel.Beschreibung) && !string.IsNullOrWhiteSpace(quelle.Beschreibung))
        {
            ziel.Beschreibung = quelle.Beschreibung;
        }
        // Verschlusssache „färbt ab": die zusammengeführte Akte enthält auch die VS-Inhalte der Quelle.
        ziel.IstVerschlusssache = ziel.IstVerschlusssache || quelle.IstVerschlusssache;
        // Einstufung/Lebensstatus der Ziel-Akte bleiben bewusst unangetastet (Rang-Gate der Einstufung).

        // Nachvollziehbarkeit direkt an der Ziel-Akte (zusätzlich zum Audit-Log).
        db.Kommentare.Add(new Kommentar
        {
            EntitaetTyp = typ,
            EntitaetId = zielId,
            Text = $"Akte „{quelle.Name}“ ({quelle.Aktenzeichen}) wurde in diese Akte überführt (Duplikat-Zusammenführung).",
            AutorName = handelnder.GetCodename(),
        });

        // ---- Quell-Akte in den Papierkorb (Interceptor wandelt Remove in Soft-Delete um). ----
        db.Personen.Remove(quelle);

        await db.SaveChangesAsync(cancellationToken);
    }
}
