using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Announcements;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen (und Bearbeiten) einer Ankündigung. Die Broadcast-Felder
/// (<see cref="Zielgruppe"/> ≠ Alle, <see cref="AlsBroadcast"/>, <see cref="QuittierungVerlangt"/>) sind nur für
/// die Führung wirksam und werden ausschließlich beim Anlegen ausgewertet; beim Bearbeiten zählen nur
/// <see cref="Titel"/>/<see cref="Inhalt"/>/<see cref="Wichtig"/>.
/// </summary>
public class AnnouncementInput
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public bool Important { get; set; }

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.AllActive;
    /// <summary>Taskforce-Id bei <see cref="AnkuendigungZielgruppe.Taskforce"/>.</summary>
    public string? TargetId { get; set; }
    /// <summary>Mindest-Dienstgrad bei <see cref="AnkuendigungZielgruppe.AbDienstgrad"/>.</summary>
    public Rank? MinRank { get; set; }

    public bool AsBroadcast { get; set; }
    public bool AcknowledgmentRequired { get; set; }
}
