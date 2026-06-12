using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine hochgeladene Datei der zentralen Datei-Bibliothek (Formulare, SOPs, Vorlagen – Phase 7).
/// Die Datei selbst liegt außerhalb von wwwroot (siehe <c>IBibliothekStorageService</c>) und wird
/// nur über den geschützten Endpoint <c>/dateien/bibliothek/{id}</c> ausgeliefert. Verschlusssachen
/// sind nur der Führung sichtbar. Voll auditiert und papierkorbfähig.
/// </summary>
public class BibliothekDatei : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Titel { get; set; } = string.Empty;

    /// <summary>Optionale Kategorie zur Gruppierung/Filterung (z. B. „Formular", „SOP").</summary>
    public string? Kategorie { get; set; }

    /// <summary>Ursprünglicher Dateiname des Uploads (für den Download-Dateinamen).</summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>Serverseitig vergebener Dateiname im Bibliotheks-Ordner (GUID + sichere Endung).</summary>
    public string DateinameGespeichert { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long GroesseBytes { get; set; }

    /// <summary>Verschlusssache: nur für die Führung sichtbar.</summary>
    public bool IstVerschlusssache { get; set; }

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
