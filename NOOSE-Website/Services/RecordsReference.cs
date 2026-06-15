using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Auflösung von Objekt-Verweisen (Typ + Id) zu Anzeigename, Verschlusssache-Flag und Navigations-Href.
/// Spiegelt die Typ→(Bezeichnung, VS, Href)-Auflösung aus <see cref="VerknuepfungService"/>, ergänzt um
/// <see cref="Quelle"/> und <see cref="Agent"/>; dient den @-Mentions (siehe MentionService). Liefert das ECHTE
/// Verschlusssache-Flag – das Ausblenden für Nicht-Führung übernimmt der Aufrufer, damit sensible Namen
/// serverseitig bleiben (Blazor Server serialisiert verborgene Werte nie zum Client).
/// </summary>
public static class RecordsReference
{
    public readonly record struct Resolution(string Display, bool Classified, string? Href);

    /// <summary>Löst alle angegebenen (Typ, Id)-Verweise in einer Sammelabfrage je Typ auf.
    /// Taskforces werden NUR aufgelöst, wenn der Betrachter sie sehen darf (<paramref name="darfAlleTaskforces"/>
    /// = Führung/Admin/Aufsicht, sonst muss er via <paramref name="meId"/> zugeteilt sein); nicht sichtbare
    /// Taskforce-Verweise fehlen im Ergebnis (Aufrufer zeigen sie dann als „(nicht verfügbar)"/gar nicht).
    /// Hintergrund-Fan-out-Dienste, die für viele Empfänger EINMAL auflösen, übergeben
    /// (darfAlleTaskforces: true, meId: null) und filtern die Zustellung separat pro Empfänger.</summary>
    // Standard (darfAlleTaskforces: true) = ALLE Taskforces auflösen (bisheriges Verhalten). Betrachter-bezogene
    // Aufrufer MÜSSEN den echten Kontext (darfAlleTaskforces=DarfAlleTaskforcesSehen, meId) übergeben, damit fremde
    // Taskforces gar nicht erst aufgelöst werden. Hintergrund-Fan-out (viele Empfänger) bleibt beim Standard und
    // filtert pro Empfänger separat (IstAkteSichtbarAsync mit Empfänger-Id).
    public static async Task<Dictionary<(string Type, string Id), Resolution>> ResolveAsync(
        AppDbContext db, IReadOnlyCollection<(string Type, string Id)> refs, CancellationToken ct = default,
        bool mayAllTaskforces = true, string? meId = null)
    {
        var map = new Dictionary<(string, string), Resolution>();
        if (refs.Count == 0)
        {
            return map;
        }

        await ResolveRecordsAsync(db, refs, map, mayAllTaskforces, meId, ct);

        // Quellen: Anzeige = Titel; Verschlusssache + Route von der Eltern-Akte abgeleitet.
        var sourceIds = refs.Where(r => r.Type == nameof(Source)).Select(r => r.Id).Distinct().ToList();
        if (sourceIds.Count > 0)
        {
            var sources = await db.Sources.Where(q => sourceIds.Contains(q.Id))
                .Select(q => new { q.Id, q.Title, q.Type, q.Url, q.EntityType, q.EntityId })
                .ToListAsync(ct);
            // Eltern-Akten der Quellen auflösen (für VS + Route), falls nicht ohnehin schon aufgelöst.
            var parentsRefs = sources.Select(q => (q.EntityType, q.EntityId)).Distinct().ToList();
            await ResolveRecordsAsync(db, parentsRefs, map, mayAllTaskforces, meId, ct);
            foreach (var q in sources)
            {
                map.TryGetValue((q.EntityType, q.EntityId), out var parents);
                var href = q.Type switch
                {
                    SourceType.Upload => $"/dateien/quellen/{q.Id}",
                    SourceType.Link => string.IsNullOrWhiteSpace(q.Url) ? null : q.Url,
                    _ => parents.Href is { } h ? $"{h}?tab=quellen" : null,
                };
                map[(nameof(Source), q.Id)] = new Resolution(
                    string.IsNullOrWhiteSpace(q.Title) ? "Quelle" : q.Title,
                    parents.Classified, href);
            }
        }

        return map;
    }

