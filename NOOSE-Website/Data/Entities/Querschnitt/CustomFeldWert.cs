using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Konkreter Wert eines <see cref="CustomFeldDefinition"/> an einer bestimmten Akte. Die Zuordnung
/// erfolgt polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> (ohne FK-Navigation,
/// analog zu <c>Quelle</c>/<c>Kommentar</c>). Der Wert wird stets als String gehalten und je nach
/// <c>FeldTyp</c> der Definition interpretiert. Nur auditiert (kein Soft-Delete: Werte werden
/// beim Leeren entfernt bzw. mit der Akte mitgeführt).
/// </summary>
[Table("CustomFeldWerte")]
public class CustomFeldWert : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Id der zugehörigen <see cref="CustomFeldDefinition"/> (lose Verknüpfung ohne FK).</summary>
    [Column("CustomFeldDefinitionId")]
    public string CustomFeldDefinitionId { get; set; } = string.Empty;

    /// <summary>Aktentyp der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    [Column("EntitaetTyp")]
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    [Column("EntitaetId")]
    public string EntitaetId { get; set; } = string.Empty;

    /// <summary>Wert als String (Zahl invariant, Datum ISO, Ja/Nein als true/false).</summary>
    [Column("Wert")]
    public string? Wert { get; set; }

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
