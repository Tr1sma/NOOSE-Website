using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personnel;

/// <summary>
/// Ein Ausbildungs-/Schulungsmodul (Katalog). Admins legen Module zentral an; sie können anschließend in der
/// Personalakte eines Agenten als abgeschlossen markiert werden (siehe <see cref="AgentModuleCompletion"/>).
/// Nur aktive Module erscheinen in der Personalakte zum Abhaken. Voll auditiert und papierkorbfähig
/// (Vorlage: <c>DocTemplate</c>).
/// </summary>
[Table("AusbildungsModule")]
public class TrainingModule : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Sprechender Name des Moduls, z. B. „Funkdisziplin – Grundlagen". Eindeutig.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Erläuterung – wird in der Verwaltung und in der Personalakte angezeigt.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Nur aktive Module erscheinen in der Personalakte zum Abschließen.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    /// <summary>Sortierreihenfolge in der Liste (kleiner zuerst).</summary>
    [Column("Sortierung")]
    public int Sorting { get; set; }

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
