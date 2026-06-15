using System.Security.Claims;
using NOOSE_Website.Data.Entities.Announcements;
using NOOSE_Website.Models.Announcements;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik des Schwarzen Bretts / der Behörden-Broadcasts – Phase 6. Eine Ankündigung erscheint für ihre
/// Zielgruppe am Brett; optional als Glocken-Broadcast und/oder mit Quittierung. Einfache Brett-Einträge (Zielgruppe
/// Alle, kein Push, keine Quittierung) darf jeder aktive Agent anlegen; die Broadcast-Features (gezielte Zielgruppe,
/// Push, Quittierung) sind der Führung vorbehalten und werden serverseitig erzwungen.
/// </summary>
public interface IAnnouncementService
{
    /// <summary>Die für den Aufrufer sichtbaren Ankündigungen (Wichtig zuerst, dann neueste). Führung sieht alle.</summary>
    Task<List<AnnouncementRow>> GetBoardAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Detail einer Ankündigung – oder null, wenn der Aufrufer sie nicht sehen darf.</summary>
    Task<AnnouncementView?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Gelöschte Ankündigungen (Papierkorb, Führung).</summary>
    Task<List<Announcement>> GetTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt eine Ankündigung an. Sind Broadcast-Features gesetzt (Zielgruppe ≠ Alle, Push oder Quittierung),
    /// ist die Aktion der Führung vorbehalten. Bei Quittierung wird der Empfängerkreis als Snapshot erfasst;
    /// bei Push erhält der Empfängerkreis (außer dem Verfasser) eine Glocken-Meldung.
    /// </summary>
    Task<Announcement> CreateAsync(AnnouncementInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Titel/Inhalt/Wichtig bearbeiten – nur Ersteller oder Führung. Broadcast-Einstellungen sind fix.</summary>
    Task RefreshAsync(string id, AnnouncementInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Kenntnisnahme (Quittierung) durch den Aufrufer – setzt seinen Quittierungs-Zeitpunkt.</summary>
    Task AcknowledgeAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Anzahl Ankündigungen, die der Aufrufer noch quittieren muss (für das NavMenu-Badge).</summary>
    Task<int> GetOpenAcknowledgmentsCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <summary>Listen-/Kartenzeile einer Ankündigung fürs Schwarze Brett (öffentliche Codenamen, nie Klarname).</summary>
public sealed class AnnouncementRow
{
    public string Id { get; set; } = string.Empty;
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool Important { get; set; }
    public AnnouncementAudience Audience { get; set; }
    /// <summary>Anzeigetext der Zielgruppe (inkl. aufgelöstem Taskforce-Namen bzw. Mindest-Dienstgrad).</summary>
    public string TargetDisplay { get; set; } = string.Empty;
    public bool AsBroadcast { get; set; }
    public bool AcknowledgmentRequired { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatorCodename { get; set; }

    /// <summary>Der Aufrufer hat eine offene Quittierung zu dieser Ankündigung.</summary>
    public bool MustAcknowledge { get; set; }
    /// <summary>Der Aufrufer hat bereits quittiert.</summary>
    public bool AlreadyAcknowledged { get; set; }
    /// <summary>Anzahl bereits quittierter Empfänger (nur sinnvoll bei <see cref="QuittierungVerlangt"/>).</summary>
    public int AcknowledgedCount { get; set; }
    /// <summary>Gesamtzahl der quittierungspflichtigen Empfänger.</summary>
    public int TotalCount { get; set; }
    /// <summary>Der Aufrufer darf die Ankündigung bearbeiten/löschen + die Quittierungsliste sehen (Ersteller/Führung).</summary>
    public bool MayManage { get; set; }
}

/// <summary>Detailansicht einer Ankündigung – Kopfzeile + (für Verwalter) die Quittierungsliste.</summary>
public sealed class AnnouncementView
{
    public AnnouncementRow Row { get; init; } = default!;
    public IReadOnlyList<AcknowledgmentRow> Acknowledgments { get; init; } = Array.Empty<AcknowledgmentRow>();
}

/// <summary>Eine Zeile der Quittierungsliste (Codename + Zeitpunkt; null = noch offen).</summary>
public sealed record AcknowledgmentRow(string Codename, DateTime? AcknowledgedAt);
