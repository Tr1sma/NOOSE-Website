namespace NOOSE_Website.Components.Pages.Rechtliches;

/// <summary>
/// Zentrale Quelle für die rechtlichen Pflichtangaben. Die Platzhalter unten – und nur hier –
/// durch die echten Werte ersetzen: Sowohl die Impressum-Verlinkung (Anmeldeseite + Footer) als
/// auch die Datenschutzseite lesen daraus, damit nichts doppelt gepflegt werden muss.
/// </summary>
public static class RechtlichesDaten
{
    /// <summary>
    /// Externes Impressum (Modern-Gaming-Netzwerk). Wird auf der Anmeldeseite, im Footer und auf
    /// der Datenschutzseite verlinkt. Bei Bedarf hier auf eine andere URL umstellen.
    /// </summary>
    public const string ImpressumUrl = "https://modern-gaming.net/legal-notice/";

    /// <summary>
    /// Verantwortliche Stelle i.S.d. Art. 4 Nr. 7 DSGVO. Vollständiger Name und Anschrift stehen im
    /// Impressum; hier den Namen eintragen, der auf der Datenschutzseite genannt werden soll.
    /// </summary>
    public const string Verantwortlicher = "David Dorn";

    /// <summary>Kontaktadresse für Datenschutz-Anfragen (Auskunft, Löschung usw.).</summary>
    public const string DatenschutzEmail = "tristan.atze@gmail.com";

    /// <summary>Stand der Datenschutzerklärung (bei inhaltlichen Änderungen aktualisieren).</summary>
    public const string Stand = "Juni 2026";
}
