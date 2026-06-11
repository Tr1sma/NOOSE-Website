using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Konkreter Wert eines <see cref="CustomFeldDefinition"/> an einer bestimmten Akte. Die Zuordnung
/// erfolgt polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> (ohne FK-Navigation,
/// analog zu <c>Quelle</c>/<c>Kommentar</c>). Der Wert wird stets als String gehalten und je nach
/// <c>FeldTyp</c> der Definition interpretiert. Nur auditiert (kein Soft-Delete: Werte werden
/// beim Leeren entfernt bzw. mit der Akte mitgeführt).
/// </summary>
public class CustomFeldWert : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Id der zugehörigen <see cref="CustomFeldDefinition"/> (lose Verknüpfung ohne FK).</summary>
    public string CustomFeldDefinitionId { get; set; } = string.Empty;

    /// <summary>Aktentyp der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    public string EntitaetId { get; set; } = string.Empty;

    /// <summary>Wert als String (Zahl invariant, Datum ISO, Ja/Nein als true/false).</summary>
    public string? Wert { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
