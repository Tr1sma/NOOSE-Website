using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Zeitstrahl;

namespace NOOSE_Website.Services;

/// <summary>
/// Baut den vereinheitlichten Akten-Zeitstrahl aus mehreren Quellen zusammen. Basis sind die strukturellen
/// Audit-Einträge (Akte + Doks/Mitglieder/Zuteilungen – exakt die Whitelist der bisherigen <c>GetHistorieAsync</c>),
/// ergänzt um die semantischen Quellen, die das reine Audit nicht schön abbildet (Einstufungs-Verlauf, Kommentare,
/// Quellen, Wiedervorlagen, Verknüpfungen, sowie Person-/Fraktion-spezifische Ereignisse). Verknüpfungen werden –
/// anders als im alten Audit – einheitlich semantisch mit aufgelöstem Gegenseiten-Namen dargestellt und sind daher
/// aus dem Audit-Teil ausgenommen (keine Doppelungen). Alle Abfragen laufen sequenziell auf einem Context
/// (DbContext ist nicht thread-safe); flache <c>WHERE</c>-Filter, kein SelectMany/CROSS APPLY (Pomelo/MySQL).
/// </summary>
public class ZeitstrahlService(IDbContextFactory<AppDbContext> dbFactory) : IZeitstrahlService
{
    // Zwischenform: wie ZeitstrahlEintrag, aber mit optionaler Akteur-Id, deren Codename am Ende in einem
    // Rutsch aufgelöst wird (Quellen ohne denormalisierten Namen tragen nur die ErstelltVon-/GeloeschtVon-Id).
    private sealed record Roh(
        DateTime Zeitpunkt, ZeitstrahlKategorie Kategorie, string Titel, string? Detail,
        string? AkteurName, string? AkteurId, string? Href,
        IReadOnlyList<AuditAnzeige.Feldaenderung>? Aenderungen);

