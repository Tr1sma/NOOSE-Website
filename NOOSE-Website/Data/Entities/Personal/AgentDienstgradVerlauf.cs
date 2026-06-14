using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personal;

/// <summary>
/// Ein Eintrag im Dienstgrad-Verlauf eines Agents (Phase 5e, append-only Historie – wie
/// <c>EinstufungVerlauf</c>, daher ohne Soft-Delete). Wird bei jeder Rangänderung geschrieben
/// (Freigabe, manuelle Rangänderung, genehmigte Beförderung). <see cref="AkteurName"/> ist der Codename
/// des Handelnden zum Zeitpunkt (denormalisiert).
/// </summary>
[Table("AgentDienstgradVerlaeufe")]
public class AgentDienstgradVerlauf
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    /// <summary>Vorheriger Dienstgrad (null bei erstmaliger Freigabe).</summary>
    public Dienstgrad? Alt { get; set; }

    /// <summary>Neuer Dienstgrad.</summary>
    [Column("Neu")]
    public Dienstgrad Neu { get; set; }

    [Column("Zeitpunkt")]
    public DateTime Zeitpunkt { get; set; }

    /// <summary>Codename des Handelnden (denormalisiert).</summary>
    [Column("AkteurName")]
    public string? AkteurName { get; set; }

    /// <summary>Grund/Anlass (z. B. „Erstmalige Freigabe", „Rangänderung", „Beförderung").</summary>
    [Column("Grund")]
    public string? Grund { get; set; }
}
