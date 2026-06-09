using Microsoft.AspNetCore.Identity;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities;

/// <summary>
/// Ein NOOSE-Nutzerkonto. Erweitert <see cref="IdentityUser"/> (String-GUID-Key) um die
/// fachlichen Felder: Dienstgrad, Status, TRU-Flag und Discord-Bezug. Die Anmeldung erfolgt
/// ausschließlich über Discord-OAuth; ein Passwort wird nie gesetzt.
/// </summary>
public class Agent : IdentityUser
{
    /// <summary>Anzeigename im System (initial der Discord-Global-Name bzw. -Username).</summary>
    public string Anzeigename { get; set; } = string.Empty;

    /// <summary>Numerische Discord-Benutzer-ID (stabiler externer Schlüssel, eindeutig).</summary>
    public string DiscordId { get; set; } = string.Empty;

    /// <summary>Discord-Benutzername (z. B. "name" bzw. altes "name#1234"), rein informativ.</summary>
    public string? DiscordUsername { get; set; }

    /// <summary>URL zum Discord-Avatar (optional, für die Anzeige).</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>NOOSE-Dienstgrad. Null, solange der Account noch nicht freigegeben wurde.</summary>
    public Dienstgrad? Dienstgrad { get; set; }

    /// <summary>Tactical Response Unit: rangübergreifendes Flag (jeder Rang kann rein).</summary>
    public bool IstTRU { get; set; }

    /// <summary>Technische Systemrolle (Auftraggeber). Unabhängig vom Dienstgrad.</summary>
    public bool IstAdmin { get; set; }

    /// <summary>Account-Lebenszyklus (Ausstehend/Aktiv/Gesperrt).</summary>
    public AgentStatus Status { get; set; } = AgentStatus.Ausstehend;

    public DateTime RegistriertAm { get; set; }
    public DateTime? FreigegebenAm { get; set; }
    public string? FreigegebenVonId { get; set; }

    /// <summary>Begründung der letzten Sperrung (für Audit/Anzeige).</summary>
    public string? GesperrtGrund { get; set; }

    /// <summary>Zeitpunkt des letzten Discord-Rollen-Syncs (vorbereitet, derzeit ungenutzt).</summary>
    public DateTime? DiscordRollenSyncAm { get; set; }
}
