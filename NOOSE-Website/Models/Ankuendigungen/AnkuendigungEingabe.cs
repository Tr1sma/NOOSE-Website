using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Ankuendigungen;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen (und Bearbeiten) einer Ankündigung. Die Broadcast-Felder
/// (<see cref="Zielgruppe"/> ≠ Alle, <see cref="AlsBroadcast"/>, <see cref="QuittierungVerlangt"/>) sind nur für
/// die Führung wirksam und werden ausschließlich beim Anlegen ausgewertet; beim Bearbeiten zählen nur
/// <see cref="Titel"/>/<see cref="Inhalt"/>/<see cref="Wichtig"/>.
/// </summary>
public class AnkuendigungEingabe
{
    public string Titel { get; set; } = string.Empty;
    public string? Inhalt { get; set; }
    public bool Wichtig { get; set; }

    public AnkuendigungZielgruppe Zielgruppe { get; set; } = AnkuendigungZielgruppe.AlleAktiven;
    /// <summary>Taskforce-Id bei <see cref="AnkuendigungZielgruppe.Taskforce"/>.</summary>
    public string? ZielId { get; set; }
    /// <summary>Mindest-Dienstgrad bei <see cref="AnkuendigungZielgruppe.AbDienstgrad"/>.</summary>
    public Dienstgrad? MinDienstgrad { get; set; }

    public bool AlsBroadcast { get; set; }
    public bool QuittierungVerlangt { get; set; }
}
