using System.Security.Claims;
using NOOSE_Website.Data.Entities.Ankuendigungen;
using NOOSE_Website.Models.Ankuendigungen;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik des Schwarzen Bretts / der Behörden-Broadcasts – Phase 6. Eine Ankündigung erscheint für ihre
/// Zielgruppe am Brett; optional als Glocken-Broadcast und/oder mit Quittierung. Einfache Brett-Einträge (Zielgruppe
/// Alle, kein Push, keine Quittierung) darf jeder aktive Agent anlegen; die Broadcast-Features (gezielte Zielgruppe,
/// Push, Quittierung) sind der Führung vorbehalten und werden serverseitig erzwungen.
/// </summary>
public interface IAnkuendigungService
{
    /// <summary>Die für den Aufrufer sichtbaren Ankündigungen (Wichtig zuerst, dann neueste). Führung sieht alle.</summary>
    Task<List<AnkuendigungZeile>> GetBrettAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Detail einer Ankündigung – oder null, wenn der Aufrufer sie nicht sehen darf.</summary>
    Task<AnkuendigungAnsicht?> GetDetailAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Gelöschte Ankündigungen (Papierkorb, Führung).</summary>
    Task<List<Ankuendigung>> GetPapierkorbAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt eine Ankündigung an. Sind Broadcast-Features gesetzt (Zielgruppe ≠ Alle, Push oder Quittierung),
    /// ist die Aktion der Führung vorbehalten. Bei Quittierung wird der Empfängerkreis als Snapshot erfasst;
    /// bei Push erhält der Empfängerkreis (außer dem Verfasser) eine Glocken-Meldung.
    /// </summary>
    Task<Ankuendigung> ErstellenAsync(AnkuendigungEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Titel/Inhalt/Wichtig bearbeiten – nur Ersteller oder Führung. Broadcast-Einstellungen sind fix.</summary>
    Task AktualisierenAsync(string id, AnkuendigungEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Kenntnisnahme (Quittierung) durch den Aufrufer – setzt seinen Quittierungs-Zeitpunkt.</summary>
    Task QuittierenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Anzahl Ankündigungen, die der Aufrufer noch quittieren muss (für das NavMenu-Badge).</summary>
    Task<int> GetOffeneQuittierungenAnzahlAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}

/// <summary>Listen-/Kartenzeile einer Ankündigung fürs Schwarze Brett (öffentliche Codenamen, nie Klarname).</summary>
public sealed class AnkuendigungZeile
{
    public string Id { get; set; } = string.Empty;
    public string Aktenzeichen { get; set; } = string.Empty;
    public string Titel { get; set; } = string.Empty;
    public string Inhalt { get; set; } = string.Empty;
    public bool Wichtig { get; set; }
    public AnkuendigungZielgruppe Zielgruppe { get; set; }
    /// <summary>Anzeigetext der Zielgruppe (inkl. aufgelöstem Taskforce-Namen bzw. Mindest-Dienstgrad).</summary>
    public string ZielAnzeige { get; set; } = string.Empty;
    public bool AlsBroadcast { get; set; }
    public bool QuittierungVerlangt { get; set; }
    public DateTime ErstelltAm { get; set; }
    public string? ErstellerCodename { get; set; }

    /// <summary>Der Aufrufer hat eine offene Quittierung zu dieser Ankündigung.</summary>
    public bool MussQuittieren { get; set; }
    /// <summary>Der Aufrufer hat bereits quittiert.</summary>
    public bool SchonQuittiert { get; set; }
    /// <summary>Anzahl bereits quittierter Empfänger (nur sinnvoll bei <see cref="QuittierungVerlangt"/>).</summary>
    public int QuittiertAnzahl { get; set; }
    /// <summary>Gesamtzahl der quittierungspflichtigen Empfänger.</summary>
    public int GesamtAnzahl { get; set; }
    /// <summary>Der Aufrufer darf die Ankündigung bearbeiten/löschen + die Quittierungsliste sehen (Ersteller/Führung).</summary>
    public bool DarfVerwalten { get; set; }
}

/// <summary>Detailansicht einer Ankündigung – Kopfzeile + (für Verwalter) die Quittierungsliste.</summary>
public sealed class AnkuendigungAnsicht
{
    public AnkuendigungZeile Zeile { get; init; } = default!;
    public IReadOnlyList<QuittierungZeile> Quittierungen { get; init; } = Array.Empty<QuittierungZeile>();
}

/// <summary>Eine Zeile der Quittierungsliste (Codename + Zeitpunkt; null = noch offen).</summary>
public sealed record QuittierungZeile(string Codename, DateTime? QuittiertAm);
