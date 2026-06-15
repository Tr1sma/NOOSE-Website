using System.Security.Claims;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Models.Appointments;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Termine/Kalender-Akten – Phase 8 (Block C). Wie eine Aufgabe (Team-Board): nicht
/// eingeschränkte Termine sehen alle aktiven Agenten; eingeschränkte nur Ersteller, zugeteilte Teilnehmer und
/// die Aufsicht (<c>DarfVerschlusssacheLesen()</c>). Anlegen mit Mehrfach-Zuteilung, Bearbeiten/Papierkorb,
/// Teilnehmer zuteilen/entfernen. Zeiten werden als UTC gespeichert (Eingabe = lokale RP-Zeit).
/// </summary>
public interface IAppointmentService
{
    /// <summary>Lädt einen Termin – liefert null, wenn er eingeschränkt und für den Aufrufer nicht sichtbar ist.</summary>
    Task<Appointment?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<Appointment>> GetTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>Termin-Suche für Picker; eingeschränkte Termine nur für Beteiligte/Aufsicht (<paramref name="darfAlles"/> = DarfVerschlusssacheLesen).</summary>
    Task<List<Appointment>> SearchAsync(string? searchText, bool mayAll, string? meId, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Legt einen Termin an, teilt ihn den angegebenen aktiven Agenten zu und benachrichtigt diese (außer den Ersteller).</summary>
    Task<Appointment> CreateAsync(AppointmentInput input, IReadOnlyList<string> agentIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Stammdaten bearbeiten – nur Ersteller oder Führung.</summary>
    Task RefreshAsync(string id, AppointmentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Dem Termin zugeteilte Teilnehmer (inkl. Agent-Daten; nach Codename).</summary>
    Task<List<AppointmentAssignment>> GetParticipantAsync(string appointmentId, CancellationToken cancellationToken = default);

    /// <summary>Teilnehmer zuteilen – nur Ersteller oder Führung; benachrichtigt den Agenten (außer er ist der Handelnde).</summary>
    Task AgentAssignAsync(string appointmentId, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Teilnahme aufheben – nur Ersteller oder Führung.</summary>
    Task AgentRemoveAsync(string assignmentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
