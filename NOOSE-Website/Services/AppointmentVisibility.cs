using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central appointment visibility rules; always filter through here, never copy the predicate.</summary>
public static class AppointmentVisibility
{
    /// <summary>General access rule: may the caller see the appointment at all?</summary>
    public static IQueryable<Appointment> OnlyVisible(this IQueryable<Appointment> query, AppDbContext db, bool mayAll, string? meId)
    {
        if (mayAll)
        {
            return query; // supervision sees all levels
        }
        if (string.IsNullOrEmpty(meId))
        {
            // Fail-closed without agent context.
            return query.Where(t => t.Visibility == AppointmentVisibilityLevel.Public);
        }
        return query.Where(t => t.Visibility == AppointmentVisibilityLevel.Public
            || t.CreatedById == meId
            || (t.Visibility == AppointmentVisibilityLevel.Restricted
                && db.AppointmentAssignments.Any(z => z.AppointmentId == t.Id && z.AgentId == meId)));
    }

    /// <summary>From a candidate set, the appointment ids accessible to the caller.</summary>
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

    /// <summary>My-calendar filter: appointments I am involved in, any level.</summary>
    public static IQueryable<Appointment> OnlyOwn(this IQueryable<Appointment> query, AppDbContext db, string? meId)
    {
        if (string.IsNullOrEmpty(meId))
        {
            return query.Where(_ => false);
        }
        return query.Where(t => t.CreatedById == meId
            || db.AppointmentAssignments.Any(z => z.AppointmentId == t.Id && z.AgentId == meId));
    }

    /// <summary>Authority-calendar filter: public appointments; supervision additionally sees all levels.</summary>
    public static IQueryable<Appointment> ForAuthority(this IQueryable<Appointment> query, bool mayAll)
    {
        return mayAll ? query : query.Where(t => t.Visibility == AppointmentVisibilityLevel.Public);
    }
}
