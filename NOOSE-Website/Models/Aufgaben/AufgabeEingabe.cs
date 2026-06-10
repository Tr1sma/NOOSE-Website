using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Aufgaben;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Aufgabe/To-Do.</summary>
public class AufgabeEingabe
{
    public string Titel { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public AufgabeStatus Status { get; set; } = AufgabeStatus.Offen;
    public AufgabePrioritaet Prioritaet { get; set; } = AufgabePrioritaet.Normal;
    public DateTime? Faelligkeit { get; set; }
}