    public async Task<IReadOnlyList<ZeitstrahlEintrag>> GetZeitstrahlAsync(
        string entitaetTyp, string entitaetId, ClaimsPrincipal betrachter,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var darfVs = betrachter.DarfVerschlusssacheLesen();
        var darfAlleTf = betrachter.DarfAlleTaskforcesSehen();
        var meId = betrachter.GetAgentId();

        // Gate = Detailseiten-Sichtbarkeit (Verschlusssache/Papierkorb/Taskforce-Zuteilung).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp, entitaetId, darfVs, cancellationToken, meId))
        {
            return Array.Empty<ZeitstrahlEintrag>();
        }
        // Aufgabe zusätzlich auf „eingeschränkt" prüfen – Sichtbarkeit stuft Aufgaben sonst immer als sichtbar ein.
        if (entitaetTyp == nameof(Aufgabe))
        {
            var sichtbar = await AufgabeSichtbarkeit.SichtbareIdsAsync(db, new[] { entitaetId }, darfAlleTf, meId, cancellationToken);
            if (!sichtbar.Contains(entitaetId))
            {
                return Array.Empty<ZeitstrahlEintrag>();
            }
        }

        var roh = new List<Roh>();
        var akteurIds = new HashSet<string>();

        // ---- 1) Audit-Basis: Akte + Doks/Mitglieder/Zuteilungen (Verknüpfungen bewusst NICHT – siehe unten) ----
        var (typen, auditIds) = await AuditQuelleAsync(db, entitaetTyp, entitaetId, cancellationToken);
        foreach (var log in await db.AuditLogs
            .Where(a => typen.Contains(a.EntitaetTyp) && auditIds.Contains(a.EntitaetId)
                && a.EntitaetTyp != nameof(Verknuepfung))
            .Select(a => new { a.Zeitpunkt, a.EntitaetTyp, a.Aktion, a.AenderungenJson, a.AgentName })
            .ToListAsync(cancellationToken))
        {
            var (kat, titel) = MapAudit(log.EntitaetTyp, log.Aktion);
            roh.Add(new Roh(log.Zeitpunkt, kat, titel, null, log.AgentName, null, null,
                AuditAnzeige.Parse(log.AenderungenJson)));
        }

        // ---- 2) Einstufungs-Verlauf (Person/Fraktion/Gruppe/Partei/Operation/Vorgang; sonst leer) ----
        foreach (var e in await db.EinstufungVerlauf
            .Where(e => e.EntitaetTyp == entitaetTyp && e.EntitaetId == entitaetId)
            .Select(e => new { e.Wert, e.Begruendung, e.Zeitpunkt, e.AgentName })
            .ToListAsync(cancellationToken))
        {
            roh.Add(new Roh(e.Zeitpunkt, ZeitstrahlKategorie.Einstufung,
                $"Einstufung: {EinstufungAnzeige.Name(e.Wert)}", e.Begruendung, e.AgentName, null, null, null));
        }

        // ---- 3) Kommentare ----
        foreach (var k in await db.Kommentare
            .Where(k => k.EntitaetTyp == entitaetTyp && k.EntitaetId == entitaetId)
            .Select(k => new { k.Text, k.AutorName, k.ErstelltAm })
            .ToListAsync(cancellationToken))
        {
            roh.Add(new Roh(k.ErstelltAm, ZeitstrahlKategorie.Kommentar,
                "Kommentar", Kuerzen(k.Text), k.AutorName, null, null, null));
        }

        // ---- 4) Quellen/Anhänge ----
        foreach (var q in await db.Quellen
            .Where(q => q.EntitaetTyp == entitaetTyp && q.EntitaetId == entitaetId)
            .Select(q => new { q.Titel, q.Typ, q.ErstelltAm, q.ErstelltVonId })
            .ToListAsync(cancellationToken))
        {
            Merke(akteurIds, q.ErstelltVonId);
            var titel = string.IsNullOrWhiteSpace(q.Titel) ? "Quelle hinzugefügt" : $"Quelle hinzugefügt: {q.Titel}";
            roh.Add(new Roh(q.ErstelltAm, ZeitstrahlKategorie.Quelle, titel,
                QuelleTypAnzeige.Name(q.Typ), null, q.ErstelltVonId, null, null));
        }

        // ---- 5) Wiedervorlagen (Anlage + Erledigung) ----
        foreach (var w in await db.Wiedervorlagen
            .Where(w => w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId)
            .Select(w => new { w.FaelligAm, w.Notiz, w.Erledigt, w.ErledigtAm, w.ErledigtVonId, w.ErstelltAm, w.ErstelltVonId })
            .ToListAsync(cancellationToken))
        {
            Merke(akteurIds, w.ErstelltVonId);
            var faellig = w.FaelligAm.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            var detail = string.IsNullOrWhiteSpace(w.Notiz) ? $"fällig am {faellig}" : $"fällig am {faellig} · {w.Notiz}";
            roh.Add(new Roh(w.ErstelltAm, ZeitstrahlKategorie.Wiedervorlage, "Wiedervorlage angelegt",
                detail, null, w.ErstelltVonId, null, null));
            if (w.Erledigt && w.ErledigtAm is { } erledigt)
            {
                Merke(akteurIds, w.ErledigtVonId);
                roh.Add(new Roh(erledigt, ZeitstrahlKategorie.Wiedervorlage, "Wiedervorlage erledigt",
                    w.Notiz, null, w.ErledigtVonId, null, null));
            }
        }

        // ---- 6) Verknüpfungen (beidseitig, inkl. entfernter; Gegenseite sichtbarkeitsgeprüft auflösen) ----
        var verkn = await db.Verknuepfungen.IgnoreQueryFilters()
            .Where(v => !v.Automatisch
                && ((v.VonTyp == entitaetTyp && v.VonId == entitaetId)
                 || (v.NachTyp == entitaetTyp && v.NachId == entitaetId)))
            .Select(v => new { v.VonTyp, v.VonId, v.NachTyp, v.NachId, v.Label, v.ErstelltAm, v.ErstelltVonId, v.IstGeloescht, v.GeloeschtAm, v.GeloeschtVonId })
            .ToListAsync(cancellationToken);
        if (verkn.Count > 0)
        {
            (string, string) Gegenseite(string vonTyp, string vonId, string nachTyp, string nachId)
                => vonTyp == entitaetTyp && vonId == entitaetId ? (nachTyp, nachId) : (vonTyp, vonId);

            var refs = verkn.Select(v => Gegenseite(v.VonTyp, v.VonId, v.NachTyp, v.NachId)).Distinct().ToList();
            var map = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken, darfAlleTf, meId);
            foreach (var v in verkn)
            {
                var (name, href) = GegenseiteAnzeige(map, Gegenseite(v.VonTyp, v.VonId, v.NachTyp, v.NachId), "Akte");
                Merke(akteurIds, v.ErstelltVonId);
                roh.Add(new Roh(v.ErstelltAm, ZeitstrahlKategorie.Verknuepfung, $"Verknüpft mit {name}",
                    v.Label, null, v.ErstelltVonId, href, null));
                if (v.IstGeloescht && v.GeloeschtAm is { } entfernt)
                {
                    Merke(akteurIds, v.GeloeschtVonId);
                    roh.Add(new Roh(entfernt, ZeitstrahlKategorie.Verknuepfung, $"Verknüpfung mit {name} entfernt",
                        v.Label, null, v.GeloeschtVonId, href, null));
                }
            }
        }

