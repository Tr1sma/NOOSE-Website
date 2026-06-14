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

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Verschlusssachen-/Papierkorb-Sichtbarkeitsprüfung für Akten (Person/Fraktion/Personengruppe).
/// Ersetzt die zuvor je Dienst kopierten <c>AkteSichtbarAsync</c>-Helfer und schließt die Lücke, dass
/// Quellen/Kommentare nur Personen prüften (Fraktionen/Gruppen blieben ungeschützt). Eine Akte ist
/// sichtbar, wenn sie existiert (nicht im Papierkorb – greift über den globalen Soft-Delete-Filter) und
/// entweder keine Verschlusssache ist oder der Aufrufer der Führung angehört. Unbekannte Typen gelten als
/// sichtbar (die Prüfung greift erst, wenn der Typ eine Akte mit Verschlusssache-Flag ist).
/// </summary>
public static class Visibility
{
    /// <summary>True, wenn die Eltern-Akte für den Aufrufer sichtbar ist (existiert + VS-Regel erfüllt).
    /// <paramref name="meId"/> wird nur für Taskforces gebraucht (Mitgliedschafts-Sichtbarkeit) – für alle
    /// übrigen Typen ist es ohne Belang.</summary>
    public static async Task<bool> IsRecordVisibleAsync(
        AppDbContext db, string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default, string? meId = null)
    {
        // Personalakten-Kommentarbereich (EntitaetTyp = Agent) ist nur für die Führung/Admin lesbar+nutzbar
        // (Phase 5e): die übrige Personalakte ist offen, dieser interne Notiz-Bereich nicht.
        if (entityType == nameof(Agent))
        {
            return isLeadership;
        }

        // Taskforce: Sichtbar nur für Führung/Admin oder zugeteilte Mitglieder (NICHT mehr nur Verschlusssache).
        if (entityType == nameof(Taskforce))
        {
            return await TaskforceVisibility.IsVisibleAsync(db, entityId, isLeadership, meId, cancellationToken);
        }

        bool? classified = entityType switch
        {
            nameof(Person) => await db.People
                .Where(p => p.Id == entityId).Select(p => (bool?)p.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Faction) => await db.Factions
                .Where(f => f.Id == entityId).Select(f => (bool?)f.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            // Fraktions-Aktivität erbt die Sichtbarkeit ihrer Eltern-Fraktion (schützt die an die Aktivität
            // gehängten Quellen/Anhänge). Existiert die Aktivität oder die Fraktion nicht (Papierkorb), liefert
            // die Navigation null → unsichtbar.
            nameof(FactionActivity) => await db.FactionActivities
                .Where(a => a.Id == entityId).Select(a => (bool?)a.Faction!.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(PersonGroup) => await db.PersonGroups
                .Where(g => g.Id == entityId).Select(g => (bool?)g.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Party) => await db.Parties
                .Where(p => p.Id == entityId).Select(p => (bool?)p.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Operation) => await db.Operations
                .Where(o => o.Id == entityId).Select(o => (bool?)o.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            // Taskforce wird oben separat behandelt (Mitgliedschaft statt nur Verschlusssache).
            nameof(Case) => await db.Cases
                .Where(v => v.Id == entityId).Select(v => (bool?)v.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            // Aufgabe: keine Verschlusssache (Team-Board) – existiert sie, ist sie für alle aktiven Agenten sichtbar.
            nameof(Job) => await db.Jobs
                .Where(a => a.Id == entityId).Select(a => (bool?)false)
                .FirstOrDefaultAsync(cancellationToken),
            // Termin: wie Aufgabe – keine Verschlusssache; die „Eingeschränkt"-Regel greift separat über
            // TerminSichtbarkeit an den Aufrufstellen (Kalender/Detail/Zeitstrahl).
            nameof(Appointment) => await db.Appointments
                .Where(t => t.Id == entityId).Select(t => (bool?)false)
                .FirstOrDefaultAsync(cancellationToken),
            // Bibliotheks-Dokument: eigene Verschlusssache-Stufe (nur Führung sieht VS-Dokumente).
            nameof(Document) => await db.Documents
                .Where(d => d.Id == entityId).Select(d => (bool?)d.IsClassified)
                .FirstOrDefaultAsync(cancellationToken),
            // Gesetz (Phase 7): keine Verschlusssache (Wissensbasis) – existiert es, ist es sichtbar.
            nameof(Law) => await db.Laws
                .Where(g => g.Id == entityId).Select(g => (bool?)false)
                .FirstOrDefaultAsync(cancellationToken),
            // Andere Typen besitzen (noch) keine Verschlusssache-Stufe.
            _ => false,
        };

        // Bei unbekanntem Typ (kein Treffer im switch) gibt es keine Akte zu schützen → sichtbar.
        if (entityType is not (nameof(Person) or nameof(Faction) or nameof(FactionActivity) or nameof(PersonGroup) or nameof(Party) or nameof(Operation) or nameof(Case) or nameof(Job) or nameof(Appointment) or nameof(Document) or nameof(Law)))
        {
            return true;
        }

        // verschluss == null → Akte nicht vorhanden (Papierkorb/unbekannt) → wie nicht sichtbar.
        return classified is not null && (isLeadership || classified == false);
    }
}
