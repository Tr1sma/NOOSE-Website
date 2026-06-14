using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Taskforces;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Taskforce. Der Genehmigungs-Status ist
/// bewusst nicht enthalten – beim Anlegen stets <see cref="TaskforceStatus.Beantragt"/>, danach nur über den
/// Genehmigungs-Workflow (Führung) änderbar.</summary>
public class TaskforceInput
{
    public string Name { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public TaskforceScope Scope { get; set; } = TaskforceScope.InternalAgency;
    public string? Remarks { get; set; }
    public bool IsClassified { get; set; }
}
