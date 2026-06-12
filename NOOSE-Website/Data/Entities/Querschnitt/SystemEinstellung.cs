using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Generische Schlüssel/Wert-Systemeinstellung (Wartungsmodus, Ankündigungsbanner, Theme-Farben,
/// Logo-Datei). Eine Zeile je Schlüssel (Primärschlüssel <see cref="Schluessel"/>; Konstanten in
/// <c>SystemEinstellungKeys</c>). Fehlt eine Zeile, gilt der Code-Standard. Reine Konfiguration –
/// nur auditiert, kein Soft-Delete (analog <see cref="AktualitaetsSchwelle"/>).
/// </summary>
public class SystemEinstellung : IAuditable
{
    /// <summary>Einstellungs-Schlüssel, z. B. <c>WartungsmodusAktiv</c>.</summary>
    public string Schluessel { get; set; } = string.Empty;

    /// <summary>Wert als Text (bool als "true"/"false", Farben als Hex, Dateinamen roh).</summary>
    public string? Wert { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
