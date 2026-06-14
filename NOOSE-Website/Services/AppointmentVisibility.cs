using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Appointments;
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
public static class AppointmentVisibility
{
    /// <summary>Allgemeine Zugriffsregel: darf der Aufrufer den Termin überhaupt sehen?</summary>
    public static IQueryable<Appointment> OnlyVisible(this IQueryable<Appointment> query, AppDbContext db, bool mayAll, string? meId)
    {
        if (mayAll)
        {
            return query; // Aufsicht/Führung sieht alle Stufen (auch Privat).
        }
        if (string.IsNullOrEmpty(meId))
        {
            // Ohne Agent-Kontext (fail-closed): nur öffentliche Termine.
            return query.Where(t => t.Visibility == AppointmentVisibilityLevel.Public);
        }
        return query.Where(t => t.Visibility == AppointmentVisibilityLevel.Public
            || t.CreatedById == meId
            || (t.Visibility == AppointmentVisibilityLevel.Restricted
                && db.AppointmentAssignments.Any(z => z.AppointmentId == t.Id && z.AgentId == meId)));
    }

    /// <summary>Aus einer Kandidatenmenge die für den Aufrufer zugänglichen Termin-Ids (Batch-Referenzauflösung).</summary>
    public static async Task<HashSet<string>> VisibleIdsAsync(AppDbContext db, IReadOnlyCollection<string> appointmentIds,
        bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        if (appointmentIds.Count == 0)
        {
            return new();
        }
        if (mayAll)
        {
            return appointmentIds.ToHashSet();
        }
        var hasMe = !string.IsNullOrEmpty(meId);
        var visible = await db.Appointments
            .Where(t => appointmentIds.Contains(t.Id)
                && (t.Visibility == AppointmentVisibilityLevel.Public
                    || (hasMe && (t.CreatedById == meId
                        || (t.Visibility == AppointmentVisibilityLevel.Restricted
                            && db.AppointmentAssignments.Any(z => z.AppointmentId == t.Id && z.AgentId == meId))))))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        return visible.ToHashSet();
    }

    /// <summary>Mein-Kalender-Filter: Termine, an denen ich beteiligt bin (Ersteller ODER Teilnehmer) – jede Stufe.</summary>
    public static IQueryable<Appointment> OnlyOwn(this IQueryable<Appointment> query, AppDbContext db, string? meId)
    {
        if (string.IsNullOrEmpty(meId))
        {
            return query.Where(_ => false);
        }
        return query.Where(t => t.CreatedById == meId
            || db.AppointmentAssignments.Any(z => z.AppointmentId == t.Id && z.AgentId == meId));
    }

    /// <summary>Behörden-Kalender-Filter: öffentliche Termine; die Aufsicht/Führung sieht zusätzlich alle Stufen.</summary>
    public static IQueryable<Appointment> ForAuthority(this IQueryable<Appointment> query, bool mayAll)
    {
        return mayAll ? query : query.Where(t => t.Visibility == AppointmentVisibilityLevel.Public);
    }
}
