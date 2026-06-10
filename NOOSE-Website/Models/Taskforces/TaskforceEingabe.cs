using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Taskforces;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Taskforce. Der Genehmigungs-Status ist
/// bewusst nicht enthalten – beim Anlegen stets <see cref="TaskforceStatus.Beantragt"/>, danach nur über den
/// Genehmigungs-Workflow (Führung) änderbar.</summary>
public class TaskforceEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Zweck { get; set; }
    public TaskforceGeltungsbereich Geltungsbereich { get; set; } = TaskforceGeltungsbereich.Innerbehoerdlich;
    public string? Bemerkungen { get; set; }
    public bool IstVerschlusssache { get; set; }
}
