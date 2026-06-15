using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Jobs;

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
public static class JobVisibility
{
    /// <summary>Filtert eine Aufgaben-Query auf die für den Aufrufer sichtbaren Einträge (eingeschränkte nur für Beteiligte/Aufsicht).</summary>
    public static IQueryable<Job> OnlyVisible(this IQueryable<Job> query, AppDbContext db, bool mayAll, string? meId)
    {
        if (mayAll)
        {
            return query;
        }
        if (string.IsNullOrEmpty(meId))
        {
            // Ohne Agent-Kontext (fail-closed): nur nicht-eingeschränkte Aufgaben.
            return query.Where(a => !a.IsRestricted);
        }
        return query.Where(a => !a.IsRestricted
            || a.CreatedById == meId
            || db.JobAssignments.Any(z => z.JobId == a.Id && z.AgentId == meId));
    }

    /// <summary>Aus einer Kandidatenmenge die für den Aufrufer sichtbaren Aufgaben-Ids (für Batch-Referenzauflösung).</summary>
    public static async Task<HashSet<string>> VisibleIdsAsync(AppDbContext db, IReadOnlyCollection<string> jobIds,
        bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        if (jobIds.Count == 0)
        {
            return new();
        }
        if (mayAll)
        {
            return jobIds.ToHashSet();
        }
        var hasMe = !string.IsNullOrEmpty(meId);
        var visible = await db.Jobs
            .Where(a => jobIds.Contains(a.Id)
                && (!a.IsRestricted
                    || (hasMe && (a.CreatedById == meId
                        || db.JobAssignments.Any(z => z.JobId == a.Id && z.AgentId == meId)))))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        return visible.ToHashSet();
    }
}
