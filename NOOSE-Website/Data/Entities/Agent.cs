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
    /// <summary>Codename (Deckname). Wird ALLEN Nutzern überall angezeigt. Vom Admin vergeben; bei neuen Konten leer.</summary>
    public string Codename { get; set; } = string.Empty;

    /// <summary>Klarname (echter Name des Agenten). NUR für Admins sichtbar, nie für normale Nutzer.</summary>
    public string? Klarname { get; set; }

    /// <summary>Dienstnummer (alphanumerische Dienstkennung, Freitext). Für alle Nutzer sichtbar.</summary>
    public string? Dienstnummer { get; set; }

    /// <summary>Numerische Discord-Benutzer-ID (stabiler externer Schlüssel, eindeutig). Rein intern.</summary>
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