    private static async Task ResolveRecordsAsync(
        AppDbContext db, IReadOnlyCollection<(string Type, string Id)> refs,
        Dictionary<(string, string), Resolution> map, bool mayAllTaskforces, string? meId, CancellationToken ct)
    {
        List<string> OpenIds(string type) => refs
            .Where(r => r.Type == type && !map.ContainsKey((type, r.Id)))
            .Select(r => r.Id).Distinct().ToList();

        var personIds = OpenIds(nameof(Person));
        if (personIds.Count > 0)
        {
            foreach (var x in await db.People.Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified }).ToListAsync(ct))
            {
                map[(nameof(Person), x.Id)] = new($"{x.Name} ({x.CaseNumber})", x.IsClassified, SearchNavigation.Route(nameof(Person), x.Id));
            }
        }

        var factionIds = OpenIds(nameof(Faction));
        if (factionIds.Count > 0)
        {
            foreach (var x in await db.Factions.Where(f => factionIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Name, f.CaseNumber, f.IsClassified }).ToListAsync(ct))
            {
                map[(nameof(Faction), x.Id)] = new($"{x.Name} ({x.CaseNumber})", x.IsClassified, SearchNavigation.Route(nameof(Faction), x.Id));
            }
        }

        var groupsIds = OpenIds(nameof(PersonGroup));
        if (groupsIds.Count > 0)
        {
            foreach (var x in await db.PersonGroups.Where(g => groupsIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name, g.CaseNumber, g.IsClassified }).ToListAsync(ct))
            {
                map[(nameof(PersonGroup), x.Id)] = new($"{x.Name} ({x.CaseNumber})", x.IsClassified, SearchNavigation.Route(nameof(PersonGroup), x.Id));
            }
        }

        var partyIds = OpenIds(nameof(Party));
        if (partyIds.Count > 0)
        {
            foreach (var x in await db.Parties.Where(p => partyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified }).ToListAsync(ct))
            {
                map[(nameof(Party), x.Id)] = new($"{x.Name} ({x.CaseNumber})", x.IsClassified, SearchNavigation.Route(nameof(Party), x.Id));
            }
        }

        var operationIds = OpenIds(nameof(Operation));
        if (operationIds.Count > 0)
        {
            foreach (var x in await db.Operations.Where(o => operationIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Title, o.CaseNumber, o.IsClassified }).ToListAsync(ct))
            {
                map[(nameof(Operation), x.Id)] = new($"{x.Title} ({x.CaseNumber})", x.IsClassified, SearchNavigation.Route(nameof(Operation), x.Id));
            }
        }

        var taskforceIds = OpenIds(nameof(Taskforce));
        if (taskforceIds.Count > 0)
        {
            // Nur die für den Betrachter sichtbaren Taskforces auflösen (zugeteilt oder darf alle sehen);
            // die übrigen bleiben unaufgelöst → Aufrufer zeigen sie als „(nicht verfügbar)"/gar nicht.
            var visible = await TaskforceVisibility.VisibleIdsAsync(db, taskforceIds, mayAllTaskforces, meId, ct);
            if (visible.Count > 0)
            {
                foreach (var x in await db.Taskforces.Where(t => visible.Contains(t.Id))
                    .Select(t => new { t.Id, t.Name, t.CaseNumber }).ToListAsync(ct))
                {
                    // Verschluss bewusst false: Die Mitgliedschaft hat die Sichtbarkeit bereits entschieden (nicht
                    // sichtbare Taskforces sind gar nicht in `sichtbar`). So verbergen nachgelagerte VS-Prüfungen
                    // der Aufrufer einem zugeteilten Mitglied NICHT fälschlich den Namen seiner VS-Taskforce.
                    map[(nameof(Taskforce), x.Id)] = new($"{x.Name} ({x.CaseNumber})", false, SearchNavigation.Route(nameof(Taskforce), x.Id));
                }
            }
        }

        var caseIds = OpenIds(nameof(Case));
        if (caseIds.Count > 0)
        {
            foreach (var x in await db.Cases.Where(v => caseIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Title, v.CaseNumber, v.IsClassified }).ToListAsync(ct))
            {
                map[(nameof(Case), x.Id)] = new($"{x.Title} ({x.CaseNumber})", x.IsClassified, SearchNavigation.Route(nameof(Case), x.Id));
            }
        }

        // Aufgabe: kein Verschlusssache-Konzept (Team-Board), ABER „eingeschränkte" Aufgaben sind nur für
        // Beteiligte (Ersteller/Zugeteilte) bzw. die Aufsicht sichtbar. darfAlleTaskforces trägt hier denselben
        // „Aufsicht darf alles"-Wert (DarfAlleTaskforcesSehen == DarfVerschlusssacheLesen); nicht sichtbare
        // Aufgaben bleiben unaufgelöst → Aufrufer zeigen sie als „(nicht verfügbar)"/gar nicht. Verschluss fest false.
        var jobIds = OpenIds(nameof(Job));
        if (jobIds.Count > 0)
        {
            var visible = await JobVisibility.VisibleIdsAsync(db, jobIds, mayAllTaskforces, meId, ct);
            if (visible.Count > 0)
            {
                foreach (var x in await db.Jobs.Where(a => visible.Contains(a.Id))
                    .Select(a => new { a.Id, a.Title, a.CaseNumber }).ToListAsync(ct))
                {
                    map[(nameof(Job), x.Id)] = new($"{x.Title} ({x.CaseNumber})", false, SearchNavigation.Route(nameof(Job), x.Id));
                }
            }
        }

        // Termin (Phase 8 – Block C): wie Aufgabe – kein Verschlusssache-Konzept, ABER „eingeschränkte"
        // Termine sind nur für Beteiligte (Ersteller/Teilnehmer) bzw. die Aufsicht sichtbar. Nicht sichtbare
        // Termine bleiben unaufgelöst → Aufrufer zeigen sie als „(nicht verfügbar)"/gar nicht. Verschluss fest false.
        var appointmentIds = OpenIds(nameof(Appointment));
        if (appointmentIds.Count > 0)
        {
            var visible = await AppointmentVisibility.VisibleIdsAsync(db, appointmentIds, mayAllTaskforces, meId, ct);
            if (visible.Count > 0)
            {
                foreach (var x in await db.Appointments.Where(t => visible.Contains(t.Id))
                    .Select(t => new { t.Id, t.Title, t.CaseNumber }).ToListAsync(ct))
                {
                    map[(nameof(Appointment), x.Id)] = new($"{x.Title} ({x.CaseNumber})", false, SearchNavigation.Route(nameof(Appointment), x.Id));
                }
            }
        }

        // Bibliotheks-Dokument: Anzeige = Titel, Route auf den Viewer. Jede Verschluss-Stufe (Führung/TRU/HRB)
        // gilt hier als Verschlusssache → der Titel bleibt in fremden Akten-/Verweis-Kontexten der Führung
        // vorbehalten (kein Namens-Leak an Dritte).
        var documentIds = OpenIds(nameof(Document));
        if (documentIds.Count > 0)
        {
            foreach (var x in await db.Documents.Where(d => documentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Title, Classified = d.IsClassified || d.IsTRUClassified || d.IsHRBClassified }).ToListAsync(ct))
            {
                map[(nameof(Document), x.Id)] = new(
                    string.IsNullOrWhiteSpace(x.Title) ? "Dokument" : x.Title,
                    x.Classified, SearchNavigation.Route(nameof(Document), x.Id));
            }
        }

        // Agent: kein Verschlusssache-Konzept; Verweis auf die Personalakte (/personal/{id}, für jeden aktiven
        // Agenten zugänglich) – nur der Codename als Anzeigename (Klarname bleibt verborgen).
        var agentIds = OpenIds(nameof(Agent));
        if (agentIds.Count > 0)
        {
            foreach (var x in await db.Users.Where(u => agentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename }).ToListAsync(ct))
            {
                map[(nameof(Agent), x.Id)] = new(string.IsNullOrWhiteSpace(x.Codename) ? "(unbenannter Agent)" : x.Codename,
                    false, $"/personal/{x.Id}");
            }
        }
    }
}
