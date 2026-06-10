using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Taskforces;

/// <summary>
/// Eine Chat-Nachricht im Team-Chat einer Taskforce (Phase 5d). <see cref="Text"/> enthält den Rohtext inklusive
/// inline-@-Verlinkungs-Tokens (<c>@{Typ:Id}</c>), die erst beim Anzeigen aufgelöst werden. <see cref="AutorName"/>
/// ist der Codename des Autors zum Sende-Zeitpunkt (denormalisiert, wie bei <c>Kommentar</c>). Sichtbarkeit erbt
/// von der Eltern-Taskforce (kein eigenes Verschlusssache-Flag). Voll auditiert und papierkorbfähig
/// (Soft-Delete = Nachricht „zurückgezogen").
/// </summary>
public class TaskforceNachricht : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TaskforceId { get; set; } = string.Empty;
    public Taskforce? Taskforce { get; set; }

    /// <summary>Rohtext der Nachricht inkl. <c>@{Typ:Id}</c>-Verlinkungs-Tokens.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Codename des Autors zum Sende-Zeitpunkt (denormalisiert).</summary>
    public string? AutorName { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
