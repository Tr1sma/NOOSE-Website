using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Graph;

/// <summary>
/// Ein Knoten im Beziehungsgraph – eine aufgelöste, für den Betrachter sichtbare Akte.
/// <see cref="Id"/> ist der stabile Graph-Schlüssel im Format <c>"Typ:EntitaetId"</c>
/// (z. B. <c>"Person:abc"</c>); so lassen sich Knoten verschiedener Aktentypen eindeutig adressieren.
/// </summary>
/// <param name="Id">Graph-Schlüssel "Typ:EntitaetId".</param>
/// <param name="Typ">CLR-Typname der Akte (<c>nameof(Person)</c> …) – steuert Farbe/Icon im Frontend.</param>
/// <param name="Bezeichnung">Anzeigename (z. B. Personenname/Fraktionsname).</param>
/// <param name="Untertitel">Optionaler Zusatz (i. d. R. das Aktenzeichen).</param>
/// <param name="Href">Navigationsziel zur Detailseite oder <c>null</c> (z. B. Agent).</param>
/// <param name="EinstufungStufe">Sicherheitseinstufung 0–3 (<see cref="Einstufung"/>) – speist die
/// „Lagebild"-Randfarbe; 0 für Aktentypen ohne Einstufung.</param>
/// <param name="IstVerschluss">Verschlusssache (nur der Führung überhaupt sichtbar) – für ein Badge.</param>
/// <param name="FotoUrl">Optionales Thumbnail (Personen) über den geschützten Foto-Endpoint.</param>
/// <param name="Grad">Knotengrad (Anzahl anliegender Kanten im aktuellen Graph) – steuert die Knotengröße.</param>
public record GraphKnoten(
    string Id,
    string Typ,
    string Bezeichnung,
    string? Untertitel,
    string? Href,
    int EinstufungStufe,
    bool IstVerschluss,
    string? FotoUrl,
    int Grad);

/// <summary>
/// Eine ungerichtet dargestellte Kante zwischen zwei <see cref="GraphKnoten"/>. Quelle ist entweder eine
/// generische <c>Verknuepfung</c> (mit ihrer <see cref="VerknuepfungArt"/>) oder eine Person-zu-Person-
/// Beziehung (deren Typ auf die Art gemappt wird: Feind→Konflikt, Verbündeter→Bündnis, sonst Standard).
/// </summary>
public record GraphKante(
    string Von,
    string Nach,
    string? Label,
    VerknuepfungArt Art,
    bool Automatisch);

/// <summary>
/// Vollständiges Graph-Ergebnis. <see cref="Abgeschnitten"/> ist <c>true</c>, wenn der Gesamtgraph mehr
/// Knoten enthielt als die Obergrenze und deshalb auf die wichtigsten (höchster Grad) reduziert wurde –
/// die UI weist darauf hin (kein stilles Abschneiden).
/// </summary>
public record GraphDaten(
    IReadOnlyList<GraphKnoten> Knoten,
    IReadOnlyList<GraphKante> Kanten,
    bool Abgeschnitten);

/// <summary>Parameter einer Graph-Abfrage.</summary>
/// <param name="FokusTyp">Aktentyp des Fokusknotens oder <c>null</c> für den Gesamtgraph.</param>
/// <param name="FokusId">Akten-Id des Fokusknotens oder <c>null</c> für den Gesamtgraph.</param>
/// <param name="Tiefe">Im Fokus-Modus: Anzahl der Hops um den Fokusknoten (1–3).</param>
/// <param name="TypFilter">Wenn gesetzt: nur Knoten dieser Aktentypen.</param>
/// <param name="ArtFilter">Wenn gesetzt: nur Kanten dieser Art (Standard/Konflikt/Bündnis).</param>
public record GraphAnfrage(
    string? FokusTyp = null,
    string? FokusId = null,
    int Tiefe = 1,
    IReadOnlyCollection<string>? TypFilter = null,
    VerknuepfungArt? ArtFilter = null);

/// <summary>
/// Ergebnis der Beziehungs-Pfadsuche zwischen zwei Akten. <see cref="Gefunden"/> ist <c>false</c>, wenn es
/// keine (sichtbare) Verbindung gibt. <see cref="Knoten"/> stehen in Pfadreihenfolge (Start … Ziel),
/// <see cref="Kanten"/> verbinden jeweils zwei aufeinanderfolgende Knoten.
/// </summary>
public record PfadErgebnis(
    bool Gefunden,
    IReadOnlyList<GraphKnoten> Knoten,
    IReadOnlyList<GraphKante> Kanten);

/// <summary>
/// Auswahl einer Akte in der Graph-Oberfläche (Fokus- bzw. Pfad-Endpunkt). Trägt den Aktentyp, die Id und
/// eine Anzeigebezeichnung – genug, um den Graph zu fokussieren oder die Pfadsuche zu starten.
/// </summary>
public record GraphAkteWahl(string Typ, string Id, string Bezeichnung);

/// <summary>
/// Ein automatisch erkannter Verknüpfungs-Vorschlag: eine Akte, die mit der betrachteten vermutlich
/// zusammenhängt (gleiche Telefonnummer/Fraktion/Gruppe/Tag oder gemeinsame Verknüpfung), aber noch
/// nicht verknüpft ist. <see cref="Grund"/> erklärt das „warum", <see cref="Staerke"/> = Anzahl der
/// zutreffenden Signale (für die Sortierung).
/// </summary>
public record VerknuepfungVorschlag(
    string ZielTyp,
    string ZielId,
    string Bezeichnung,
    string? Untertitel,
    string? Href,
    string Grund,
    int Staerke);
