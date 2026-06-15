using NOOSE_Website.Data.Entities.Personnel;

namespace NOOSE_Website.Models.Personnel;

/// <summary>Eingabemodell zum Anlegen/Bearbeiten eines Ausbildungsmoduls.</summary>
public class ModuleInput
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int Sorting { get; set; }
}

/// <summary>
/// Anzeigemodell für die Personalakte: ein Modul samt Abschluss-Status des betrachteten Agenten.
/// Ist <see cref="CompletionId"/> gesetzt, gilt das Modul als abgeschlossen.
/// </summary>
public record AgentModuleStatus(
    TrainingModule Module,
    string? CompletionId,
    DateTime? CompletedAt,
    string? CompleterName,
    string? Note)
{
    public bool IsCompleted => CompletionId is not null;
}
