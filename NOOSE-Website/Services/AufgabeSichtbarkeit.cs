using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Aufgaben;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Sichtbarkeitsregel für Aufgaben: Nicht-eingeschränkte Aufgaben sieht jeder (Team-Board).
/// Eine als <see cref="Aufgabe.IstEingeschraenkt"/> markierte Aufgabe sieht nur, wer ihr ZUGETEILT ist
/// (Zeile in <c>AufgabeZuweisungen</c>), ihr ERSTELLER, oder wer ohnehin alles sehen darf
/// (<c>ClaimsPrincipal.DarfVerschlusssacheLesen()</c> = Führung/Admin + Teamleitung/Nur-Lese-Aufsicht).
/// Zwei Formen derselben Regel: Query-Prädikat (Board/Suche/Picker) und Batch-Check (Referenz-/Verknüpfungs-
/// auflösung). Der Soft-Delete-/Papierkorb-Filter greift weiterhin über die globalen Query-Filter.
/// Immer hierüber filtern, nie das Prädikat kopieren. Analog <see cref="TaskforceSichtbarkeit"/>.
/// </summary>
public static class AufgabeSichtbarkeit
{
    /// <summary>Filtert eine Aufgaben-Query auf die für den Aufrufer sichtbaren Einträge (eingeschränkte nur für Beteiligte/Aufsicht).</summary>
    public static IQueryable<Aufgabe> NurSichtbare(this IQueryable<Aufgabe> query, AppDbContext db, bool darfAlles, string? meId)
    {
        if (darfAlles)
        {
            return query;
        }
        if (string.IsNullOrEmpty(meId))
        {
            // Ohne Agent-Kontext (fail-closed): nur nicht-eingeschränkte Aufgaben.
            return query.Where(a => !a.IstEingeschraenkt);
        }
        return query.Where(a => !a.IstEingeschraenkt
            || a.ErstelltVonId == meId
            || db.AufgabeZuweisungen.Any(z => z.AufgabeId == a.Id && z.AgentId == meId));
    }

    /// <summary>Aus einer Kandidatenmenge die für den Aufrufer sichtbaren Aufgaben-Ids (für Batch-Referenzauflösung).</summary>
    public static async Task<HashSet<string>> SichtbareIdsAsync(AppDbContext db, IReadOnlyCollection<string> aufgabeIds,
        bool darfAlles, string? meId, CancellationToken cancellationToken = default)
    {
        if (aufgabeIds.Count == 0)
        {
            return new();
        }
        if (darfAlles)
        {
            return aufgabeIds.ToHashSet();
        }
        var hatMe = !string.IsNullOrEmpty(meId);
        var sichtbar = await db.Aufgaben
            .Where(a => aufgabeIds.Contains(a.Id)
                && (!a.IstEingeschraenkt
                    || (hatMe && (a.ErstelltVonId == meId
                        || db.AufgabeZuweisungen.Any(z => z.AufgabeId == a.Id && z.AgentId == meId)))))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        return sichtbar.ToHashSet();
    }
}
