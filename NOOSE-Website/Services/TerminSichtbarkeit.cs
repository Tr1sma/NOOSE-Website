using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Termine;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Sichtbarkeitsregel für Termine – analog <see cref="AufgabeSichtbarkeit"/>. Nicht-eingeschränkte
/// Termine sieht jeder aktive Agent. Ein als <see cref="Termin.IstEingeschraenkt"/> markierter Termin sieht nur,
/// wer ihm als Teilnehmer ZUGETEILT ist (Zeile in <c>TerminZuweisungen</c>), sein ERSTELLER, oder wer ohnehin
/// alles sehen darf (<c>ClaimsPrincipal.DarfVerschlusssacheLesen()</c> = Führung/Admin + Teamleitung/Nur-Lese-
/// Aufsicht). Zwei Formen derselben Regel: Query-Prädikat (Kalender/Suche/Picker) und Batch-Check (Referenz-/
/// Verknüpfungsauflösung). Der Soft-Delete-/Papierkorb-Filter greift weiterhin über die globalen Query-Filter.
/// Immer hierüber filtern, nie das Prädikat kopieren.
/// </summary>
public static class TerminSichtbarkeit
{
    /// <summary>Filtert eine Termin-Query auf die für den Aufrufer sichtbaren Einträge (eingeschränkte nur für Beteiligte/Aufsicht).</summary>
    public static IQueryable<Termin> NurSichtbare(this IQueryable<Termin> query, AppDbContext db, bool darfAlles, string? meId)
    {
        if (darfAlles)
        {
            return query;
        }
        if (string.IsNullOrEmpty(meId))
        {
            // Ohne Agent-Kontext (fail-closed): nur nicht-eingeschränkte Termine.
            return query.Where(t => !t.IstEingeschraenkt);
        }
        return query.Where(t => !t.IstEingeschraenkt
            || t.ErstelltVonId == meId
            || db.TerminZuweisungen.Any(z => z.TerminId == t.Id && z.AgentId == meId));
    }

    /// <summary>Aus einer Kandidatenmenge die für den Aufrufer sichtbaren Termin-Ids (für Batch-Referenzauflösung).</summary>
    public static async Task<HashSet<string>> SichtbareIdsAsync(AppDbContext db, IReadOnlyCollection<string> terminIds,
        bool darfAlles, string? meId, CancellationToken cancellationToken = default)
    {
        if (terminIds.Count == 0)
        {
            return new();
        }
        if (darfAlles)
        {
            return terminIds.ToHashSet();
        }
        var hatMe = !string.IsNullOrEmpty(meId);
        var sichtbar = await db.Termine
            .Where(t => terminIds.Contains(t.Id)
                && (!t.IstEingeschraenkt
                    || (hatMe && (t.ErstelltVonId == meId
                        || db.TerminZuweisungen.Any(z => z.TerminId == t.Id && z.AgentId == meId)))))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        return sichtbar.ToHashSet();
    }
}
