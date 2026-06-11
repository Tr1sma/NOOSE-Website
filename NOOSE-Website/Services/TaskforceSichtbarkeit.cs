using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Taskforces;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Sichtbarkeitsregel für Taskforces: Ein Agent sieht nur Taskforces, denen er ZUGETEILT ist
/// (es existiert eine Zeile in <c>TaskforceAgenten</c>). Die Führung (<c>ClaimsPrincipal.IstFuehrung()</c>,
/// d. h. Supervisory Special Agent+ oder Admin) sieht ALLE. Drei Formen derselben Regel:
/// Query-Prädikat für Listen/Suchen/Dashboard, Punkt-Check für die Detailseite, Batch-Check für die
/// Referenz-/Verknüpfungsauflösung. Mitgliedschaftsmuster analog <see cref="AnkuendigungService"/> (Zielgruppe
/// Taskforce). Der Soft-Delete-/Papierkorb-Filter greift weiterhin über die globalen Query-Filter.
/// </summary>
public static class TaskforceSichtbarkeit
{
    /// <summary>Filtert eine Taskforce-Query auf die für den Aufrufer sichtbaren Einträge.</summary>
    public static IQueryable<Taskforce> NurSichtbare(this IQueryable<Taskforce> query, AppDbContext db, bool darfAlles, string? meId)
    {
        if (darfAlles)
        {
            return query;
        }
        if (string.IsNullOrEmpty(meId))
        {
            return query.Where(_ => false);
        }
        return query.Where(t => db.TaskforceAgenten.Any(ta => ta.TaskforceId == t.Id && ta.AgentId == meId));
    }

    /// <summary>True, wenn die Taskforce existiert und für den Aufrufer sichtbar ist (Führung/Admin oder Mitglied).</summary>
    public static async Task<bool> IstSichtbarAsync(AppDbContext db, string taskforceId, bool darfAlles, string? meId, CancellationToken cancellationToken = default)
    {
        if (!await db.Taskforces.AnyAsync(t => t.Id == taskforceId, cancellationToken))
        {
            return false;
        }
        if (darfAlles)
        {
            return true;
        }
        return !string.IsNullOrEmpty(meId)
            && await db.TaskforceAgenten.AnyAsync(ta => ta.TaskforceId == taskforceId && ta.AgentId == meId, cancellationToken);
    }

    /// <summary>Aus einer Kandidatenmenge die für den Aufrufer sichtbaren Taskforce-Ids (für Batch-Referenzauflösung).</summary>
    public static async Task<HashSet<string>> SichtbareIdsAsync(AppDbContext db, IReadOnlyCollection<string> taskforceIds, bool darfAlles, string? meId, CancellationToken cancellationToken = default)
    {
        if (taskforceIds.Count == 0)
        {
            return new();
        }
        if (darfAlles)
        {
            return taskforceIds.ToHashSet();
        }
        if (string.IsNullOrEmpty(meId))
        {
            return new();
        }
        var sichtbar = await db.TaskforceAgenten
            .Where(ta => ta.AgentId == meId && taskforceIds.Contains(ta.TaskforceId))
            .Select(ta => ta.TaskforceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return sichtbar.ToHashSet();
    }
}
