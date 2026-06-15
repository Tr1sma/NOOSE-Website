using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Threat;

/// <summary>Eine datierte Fraktions-Aktivität, reduziert auf das für den Score Nötige (Art + Zeitpunkt).</summary>
public sealed record ThreatActivity(string? Kind, DateTime Timestamp);

/// <summary>Ein Maßnahmen-Dok eines Mitglieds, reduziert auf das für den Score Nötige (Ausgang + Zeitpunkt).</summary>
public sealed record ThreatDoc(MeasureOutcome Outcome, DateTime Timestamp);

/// <summary>
/// Reine, EF-freie Eingabe für die Score-Berechnung einer Fraktion. Der <c>BedrohungsScoreService</c> lädt die
/// Rohdaten flach aus der DB und befüllt dieses Objekt; <see cref="BedrohungsScoreService.Berechne"/> ist damit
/// eine pure Funktion (deterministisch, ohne DB) und gegen das durchgerechnete „Vagos"-Beispiel verifizierbar.
/// </summary>
public sealed class ThreatScoreInput
{
    public bool IsStateFaction { get; init; }
    public Classification Classification { get; init; }

    public int? EstimatedMemberCount { get; init; }
    public int ActiveMembersCount { get; init; }
    public bool HasActiveLead { get; init; }
    public int RanksCount { get; init; }
    public bool HasEstate { get; init; }

    /// <summary>Distinkte, nicht-leere Bezeichnungen im Waffenbestand.</summary>
    public int DistinctWeaponsCount { get; init; }
    /// <summary>Distinkte, nicht-leere Bezeichnungen im Lagerbestand.</summary>
    public int InventoryCount { get; init; }
    /// <summary>Distinkte, nicht-leere Bezeichnungen der Drogenrouten.</summary>
    public int DrugRoutesCount { get; init; }

    public IReadOnlyList<ThreatActivity> Activities { get; init; } = [];

    /// <summary>
    /// Maßnahmen-Doks gebündelt je Mitgliedschaft (austritts-stabil &amp; auf die Mitgliedschaftsdauer
    /// begrenzt): eine innere Liste je Mitgliedschafts-Periode mit den Doks, deren Zeitpunkt in
    /// [Beitritt … Austritt] fällt. Der Pro-Mitglied-Cap wird je innerer Liste angewandt.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<ThreatDoc>> DocsPerMember { get; init; } = [];

    public int ConflictCount { get; init; }
    public int AllianceCount { get; init; }

    /// <summary>Anzahl manueller, nicht-automatischer <em>Standard</em>-Verknüpfungen inzident zur Fraktion
    /// (S4 Netzwerk-Zentralität). Disjunkt zu Konflikt/Bündnis (die in S3 zählen).</summary>
    public int DefaultEdgesDegree { get; init; }

    /// <summary>Jüngster <em>Erfassungs</em>-Zeitstempel (ErstelltAm/GeaendertAm) über Fraktion + relevante
    /// Kind-Daten – nur für die Konfidenz-Frische, NICHT für die Höhe des Scores. <c>null</c> = nichts erfasst.</summary>
    public DateTime? LatestCaptureUtc { get; init; }
}

/// <summary>Eine Observation (Überwachung), reduziert auf das für den Score Nötige (Beginn + ggf. Ende).</summary>
public sealed record ThreatObservation(DateTime Start, DateTime? End);

/// <summary>
/// Reine, EF-freie Eingabe für die PERSON-Score-Berechnung. Analog <see cref="BedrohungsScoreEingabe"/>; nutzt
/// ausschließlich person-eigene Daten (keine Fraktion-Live-Importe → keine Zirkularität).
/// </summary>
public sealed class PersonThreatScoreInput
{
    public Classification Classification { get; init; }
    public LifeStatus LifeStatus { get; init; }
    public DateTime? DeadUntil { get; init; }

    /// <summary>Maßnahmen-Doks der Person (P1). Wiederverwendung von <see cref="BedrohungsDok"/>.</summary>
    public IReadOnlyList<ThreatDoc> Docs { get; init; } = [];
    /// <summary>Distinkte, nicht-leere Waffen-Beschreibungen (P2).</summary>
    public int DistinctWeaponsCount { get; init; }
    /// <summary>Observationen/Überwachungen (P3).</summary>
    public IReadOnlyList<ThreatObservation> Observations { get; init; } = [];

    // P4 – soziale Gefahr (typisierte PersonBeziehung + Leitungsrollen).
    public int EnemyCount { get; init; }
    public int AllyCount { get; init; }
    public int BusinessPartnerCount { get; init; }
    /// <summary>Anzahl aktiver Mitgliedschaften MIT Leitungsrolle (Fraktion/Gruppe/Partei).</summary>
    public int LeadershipRolesCount { get; init; }

    /// <summary>Grad manueller Standard-Verknüpfungen inzident zur Person (P5).</summary>
    public int DefaultEdgesDegree { get; init; }

    // Nur für die Konfidenz (senken den Score NIE):
    public int MembershipsCount { get; init; }
    /// <summary>Datenreichtum = Aliase + Fahrzeuge + Telefone + Orte (nur Konfidenz, nie Score).</summary>
    public int DataRichness { get; init; }
    public DateTime? LatestCaptureUtc { get; init; }
}

/// <summary>Beitrag eines einzelnen Teilscores – für die nachvollziehbare Aufschlüsselung in der UI.</summary>
public sealed record ThreatPartialScore(string Name, double RawValue, double Points, double Cap, IReadOnlyList<string> Driver);

/// <summary>
/// Strukturierte Aufschlüsselung eines Score-Laufs. Wird als JSON in <c>Fraktion.BedrohungsDetailJson</c>
/// persistiert und beantwortet in der UI „warum dieser Score?" Zeile für Zeile.
/// </summary>
public sealed class ThreatScoreDetail
{
    public IReadOnlyList<ThreatPartialScore> PartialScores { get; init; } = [];
    /// <summary>Summe der Inhalts-Teilscores (0–100), vor der Einstufungs-Band-Projektion.</summary>
    public double Content { get; init; }
    public string ClassificationName { get; init; } = "";
    /// <summary>Mindest-Band durch die Einstufung (0/12/50/75).</summary>
    public int Base { get; init; }
    public string BandHint { get; init; } = "";
    public int Score { get; init; }
    public int Confidence { get; init; }
    public bool TriageFlag { get; init; }
    public string? TriageHint { get; init; }
    /// <summary>Gesetzt (z. B. „Staatsfraktion"), wenn die Fraktion vom Score ausgenommen ist (Score = null).</summary>
    public string? Excluded { get; init; }
    public DateTime CalculatedAtUtc { get; init; }
}

/// <summary>Ergebnis eines Score-Laufs: persistierte Werte (Score/Konfidenz = <c>null</c> bei Ausnahme) + Aufschlüsselung.</summary>
public sealed record ThreatScoreResult(int? Score, int? Confidence, ThreatScoreDetail Detail);
