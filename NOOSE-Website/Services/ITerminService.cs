using System.Security.Claims;
using NOOSE_Website.Data.Entities.Termine;
using NOOSE_Website.Models.Termine;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Termine/Kalender-Akten – Phase 8 (Block C). Wie eine Aufgabe (Team-Board): nicht
/// eingeschränkte Termine sehen alle aktiven Agenten; eingeschränkte nur Ersteller, zugeteilte Teilnehmer und
/// die Aufsicht (<c>DarfVerschlusssacheLesen()</c>). Anlegen mit Mehrfach-Zuteilung, Bearbeiten/Papierkorb,
/// Teilnehmer zuteilen/entfernen. Zeiten werden als UTC gespeichert (Eingabe = lokale RP-Zeit).
/// </summary>
public interface ITerminService
{
    /// <summary>Lädt einen Termin – liefert null, wenn er eingeschränkt und für den Aufrufer nicht sichtbar ist.</summary>
    Task<Termin?> GetDetailAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task<List<Termin>> GetPapierkorbAsync(CancellationToken cancellationToken = default);

    /// <summary>Termin-Suche für Picker; eingeschränkte Termine nur für Beteiligte/Aufsicht (<paramref name="darfAlles"/> = DarfVerschlusssacheLesen).</summary>
    Task<List<Termin>> SucheAsync(string? suchtext, bool darfAlles, string? meId, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Legt einen Termin an, teilt ihn den angegebenen aktiven Agenten zu und benachrichtigt diese (außer den Ersteller).</summary>
    Task<Termin> ErstellenAsync(TerminEingabe eingabe, IReadOnlyList<string> agentIds, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Stammdaten bearbeiten – nur Ersteller oder Führung.</summary>
    Task AktualisierenAsync(string id, TerminEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Dem Termin zugeteilte Teilnehmer (inkl. Agent-Daten; nach Codename).</summary>
    Task<List<TerminZuweisung>> GetTeilnehmerAsync(string terminId, CancellationToken cancellationToken = default);

    /// <summary>Teilnehmer zuteilen – nur Ersteller oder Führung; benachrichtigt den Agenten (außer er ist der Handelnde).</summary>
    Task AgentZuweisenAsync(string terminId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Teilnahme aufheben – nur Ersteller oder Führung.</summary>
    Task AgentEntfernenAsync(string zuweisungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
