using Microsoft.AspNetCore.Identity;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities;

/// <summary>
/// Ein NOOSE-Nutzerkonto. Erweitert <see cref="IdentityUser"/> (String-GUID-Key) um die
/// fachlichen Felder: Dienstgrad, Status, TRU-/HRB-Flag und Discord-Bezug. Die Anmeldung erfolgt
/// ausschließlich über Discord-OAuth; ein Passwort wird nie gesetzt.
/// </summary>
public class Agent : IdentityUser
{
    /// <summary>Codename (Deckname). Wird ALLEN Nutzern überall angezeigt. Vom Admin vergeben; bei neuen Konten leer.</summary>
    public string Codename { get; set; } = string.Empty;

    /// <summary>Klarname (echter Name des Agenten). Nur für die Führungsebene (Supervisory+) und Admins sichtbar,
    /// nie für rangniedrigere Nutzer. Sichtbarkeit überall via <c>ClaimsPrincipal.DarfKlarnameSehen()</c> prüfen.</summary>
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

    /// <summary>Human Resources Branch: rangübergreifendes Flag (analog zur TRU; jeder Rang kann rein).</summary>
    public bool IstHRB { get; set; }

    /// <summary>Technische Systemrolle (Auftraggeber). Unabhängig vom Dienstgrad.</summary>
    public bool IstAdmin { get; set; }

    /// <summary>FiveM-Server-Teamleitung (Aufsicht). Reiner Sichtbarkeits-Marker: verleiht KEINE Rechte und greift
    /// NICHT in die Dienstgrad-Hierarchie ein. Vollzugriff entsteht ausschließlich über den separat gesetzten
    /// <see cref="IstAdmin"/>-Haken. TeamLeitungen sind im gesamten RP-Betrieb unsichtbar (nirgends auswählbar,
    /// erwähnbar oder gelistet) und nur in der Agenten-Verwaltung sichtbar.</summary>
    public bool IstTeamLeitung { get; set; }

    /// <summary>Account-Lebenszyklus (Ausstehend/Aktiv/Gesperrt).</summary>
    public AgentStatus Status { get; set; } = AgentStatus.Ausstehend;

    public DateTime RegistriertAm { get; set; }
    public DateTime? FreigegebenAm { get; set; }
    public string? FreigegebenVonId { get; set; }

    /// <summary>Begründung der letzten Sperrung (für Audit/Anzeige).</summary>
    public string? GesperrtGrund { get; set; }

    /// <summary>Zeitpunkt des letzten Discord-Rollen-Syncs (vorbereitet, derzeit ungenutzt).</summary>
    public DateTime? DiscordRollenSyncAm { get; set; }

    // --- Ausstehende Selbst-Namensänderung -----------------------------------------------------
    // Ränge unterhalb Supervisory können ihre Stammdaten nicht direkt ändern; der gewünschte
    // Zielzustand wird hier als vollständiger Schnappschuss zwischengelagert, bis die Führung ihn
    // im Freigabe-Posteingang genehmigt (Werte werden übernommen) oder ablehnt (Felder werden geleert).
    // Maßgeblich für „es liegt ein Antrag vor" ist allein NamensaenderungBeantragtAm (Klarname/
    // Dienstnummer dürfen auch im Antrag legitim null sein = Feld soll geleert werden).

    /// <summary>Beantragter neuer Codename (nur gültig, wenn <see cref="NamensaenderungBeantragtAm"/> gesetzt ist).</summary>
    public string? AusstehenderCodename { get; set; }

    /// <summary>Beantragter neuer Klarname (Schnappschuss; null = Feld soll geleert werden).</summary>
    public string? AusstehenderKlarname { get; set; }

    /// <summary>Beantragte neue Dienstnummer (Schnappschuss; null = Feld soll geleert werden).</summary>
    public string? AusstehendeDienstnummer { get; set; }

    /// <summary>Zeitpunkt des offenen Namensänderungs-Antrags. Null = kein offener Antrag.</summary>
    public DateTime? NamensaenderungBeantragtAm { get; set; }
}
