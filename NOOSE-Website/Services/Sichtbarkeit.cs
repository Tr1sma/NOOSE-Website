using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Verschlusssachen-/Papierkorb-Sichtbarkeitsprüfung für Akten (Person/Fraktion/Personengruppe).
/// Ersetzt die zuvor je Dienst kopierten <c>AkteSichtbarAsync</c>-Helfer und schließt die Lücke, dass
/// Quellen/Kommentare nur Personen prüften (Fraktionen/Gruppen blieben ungeschützt). Eine Akte ist
/// sichtbar, wenn sie existiert (nicht im Papierkorb – greift über den globalen Soft-Delete-Filter) und
/// entweder keine Verschlusssache ist oder der Aufrufer der Führung angehört. Unbekannte Typen gelten als
/// sichtbar (die Prüfung greift erst, wenn der Typ eine Akte mit Verschlusssache-Flag ist).
/// </summary>
public static class Sichtbarkeit
{
    /// <summary>True, wenn die Eltern-Akte für den Aufrufer sichtbar ist (existiert + VS-Regel erfüllt).</summary>
    public static async Task<bool> IstAkteSichtbarAsync(
        AppDbContext db, string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        // Personalakten-Kommentarbereich (EntitaetTyp = Agent) ist nur für die Führung/Admin lesbar+nutzbar
        // (Phase 5e): die übrige Personalakte ist offen, dieser interne Notiz-Bereich nicht.
        if (entitaetTyp == nameof(Agent))
        {
            return istFuehrung;
        }

        bool? verschluss = entitaetTyp switch
        {
            nameof(Person) => await db.Personen
                .Where(p => p.Id == entitaetId).Select(p => (bool?)p.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Fraktion) => await db.Fraktionen
                .Where(f => f.Id == entitaetId).Select(f => (bool?)f.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Personengruppe) => await db.Personengruppen
                .Where(g => g.Id == entitaetId).Select(g => (bool?)g.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Partei) => await db.Parteien
                .Where(p => p.Id == entitaetId).Select(p => (bool?)p.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Operation) => await db.Operationen
                .Where(o => o.Id == entitaetId).Select(o => (bool?)o.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Taskforce) => await db.Taskforces
                .Where(t => t.Id == entitaetId).Select(t => (bool?)t.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            nameof(Vorgang) => await db.Vorgaenge
                .Where(v => v.Id == entitaetId).Select(v => (bool?)v.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            // Aufgabe: keine Verschlusssache (Team-Board) – existiert sie, ist sie für alle aktiven Agenten sichtbar.
            nameof(Aufgabe) => await db.Aufgaben
                .Where(a => a.Id == entitaetId).Select(a => (bool?)false)
                .FirstOrDefaultAsync(cancellationToken),
            // Bibliotheks-Dokument: eigene Verschlusssache-Stufe (nur Führung sieht VS-Dokumente).
            nameof(Dokument) => await db.Dokumente
                .Where(d => d.Id == entitaetId).Select(d => (bool?)d.IstVerschlusssache)
                .FirstOrDefaultAsync(cancellationToken),
            // Andere Typen besitzen (noch) keine Verschlusssache-Stufe.
            _ => false,
        };

        // Bei unbekanntem Typ (kein Treffer im switch) gibt es keine Akte zu schützen → sichtbar.
        if (entitaetTyp is not (nameof(Person) or nameof(Fraktion) or nameof(Personengruppe) or nameof(Partei) or nameof(Operation) or nameof(Taskforce) or nameof(Vorgang) or nameof(Aufgabe) or nameof(Dokument)))
        {
            return true;
        }

        // verschluss == null → Akte nicht vorhanden (Papierkorb/unbekannt) → wie nicht sichtbar.
        return verschluss is not null && (istFuehrung || verschluss == false);
    }
}
