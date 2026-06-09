using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein global verwaltetes Tag/Label (Stammdaten), das beliebigen Akten über eine
/// <see cref="TagZuordnung"/> zugeordnet werden kann. Bewusst <b>ohne</b> Soft-Delete: ein Tag ist
/// keine Akte, sondern ein Lookup-Wert – beim Löschen wird es hart entfernt (FK-Cascade räumt die
/// Zuordnungen ab), wodurch der eindeutige Index auf <see cref="Name"/> sauber bleibt.
/// </summary>
public class Tag : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Farbe (MudBlazor-Color-Name wie „Primary"/„Info") für die Chip-Darstellung.</summary>
    public string? Farbe { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
