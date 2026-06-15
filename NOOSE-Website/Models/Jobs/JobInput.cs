using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Jobs;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Aufgabe/To-Do.</summary>
public class JobInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Open;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public DateTime? DueDate { get; set; }

    /// <summary>Eingeschränkt: nur zugeteilte Agenten, der Ersteller und die Aufsicht (Führung/Admin/Teamleitung) sehen die Aufgabe.</summary>
    public bool IsRestricted { get; set; }
}
