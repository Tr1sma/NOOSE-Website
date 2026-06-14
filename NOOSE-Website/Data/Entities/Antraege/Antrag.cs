using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Antraege;

/// <summary>
/// Ein Antrag im Posteingang-Workflow (Phase 5). Generisch über <see cref="AntragTyp"/> und ein
/// polymorphes Ziel (<see cref="ZielTyp"/> = CLR-Typname der Akte + <see cref="ZielId"/>). Aktuell genutzt
/// für die <see cref="AntragTyp.Hochstufung"/>: ein Agent unterhalb Senior Special Agent beantragt die
/// Einstufung „Gesichert staatsgefährdend" einer Akte; ein Senior Special Agent+ entscheidet im Posteingang.
/// Bei Genehmigung wird die Einstufung der Ziel-Akte gesetzt und im polymorphen Einstufungs-Verlauf mit
/// Antrags-Bezug (<c>EinstufungVerlauf.AntragId</c>) protokolliert.
/// </summary>
[Table("Antraege")]
public class Antrag : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Typ")]
    public AntragTyp Typ { get; set; } = AntragTyp.Hochstufung;

    /// <summary>CLR-Typname der Ziel-Akte (z. B. <c>nameof(Person)</c>, <c>nameof(Fraktion)</c>).</summary>
    [Column("ZielTyp")]
    public string ZielTyp { get; set; } = string.Empty;

    /// <summary>Id der Ziel-Akte.</summary>
    [Column("ZielId")]
    public string ZielId { get; set; } = string.Empty;

    /// <summary>Denormalisierte Anzeige der Ziel-Akte (Name + Aktenzeichen) für den Posteingang.</summary>
    [Column("ZielBezeichnung")]
    public string ZielBezeichnung { get; set; } = string.Empty;

    /// <summary>Gewünschte Einstufung (für die Hochstufung stets „Gesichert staatsgefährdend").</summary>
    [Column("ZielEinstufung")]
    public Einstufung ZielEinstufung { get; set; }

    /// <summary>Begründung des Antragstellers.</summary>
    [Column("Begruendung")]
    public string? Begruendung { get; set; }

    public AntragStatus Status { get; set; } = AntragStatus.Beantragt;

    /// <summary>Codename des Antragstellers (denormalisiert).</summary>
    [Column("AntragstellerName")]
    public string? AntragstellerName { get; set; }

    /// <summary>Codename des Entscheiders (denormalisiert), null solange offen.</summary>
    [Column("EntscheiderName")]
    public string? EntscheiderName { get; set; }

    [Column("EntschiedenAm")]
    public DateTime? EntschiedenAm { get; set; }

    /// <summary>Optionale Notiz/Begründung der Entscheidung.</summary>
    [Column("Entscheidungsnotiz")]
    public string? Entscheidungsnotiz { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
