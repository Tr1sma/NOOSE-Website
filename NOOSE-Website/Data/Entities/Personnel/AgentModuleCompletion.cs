using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personnel;

/// <summary>
/// Markiert ein <see cref="TrainingModule"/> in der Personalakte eines Agenten als abgeschlossen. Je Agent und
/// Modul existiert höchstens ein aktiver Eintrag (Eindeutigkeit prüft der Dienst – analog zu anderen
/// soft-delete-fähigen Akten ohne Unique-Index, damit ein erneutes Abschließen nach dem Entfernen möglich
/// bleibt). Anlegen/Entfernen ist der Führung vorbehalten. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("AgentModulAbschluesse")]
public class AgentModuleCompletion : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent, der das Modul abgeschlossen hat.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Abgeschlossenes Modul.</summary>
    [Column("ModulId")]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Zeitpunkt des Abschlusses (vorbelegt mit jetzt, durch die Führung editierbar gedacht).</summary>
    [Column("AbgeschlossenAm")]
    public DateTime CompletedAt { get; set; }

    /// <summary>Codename desjenigen, der den Abschluss eingetragen hat (denormalisiert).</summary>
    [Column("ErfasstVonName")]
    public string? CompleterName { get; set; }

    /// <summary>Optionale Notiz zum Abschluss (z. B. Bewertung, Datum der Schulung).</summary>
    [Column("Notiz")]
    public string? Note { get; set; }

    /// <summary>Navigationsziel auf das zugehörige Modul (für Anzeige-Joins).</summary>
    public TrainingModule? Module { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
