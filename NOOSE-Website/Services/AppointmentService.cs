using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Appointments;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITerminService" />
public class AppointmentService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICaseNumberService caseNumber,
    INotificationService notifications) : IAppointmentService
{
    public async Task<Appointment?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Eingeschränkte Termine sind nur für Beteiligte/Aufsicht zugänglich (null = „nicht gefunden/zugänglich").
        return await db.Appointments
            .OnlyVisible(db, actor.MayClassifiedRead(), actor.GetAgentId())
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<List<Appointment>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Appointments.IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Appointment>> SearchAsync(string? searchText, bool mayAll, string? meId, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Eingeschränkte Termine tauchen im Picker nur für Beteiligte/Aufsicht auf.
        var query = db.Appointments.OnlyVisible(db, mayAll, meId);

        var s = searchText?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(t => t.Title.Contains(s) || t.CaseNumber.Contains(s));
        }

        return await query
            .OrderByDescending(t => t.Start)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Appointment> CreateAsync(AppointmentInput input, IReadOnlyList<string> agentIds,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var (startUtc, endUtc) = TimesFromInput(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var appointment = new Appointment
        {
            CaseNumber = await caseNumber.NextAsync(db, "TM", cancellationToken),
            Title = input.Title.Trim(),
            Category = input.Category,
            Status = input.Status,
            Location = input.Location.TrimToNull(),
            Start = startUtc,
            End = endUtc,
            AllDay = input.AllDay,
            Description = input.Description.TrimToNull(),
            Visibility = input.Visibility,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(cancellationToken);

        // Nur tatsächlich existierende, aktive Agenten zuteilen (dedupliziert).
        var valid = agentIds.Count == 0
            ? new List<string>()
            : await db.Users
                .Where(u => agentIds.Contains(u.Id) && u.Status == AgentStatus.Active)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        foreach (var agentId in valid.Distinct())
        {
            db.AppointmentAssignments.Add(new AppointmentAssignment { AppointmentId = appointment.Id, AgentId = agentId });
        }
        if (valid.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        // Nach dem Commit benachrichtigen (der Ersteller selbst bekommt keine Meldung).
        var creatorId = actor.GetAgentId();
        foreach (var agentId in valid.Distinct().Where(x => x != creatorId))
        {
            await notifications.NotifyAsync(agentId, NotificationType.AppointmentAssigned,
                $"Du bist als Teilnehmer eingetragen: „{appointment.Title}“.", $"/kalender/{appointment.Id}", cancellationToken);
        }

        return appointment;
    }

    public async Task RefreshAsync(string id, AppointmentInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var (startUtc, endUtc) = TimesFromInput(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var appointment = await db.Appointments.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{id}' nicht gefunden.");
        RequireCreatorOrLeadership(appointment, actor);

        appointment.Title = input.Title.Trim();
        appointment.Category = input.Category;
        appointment.Status = input.Status;
        appointment.Location = input.Location.TrimToNull();
        appointment.Start = startUtc;
        appointment.End = endUtc;
        appointment.AllDay = input.AllDay;
        appointment.Description = input.Description.TrimToNull();
        appointment.Visibility = input.Visibility;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var appointment = await db.Appointments.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{id}' nicht gefunden.");
        RequireCreatorOrLeadership(appointment, actor);
        db.Appointments.Remove(appointment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var appointment = await db.Appointments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{id}' nicht gefunden.");

        appointment.IsDeleted = false;
        appointment.DeletedAt = null;
        appointment.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AppointmentAssignment>> GetParticipantAsync(string appointmentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AppointmentAssignments
            .Where(z => z.AppointmentId == appointmentId)
            .Include(z => z.Agent)
            .OrderBy(z => z.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentAssignAsync(string appointmentId, string agentId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var appointment = await db.Appointments.FirstOrDefaultAsync(t => t.Id == appointmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Termin '{appointmentId}' nicht gefunden.");
        RequireCreatorOrLeadership(appointment, actor);

        if (!await db.Users.AnyAsync(u => u.Id == agentId && u.Status == AgentStatus.Active, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden oder ist nicht aktiv.");
        }
        if (await db.AppointmentAssignments.AnyAsync(z => z.AppointmentId == appointmentId && z.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist dem Termin bereits zugeteilt.");
        }

        db.AppointmentAssignments.Add(new AppointmentAssignment { AppointmentId = appointmentId, AgentId = agentId });
        await db.SaveChangesAsync(cancellationToken);

        if (agentId != actor.GetAgentId())
        {
            await notifications.NotifyAsync(agentId, NotificationType.AppointmentAssigned,
                $"Du bist als Teilnehmer eingetragen: „{appointment.Title}“.", $"/kalender/{appointment.Id}", cancellationToken);
        }
    }

    public async Task AgentRemoveAsync(string assignmentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await db.AppointmentAssignments
            .Include(z => z.Appointment)
            .FirstOrDefaultAsync(z => z.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return;
        }
        if (assignment.Appointment is not null)
        {
            RequireCreatorOrLeadership(assignment.Appointment, actor);
        }
        db.AppointmentAssignments.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- Helfer ----

    /// <summary>Lokale RP-Zeit (wie eingegeben) → UTC für die Speicherung. Behandelt unspezifizierte Kinds als lokal.</summary>
    private static DateTime LocalByUtc(DateTime local)
        => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    /// <summary>Prüft/normalisiert Beginn &amp; Ende der Eingabe und liefert sie als UTC. Ganztägig → auf das reine
    /// Datum (00:00) normalisiert; das Ende darf nicht vor dem Beginn liegen.</summary>
    private static (DateTime Start, DateTime? End) TimesFromInput(AppointmentInput input)
    {
        if (input.Start is null)
        {
            throw new InvalidOperationException("Ein Termin braucht einen Beginn.");
        }
        var startLocal = input.AllDay ? input.Start.Value.Date : input.Start.Value;
        DateTime? endLocal = input.End is { } e ? (input.AllDay ? e.Date : e) : null;
        if (endLocal is { } el && el < startLocal)
        {
            throw new InvalidOperationException("Das Ende darf nicht vor dem Beginn liegen.");
        }
        return (LocalByUtc(startLocal), endLocal is { } x ? LocalByUtc(x) : null);
    }

    private static void RequireCreatorOrLeadership(Appointment appointment, ClaimsPrincipal actor)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var meId = actor.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && appointment.CreatedById == meId)
        {
            return;
        }
        throw new UnauthorizedAccessException("Diesen Termin darf nur sein Ersteller oder die Führung bearbeiten.");
    }
}
