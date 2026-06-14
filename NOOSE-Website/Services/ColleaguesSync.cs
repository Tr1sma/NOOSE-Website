using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsame Pflege der automatischen „Kollegen"-Verknüpfungen zwischen Personen (Fraktion → „Fraktionskollege",
/// Personengruppe → „Gruppenkollege", Partei → „Parteikollege"). Jede Variante wird über ihr eigenes <c>Label</c>
/// getrennt verwaltet, damit sich die Links der verschiedenen Organisationsarten nicht gegenseitig löschen.
/// </summary>
public static class ColleaguesSync
{
    public const string FactionColleague = "Fraktionskollege";
    public const string GroupColleague = "Gruppenkollege";
    public const string PartyColleague = "Parteikollege";

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
        IReadOnlyCollection<string> shouldColleagues, CancellationToken cancellationToken)
    {
        var shouldSet = shouldColleagues as HashSet<string> ?? shouldColleagues.ToHashSet();

        // AsNoTracking: nur lesen + per Id hart löschen; keine getrackten Entitäten, die das spätere
        // SaveChanges (für die neuen Links) stören könnten.
        var existing = await db.Links.AsNoTracking()
            .Where(v => v.Automatic && v.Label == label && v.SourceType == nameof(Person) && v.TargetType == nameof(Person)
                     && (v.SourceId == personId || v.TargetId == personId))
            .Select(v => new { v.Id, v.SourceId, v.TargetId })
            .ToListAsync(cancellationToken);

        var have = new HashSet<string>();
        var toRemoveIds = new List<string>();
        foreach (var v in existing)
        {
            var other = v.SourceId == personId ? v.TargetId : v.SourceId;
            // Behalten, wenn weiterhin gewünscht und noch nicht vorhanden – sonst (auch Duplikate) entfernen.
            if (shouldSet.Contains(other) && have.Add(other))
            {
                continue;
            }
            toRemoveIds.Add(v.Id);
        }

        if (toRemoveIds.Count > 0)
        {
            await db.Links.Where(v => toRemoveIds.Contains(v.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        var toSupplement = shouldSet.Where(q => !have.Contains(q)).ToList();
        foreach (var q in toSupplement)
        {
            db.Links.Add(new Link
            {
                SourceType = nameof(Person),
                SourceId = personId,
                TargetType = nameof(Person),
                TargetId = q,
                Label = label,
                Automatic = true,
            });
        }
        if (toSupplement.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
