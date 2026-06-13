using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Bedrohung;

/// <summary>Eine datierte Fraktions-Aktivität, reduziert auf das für den Score Nötige (Art + Zeitpunkt).</summary>
public sealed record BedrohungsAktivitaet(string? Art, DateTime Zeitpunkt);

/// <summary>Ein Maßnahmen-Dok eines Mitglieds, reduziert auf das für den Score Nötige (Ausgang + Zeitpunkt).</summary>
public sealed record BedrohungsDok(MassnahmeAusgang Ausgang, DateTime Zeitpunkt);

/// <summary>
/// Reine, EF-freie Eingabe für die Score-Berechnung einer Fraktion. Der <c>BedrohungsScoreService</c> lädt die
/// Rohdaten flach aus der DB und befüllt dieses Objekt; <see cref="BedrohungsScoreService.Berechne"/> ist damit
/// eine pure Funktion (deterministisch, ohne DB) und gegen das durchgerechnete „Vagos"-Beispiel verifizierbar.
/// </summary>
public sealed class BedrohungsScoreEingabe
{
    public bool IstStaatsfraktion { get; init; }
    public Einstufung Einstufung { get; init; }

    public int? GeschaetzteMitgliederzahl { get; init; }
    public int AktiveMitgliederCount { get; init; }
    public bool HatAktiveLeitung { get; init; }
    public int RaengeCount { get; init; }
    public bool HatAnwesen { get; init; }

    /// <summary>Distinkte, nicht-leere Bezeichnungen im Waffenbestand.</summary>
    public int DistinctWaffenCount { get; init; }
    /// <summary>Distinkte, nicht-leere Bezeichnungen im Lagerbestand.</summary>
    public int LagerbestandCount { get; init; }
    /// <summary>Distinkte, nicht-leere Bezeichnungen der Drogenrouten.</summary>
    public int DrogenroutenCount { get; init; }

    public IReadOnlyList<BedrohungsAktivitaet> Aktivitaeten { get; init; } = [];

    /// <summary>
    /// Maßnahmen-Doks gebündelt je Mitgliedschaft (austritts-stabil &amp; auf die Mitgliedschaftsdauer
    /// begrenzt): eine innere Liste je Mitgliedschafts-Periode mit den Doks, deren Zeitpunkt in
    /// [Beitritt … Austritt] fällt. Der Pro-Mitglied-Cap wird je innerer Liste angewandt.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<BedrohungsDok>> DoksProMitglied { get; init; } = [];

    public int KonfliktCount { get; init; }
    public int BuendnisCount { get; init; }

    /// <summary>Anzahl manueller, nicht-automatischer <em>Standard</em>-Verknüpfungen inzident zur Fraktion
    /// (S4 Netzwerk-Zentralität). Disjunkt zu Konflikt/Bündnis (die in S3 zählen).</summary>
    public int StandardKantenGrad { get; init; }

    /// <summary>Jüngster <em>Erfassungs</em>-Zeitstempel (ErstelltAm/GeaendertAm) über Fraktion + relevante
    /// Kind-Daten – nur für die Konfidenz-Frische, NICHT für die Höhe des Scores. <c>null</c> = nichts erfasst.</summary>
    public DateTime? JuengsteErfassungUtc { get; init; }
}

/// <summary>Eine Observation (Überwachung), reduziert auf das für den Score Nötige (Beginn + ggf. Ende).</summary>
public sealed record BedrohungsObservation(DateTime Beginn, DateTime? Ende);

/// <summary>
/// Reine, EF-freie Eingabe für die PERSON-Score-Berechnung. Analog <see cref="BedrohungsScoreEingabe"/>; nutzt
/// ausschließlich person-eigene Daten (keine Fraktion-Live-Importe → keine Zirkularität).
/// </summary>
public sealed class PersonBedrohungsScoreEingabe
{
    public Einstufung Einstufung { get; init; }
    public Lebensstatus Lebensstatus { get; init; }
    public DateTime? TotBis { get; init; }

    /// <summary>Maßnahmen-Doks der Person (P1). Wiederverwendung von <see cref="BedrohungsDok"/>.</summary>
    public IReadOnlyList<BedrohungsDok> Doks { get; init; } = [];
    /// <summary>Distinkte, nicht-leere Waffen-Beschreibungen (P2).</summary>
    public int DistinctWaffenCount { get; init; }
    /// <summary>Observationen/Überwachungen (P3).</summary>
    public IReadOnlyList<BedrohungsObservation> Observationen { get; init; } = [];

    // P4 – soziale Gefahr (typisierte PersonBeziehung + Leitungsrollen).
    public int FeindCount { get; init; }
    public int VerbuendeterCount { get; init; }
    public int GeschaeftspartnerCount { get; init; }
    /// <summary>Anzahl aktiver Mitgliedschaften MIT Leitungsrolle (Fraktion/Gruppe/Partei).</summary>
    public int LeitungsrollenCount { get; init; }

    /// <summary>Grad manueller Standard-Verknüpfungen inzident zur Person (P5).</summary>
    public int StandardKantenGrad { get; init; }

    // Nur für die Konfidenz (senken den Score NIE):
    public int MitgliedschaftenCount { get; init; }
    /// <summary>Datenreichtum = Aliase + Fahrzeuge + Telefone + Orte (nur Konfidenz, nie Score).</summary>
    public int Datenreichtum { get; init; }
    public DateTime? JuengsteErfassungUtc { get; init; }
}

/// <summary>Beitrag eines einzelnen Teilscores – für die nachvollziehbare Aufschlüsselung in der UI.</summary>
public sealed record BedrohungsTeilscore(string Name, double Rohwert, double Punkte, double Cap, IReadOnlyList<string> Treiber);

/// <summary>
/// Strukturierte Aufschlüsselung eines Score-Laufs. Wird als JSON in <c>Fraktion.BedrohungsDetailJson</c>
/// persistiert und beantwortet in der UI „warum dieser Score?" Zeile für Zeile.
/// </summary>
public sealed class BedrohungsScoreDetail
{
    public IReadOnlyList<BedrohungsTeilscore> Teilscores { get; init; } = [];
    /// <summary>Summe der Inhalts-Teilscores (0–100), vor der Einstufungs-Band-Projektion.</summary>
    public double Inhalt { get; init; }
    public string EinstufungName { get; init; } = "";
    /// <summary>Mindest-Band durch die Einstufung (0/12/50/75).</summary>
    public int Sockel { get; init; }
    public string BandHinweis { get; init; } = "";
    public int Score { get; init; }
    public int Konfidenz { get; init; }
    public bool TriageFlag { get; init; }
    public string? TriageHinweis { get; init; }
    /// <summary>Gesetzt (z. B. „Staatsfraktion"), wenn die Fraktion vom Score ausgenommen ist (Score = null).</summary>
    public string? Ausgenommen { get; init; }
    public DateTime BerechnetAmUtc { get; init; }
}

/// <summary>Ergebnis eines Score-Laufs: persistierte Werte (Score/Konfidenz = <c>null</c> bei Ausnahme) + Aufschlüsselung.</summary>
public sealed record BedrohungsScoreErgebnis(int? Score, int? Konfidenz, BedrohungsScoreDetail Detail);
