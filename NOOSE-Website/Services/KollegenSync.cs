using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsame Pflege der automatischen „Kollegen"-Verknüpfungen zwischen Personen (Fraktion → „Fraktionskollege",
/// Personengruppe → „Gruppenkollege", Partei → „Parteikollege"). Jede Variante wird über ihr eigenes <c>Label</c>
/// getrennt verwaltet, damit sich die Links der verschiedenen Organisationsarten nicht gegenseitig löschen.
/// </summary>
public static class KollegenSync
{
    public const string Fraktionskollege = "Fraktionskollege";
    public const string Gruppenkollege = "Gruppenkollege";
    public const string Parteikollege = "Parteikollege";

    /// <summary>
    /// Gleicht die automatischen Verknüpfungen mit dem Label <paramref name="label"/> für die Person
    /// <paramref name="personId"/> ab: Zwischen P und Q soll genau dann eine solche (automatische)
    /// Verknüpfung bestehen, wenn Q in <paramref name="sollKollegen"/> steht. Läuft auf dem übergebenen
    /// Kontext (Transaktion des Aufrufers).
    /// </summary>
    /// <remarks>
    /// Es genügt, nur P abzugleichen: eine Verknüpfung ist nur eine Zeile, und hier werden beide Richtungen
    /// (<c>VonId == P || NachId == P</c>) berücksichtigt – ein späterer Abgleich von Q findet die Zeile von P
    /// wieder und legt keine Gegen-Richtung an. Etwaige Alt-Duplikate werden mit abgeräumt. Bewusst KEIN
    /// Unique-Index (würde mit soft-gelöschten manuellen Verknüpfungen kollidieren). Automatische
    /// Verknüpfungen werden hart gelöscht (maschinell gepflegt, kein Papierkorb).
    /// </remarks>
    public static async Task SyncAsync(AppDbContext db, string personId, string label,
        IReadOnlyCollection<string> sollKollegen, CancellationToken cancellationToken)
    {
        var sollSet = sollKollegen as HashSet<string> ?? sollKollegen.ToHashSet();

        // AsNoTracking: nur lesen + per Id hart löschen; keine getrackten Entitäten, die das spätere
        // SaveChanges (für die neuen Links) stören könnten.
        var bestehende = await db.Verknuepfungen.AsNoTracking()
            .Where(v => v.Automatisch && v.Label == label && v.VonTyp == nameof(Person) && v.NachTyp == nameof(Person)
                     && (v.VonId == personId || v.NachId == personId))
            .Select(v => new { v.Id, v.VonId, v.NachId })
            .ToListAsync(cancellationToken);

        var haben = new HashSet<string>();
        var zuEntfernenIds = new List<string>();
        foreach (var v in bestehende)
        {
            var andere = v.VonId == personId ? v.NachId : v.VonId;
            // Behalten, wenn weiterhin gewünscht und noch nicht vorhanden – sonst (auch Duplikate) entfernen.
            if (sollSet.Contains(andere) && haben.Add(andere))
            {
                continue;
            }
            zuEntfernenIds.Add(v.Id);
        }

        if (zuEntfernenIds.Count > 0)
        {
            await db.Verknuepfungen.Where(v => zuEntfernenIds.Contains(v.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        var zuErgaenzen = sollSet.Where(q => !haben.Contains(q)).ToList();
        foreach (var q in zuErgaenzen)
        {
            db.Verknuepfungen.Add(new Verknuepfung
            {
                VonTyp = nameof(Person),
                VonId = personId,
                NachTyp = nameof(Person),
                NachId = q,
                Label = label,
                Automatisch = true,
            });
        }
        if (zuErgaenzen.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
