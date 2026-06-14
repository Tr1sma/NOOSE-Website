using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Persistierte, admin-einstellbare Konfiguration des Bedrohungs-Score-Algorithmus (Phase 8/Block D). Bewusst
/// EINE Singleton-Zeile (<see cref="Id"/> = „global") mit einem JSON-Feld, das eine
/// <c>BedrohungsScoreKonfiguration</c> trägt – so kommen neue Tuning-Parameter ohne Migration aus (fehlende
/// Felder fallen beim Deserialisieren auf den Code-Default zurück). Fehlt die Zeile, gilt komplett der Default.
/// <see cref="IAuditable"/> (kein Soft-Delete – die Konfiguration wird nie gelöscht, nur überschrieben).
/// </summary>
[Table("BedrohungsScoreKonfigs")]
public class BedrohungsScoreKonfig : IAuditable
{
    public const string GlobalId = "global";

    public string Id { get; set; } = GlobalId;

    /// <summary>Serialisierte <c>BedrohungsScoreKonfiguration</c> (JSON, longtext).</summary>
    public string? Json { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }
}
