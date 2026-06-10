using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Antraege;

/// <summary>
/// Ein Antrag im Posteingang-Workflow (Phase 5). Generisch über <see cref="AntragTyp"/> und ein
/// polymorphes Ziel (<see cref="ZielTyp"/> = CLR-Typname der Akte + <see cref="ZielId"/>). Aktuell genutzt
/// für die <see cref="AntragTyp.Hochstufung"/>: ein Agent unterhalb Senior Special Agent beantragt die
/// Einstufung „Gesichert staatsgefährdend" einer Akte; ein Senior Special Agent+ entscheidet im Posteingang.
/// Bei Genehmigung wird die Einstufung der Ziel-Akte gesetzt und im polymorphen Einstufungs-Verlauf mit
/// Antrags-Bezug (<c>EinstufungVerlauf.AntragId</c>) protokolliert.
/// </summary>
public class Antrag : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public AntragTyp Typ { get; set; } = AntragTyp.Hochstufung;

    /// <summary>CLR-Typname der Ziel-Akte (z. B. <c>nameof(Person)</c>, <c>nameof(Fraktion)</c>).</summary>
    public string ZielTyp { get; set; } = string.Empty;

    /// <summary>Id der Ziel-Akte.</summary>
    public string ZielId { get; set; } = string.Empty;

    /// <summary>Denormalisierte Anzeige der Ziel-Akte (Name + Aktenzeichen) für den Posteingang.</summary>
    public string ZielBezeichnung { get; set; } = string.Empty;

    /// <summary>Gewünschte Einstufung (für die Hochstufung stets „Gesichert staatsgefährdend").</summary>
    public Einstufung ZielEinstufung { get; set; }

    /// <summary>Begründung des Antragstellers.</summary>
    public string? Begruendung { get; set; }

    public AntragStatus Status { get; set; } = AntragStatus.Beantragt;

    /// <summary>Codename des Antragstellers (denormalisiert).</summary>
    public string? AntragstellerName { get; set; }

    /// <summary>Codename des Entscheiders (denormalisiert), null solange offen.</summary>
    public string? EntscheiderName { get; set; }

    public DateTime? EntschiedenAm { get; set; }

    /// <summary>Optionale Notiz/Begründung der Entscheidung.</summary>
    public string? Entscheidungsnotiz { get; set; }

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
