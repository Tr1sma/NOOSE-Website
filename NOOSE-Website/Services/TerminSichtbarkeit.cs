using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Termine;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Sichtbarkeitsregeln für Termine – Phase 8 (Block C). Drei Stufen
/// (<see cref="TerminSichtbarkeitsStufe"/>): Öffentlich (alle), Eingeschränkt (Ersteller + Teilnehmer +
/// Aufsicht), Privat (nur Ersteller + Aufsicht). Die Aufsicht/Führung (<c>DarfVerschlusssacheLesen()</c>)
/// sieht alle Stufen. <see cref="NurSichtbare"/>/<see cref="SichtbareIdsAsync"/> = allgemeine „darf
/// zugreifen"-Regel (Detail/Referenzen/Zeitstrahl). <see cref="NurEigene"/> und <see cref="FuerBehoerde"/>
/// sind die beiden Kalender-Sichten. Immer hierüber filtern, nie das Prädikat kopieren.
/// </summary>
public static class TerminSichtbarkeit
{
    /// <summary>Allgemeine Zugriffsregel: darf der Aufrufer den Termin überhaupt sehen?</summary>
    public static IQueryable<Termin> NurSichtbare(this IQueryable<Termin> query, AppDbContext db, bool darfAlles, string? meId)
    {
        if (darfAlles)
        {
            return query; // Aufsicht/Führung sieht alle Stufen (auch Privat).
        }
        if (string.IsNullOrEmpty(meId))
        {
            // Ohne Agent-Kontext (fail-closed): nur öffentliche Termine.
            return query.Where(t => t.Sichtbarkeit == TerminSichtbarkeitsStufe.Oeffentlich);
        }
        return query.Where(t => t.Sichtbarkeit == TerminSichtbarkeitsStufe.Oeffentlich
            || t.ErstelltVonId == meId
            || (t.Sichtbarkeit == TerminSichtbarkeitsStufe.Eingeschraenkt
                && db.TerminZuweisungen.Any(z => z.TerminId == t.Id && z.AgentId == meId)));
    }

    /// <summary>Aus einer Kandidatenmenge die für den Aufrufer zugänglichen Termin-Ids (Batch-Referenzauflösung).</summary>
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
                && (t.Sichtbarkeit == TerminSichtbarkeitsStufe.Oeffentlich
                    || (hatMe && (t.ErstelltVonId == meId
                        || (t.Sichtbarkeit == TerminSichtbarkeitsStufe.Eingeschraenkt
                            && db.TerminZuweisungen.Any(z => z.TerminId == t.Id && z.AgentId == meId))))))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        return sichtbar.ToHashSet();
    }

    /// <summary>Mein-Kalender-Filter: Termine, an denen ich beteiligt bin (Ersteller ODER Teilnehmer) – jede Stufe.</summary>
    public static IQueryable<Termin> NurEigene(this IQueryable<Termin> query, AppDbContext db, string? meId)
    {
        if (string.IsNullOrEmpty(meId))
        {
            return query.Where(_ => false);
        }
        return query.Where(t => t.ErstelltVonId == meId
            || db.TerminZuweisungen.Any(z => z.TerminId == t.Id && z.AgentId == meId));
    }

    /// <summary>Behörden-Kalender-Filter: öffentliche Termine; die Aufsicht/Führung sieht zusätzlich alle Stufen.</summary>
    public static IQueryable<Termin> FuerBehoerde(this IQueryable<Termin> query, bool darfAlles)
    {
        return darfAlles ? query : query.Where(t => t.Sichtbarkeit == TerminSichtbarkeitsStufe.Oeffentlich);
    }
}