        // ---- 7) Person-spezifisch: Observationen, Fotos, Beziehungen ----
        if (entitaetTyp == nameof(Person))
        {
            foreach (var o in await db.Observationen
                .Where(o => o.PersonId == entitaetId)
                .Select(o => new { o.Beginn, o.Ort, o.Beobachtung, o.ErstelltVonId })
                .ToListAsync(cancellationToken))
            {
                Merke(akteurIds, o.ErstelltVonId);
                var titel = string.IsNullOrWhiteSpace(o.Ort) ? "Observation" : $"Observation – {o.Ort}";
                roh.Add(new Roh(o.Beginn, ZeitstrahlKategorie.Observation, titel,
                    Kuerzen(o.Beobachtung), null, o.ErstelltVonId, null, null));
            }

            foreach (var f in await db.PersonFotos
                .Where(f => f.PersonId == entitaetId)
                .Select(f => new { f.OriginalName, f.ErstelltAm, f.ErstelltVonId })
                .ToListAsync(cancellationToken))
            {
                Merke(akteurIds, f.ErstelltVonId);
                roh.Add(new Roh(f.ErstelltAm, ZeitstrahlKategorie.Foto, "Foto hinzugefügt",
                    f.OriginalName, null, f.ErstelltVonId, null, null));
            }

            var bez = await db.PersonBeziehungen
                .Where(b => b.PersonAId == entitaetId || b.PersonBId == entitaetId)
                .Select(b => new { b.PersonAId, b.PersonBId, b.Typ, b.Notiz, b.ErstelltAm, b.ErstelltVonId })
                .ToListAsync(cancellationToken);
            if (bez.Count > 0)
            {
                var refs = bez.Select(b => (nameof(Person), b.PersonAId == entitaetId ? b.PersonBId : b.PersonAId))
                    .Distinct().ToList();
                var map = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken, darfAlleTf, meId);
                foreach (var b in bez)
                {
                    var andereId = b.PersonAId == entitaetId ? b.PersonBId : b.PersonAId;
                    var (name, href) = GegenseiteAnzeige(map, (nameof(Person), andereId), "Person");
                    Merke(akteurIds, b.ErstelltVonId);
                    roh.Add(new Roh(b.ErstelltAm, ZeitstrahlKategorie.Beziehung,
                        $"Beziehung ({BeziehungsTypAnzeige.Name(b.Typ)}): {name}", b.Notiz, null, b.ErstelltVonId, href, null));
                }
            }
        }

        // ---- 8) Fraktion-spezifisch: Aktivitäten-Zeitstrahl ----
        if (entitaetTyp == nameof(Fraktion))
        {
            foreach (var a in await db.FraktionAktivitaeten
                .Where(a => a.FraktionId == entitaetId)
                .Select(a => new { a.Titel, a.Art, a.Zeitpunkt, a.Beschreibung, a.Ort, a.ErstelltVonId })
                .ToListAsync(cancellationToken))
            {
                Merke(akteurIds, a.ErstelltVonId);
                var titel = string.IsNullOrWhiteSpace(a.Art) ? $"Aktivität: {a.Titel}" : $"Aktivität ({a.Art}): {a.Titel}";
                var detail = a.Beschreibung;
                if (!string.IsNullOrWhiteSpace(a.Ort))
                {
                    detail = string.IsNullOrWhiteSpace(detail) ? $"Ort: {a.Ort}" : $"{detail} · Ort: {a.Ort}";
                }
                roh.Add(new Roh(a.Zeitpunkt, ZeitstrahlKategorie.Aktivitaet, titel, detail, null, a.ErstelltVonId, null, null));
            }
        }

        // ---- Akteur-Codenamen für die per-Id markierten Ereignisse auflösen ----
        var namen = akteurIds.Count == 0
            ? new Dictionary<string, string?>()
            : await db.Users.Where(u => akteurIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename })
                .ToDictionaryAsync(u => u.Id, u => (string?)u.Codename, cancellationToken);

        return roh
            .Select(r => new ZeitstrahlEintrag(r.Zeitpunkt, r.Kategorie, r.Titel, r.Detail,
                AkteurName(r, namen), r.Href, r.Aenderungen))
            .OrderByDescending(e => e.Zeitpunkt)
            .ToList();
    }

    // Ermittelt die Audit-Quelle (Entitätstypen-Whitelist + zugehörige Ids) je Akte – exakt die Bündel der
    // bisherigen GetHistorieAsync, jedoch OHNE Verknüpfung (die wird einheitlich semantisch dargestellt).
    // IgnoreQueryFilters: auch bereits entfernte Sub-Einträge (z. B. ausgetretene Mitglieder) liefern ihre
    // Beitritts-/Austritts-Audit-Einträge in den Zeitstrahl.
    private static async Task<(string[] Typen, HashSet<string> Ids)> AuditQuelleAsync(
        AppDbContext db, string typ, string id, CancellationToken ct)
    {
        var ids = new HashSet<string> { id };
        switch (typ)
        {
            case nameof(Person):
                ids.UnionWith(await db.PersonDoks.IgnoreQueryFilters().Where(d => d.PersonId == id).Select(d => d.Id).ToListAsync(ct));
                return ([nameof(Person), nameof(PersonDok)], ids);
            case nameof(Fraktion):
                ids.UnionWith(await db.FraktionMitglieder.IgnoreQueryFilters().Where(m => m.FraktionId == id).Select(m => m.Id).ToListAsync(ct));
                ids.UnionWith(await db.FraktionAgenten.IgnoreQueryFilters().Where(a => a.FraktionId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Fraktion), nameof(FraktionMitglied), nameof(FraktionAgent)], ids);
            case nameof(Personengruppe):
                ids.UnionWith(await db.PersonengruppeMitglieder.IgnoreQueryFilters().Where(m => m.PersonengruppeId == id).Select(m => m.Id).ToListAsync(ct));
                ids.UnionWith(await db.PersonengruppeAgenten.IgnoreQueryFilters().Where(a => a.PersonengruppeId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Personengruppe), nameof(PersonengruppeMitglied), nameof(PersonengruppeAgent)], ids);
            case nameof(Partei):
                ids.UnionWith(await db.ParteiMitglieder.IgnoreQueryFilters().Where(m => m.ParteiId == id).Select(m => m.Id).ToListAsync(ct));
                ids.UnionWith(await db.ParteiAgenten.IgnoreQueryFilters().Where(a => a.ParteiId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Partei), nameof(ParteiMitglied), nameof(ParteiAgent)], ids);
            case nameof(Operation):
                ids.UnionWith(await db.OperationAgenten.IgnoreQueryFilters().Where(a => a.OperationId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Operation), nameof(OperationAgent)], ids);
            case nameof(Vorgang):
                ids.UnionWith(await db.VorgangAgenten.IgnoreQueryFilters().Where(a => a.VorgangId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Vorgang), nameof(VorgangAgent)], ids);
            case nameof(Taskforce):
                ids.UnionWith(await db.TaskforceAgenten.IgnoreQueryFilters().Where(a => a.TaskforceId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Taskforce), nameof(TaskforceAgent)], ids);
            case nameof(Aufgabe):
                ids.UnionWith(await db.AufgabeZuweisungen.IgnoreQueryFilters().Where(z => z.AufgabeId == id).Select(z => z.Id).ToListAsync(ct));
                return ([nameof(Aufgabe), nameof(AufgabeZuweisung)], ids);
            default:
                return ([typ], ids);
        }
    }

    // Bildet einen Audit-Eintrag auf Kategorie + lesbaren Titel ab. Sub-Entitäten (Dok/Mitglied/Zuteilung)
    // erhalten eigene Kategorien; die Hauptakte Anlage/Änderung/Löschung/Wiederherstellung.
    private static (ZeitstrahlKategorie Kat, string Titel) MapAudit(string entitaetTyp, AuditAktion aktion)
    {
        string Verb(string erstellt, string geloescht) => aktion switch
        {
            AuditAktion.Erstellt => erstellt,
            AuditAktion.Geaendert => "geändert",
            AuditAktion.Geloescht => geloescht,
            AuditAktion.Wiederhergestellt => "wiederhergestellt",
            _ => aktion.ToString(),
        };

        if (entitaetTyp == nameof(PersonDok))
        {
            return (ZeitstrahlKategorie.Dok, $"Dok {Verb("angelegt", "gelöscht")}");
        }
        if (entitaetTyp is nameof(FraktionMitglied) or nameof(PersonengruppeMitglied) or nameof(ParteiMitglied))
        {
            return (ZeitstrahlKategorie.Mitgliedschaft, $"Mitglied {Verb("aufgenommen", "entfernt")}");
        }
        if (entitaetTyp is nameof(FraktionAgent) or nameof(PersonengruppeAgent) or nameof(ParteiAgent)
            or nameof(OperationAgent) or nameof(VorgangAgent) or nameof(TaskforceAgent) or nameof(AufgabeZuweisung))
        {
            return (ZeitstrahlKategorie.Zuteilung, $"Agent {Verb("zugeteilt", "entfernt")}");
        }

        var kat = aktion switch
        {
            AuditAktion.Erstellt => ZeitstrahlKategorie.Anlage,
            AuditAktion.Geloescht => ZeitstrahlKategorie.Loeschung,
            AuditAktion.Wiederhergestellt => ZeitstrahlKategorie.Wiederherstellung,
            _ => ZeitstrahlKategorie.Aenderung,
        };
        return (kat, $"Akte {Verb("angelegt", "gelöscht")}");
    }

    // Liefert Anzeigename + Href der Verknüpfungs-/Beziehungs-Gegenseite. Nicht aufgelöst → „nicht verfügbar";
    // Verschlusssache (für Nicht-Führung) → „verdeckt", jeweils ohne Link.
    private static (string Name, string? Href) GegenseiteAnzeige(
        Dictionary<(string, string), AktenReferenz.Aufloesung> map, (string, string) ziel, string verdecktNomen)
    {
        if (!map.TryGetValue(ziel, out var auf))
        {
            return ($"nicht verfügbare {verdecktNomen}", null);
        }
        return auf.Verschluss ? ($"verdeckte {verdecktNomen}", null) : (auf.Anzeige, auf.Href);
    }

    private static void Merke(HashSet<string> ids, string? id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            ids.Add(id);
        }
    }

    private static string? AkteurName(Roh r, Dictionary<string, string?> namen)
    {
        if (!string.IsNullOrWhiteSpace(r.AkteurName))
        {
            return r.AkteurName;
        }
        if (r.AkteurId is { } id && namen.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        return null;
    }

    private static string? Kuerzen(string? text, int max = 160)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        text = text.Trim();
        return text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
    }
}
