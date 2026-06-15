using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Die <b>fixen</b> Anker des Bedrohungs-Score-Algorithmus „EHK-Score" – bewusst NICHT konfigurierbar, weil sie an
/// die festen <c>GefaehrdungsStufe</c>-Schwellen (25/50/75) gekoppelt sind: ein freies Verschieben würde die
/// Stufen-Semantik brechen. Alle übrigen (numerischen) Stellschrauben sind admin-einstellbar und liegen in
/// <see cref="BedrohungsScoreKonfiguration"/>.
/// </summary>
public static class ThreatScoreConstants
{
    /// <summary>Einstufungs-Sockel = garantiertes Mindest-Band (an die GefaehrdungsStufe-Schwellen gekoppelt):
    /// Verdachtsfall ⇒ ≥50 (mind. Hoch), GesichertStaatsgefährdend ⇒ ≥75 (immer Kritisch).</summary>
    public static int Base(Classification classification) => classification switch
    {
        Classification.SecuredStateThreatening => 75,
        Classification.SuspicionCase => 50,
        Classification.ReviewCase => 12,
        _ => 0, // Unbekannt: reiner Inhalt
    };
}
