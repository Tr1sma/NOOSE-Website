using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Admin-definiertes Zusatzfeld („Custom-Feld") für einen Aktentyp – konfigurierbar ohne Code
/// (Plan.md Phase 7). Legt Name, Typ und Optionen fest; die konkreten Werte je Akte liegen in
/// <see cref="CustomFeldWert"/> (polymorph über <see cref="CustomFeldWert.EntitaetTyp"/> +
/// <see cref="CustomFeldWert.EntitaetId"/>). Voll auditiert und papierkorbfähig.
/// </summary>
public class CustomFeldDefinition : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Aktentyp, für den das Feld gilt, z. B. <c>nameof(Person)</c> (CLR-Typname).</summary>
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Feld-Bezeichnung (Label), z. B. „Decknamen-Quelle".</summary>
    public string Name { get; set; } = string.Empty;

    public CustomFeldTyp FeldTyp { get; set; }

    /// <summary>Auswahl-Optionen bei <see cref="CustomFeldTyp.Auswahl"/> – eine Option pro Zeile.</summary>
    public string? Optionen { get; set; }

    /// <summary>Pflichtfeld: beim Speichern der Werte muss ein Wert gesetzt sein.</summary>
    public bool Pflicht { get; set; }

    /// <summary>Sortierreihenfolge im Panel (kleiner zuerst).</summary>
    public int Reihenfolge { get; set; }

    /// <summary>Nur aktive Felder erscheinen im Zusatzfelder-Panel der Akten.</summary>
    public bool IstAktiv { get; set; } = true;

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
