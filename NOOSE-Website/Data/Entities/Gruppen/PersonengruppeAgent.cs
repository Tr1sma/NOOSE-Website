using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Gruppen;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Personengruppe (wer bearbeitet die Gruppe). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Gruppe ist Cascade.
/// </summary>
public class PersonengruppeAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string PersonengruppeId { get; set; } = string.Empty;
    public Personengruppe? Personengruppe { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
