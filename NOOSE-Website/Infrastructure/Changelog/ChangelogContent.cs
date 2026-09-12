using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Infrastructure.Changelog;

/// <summary>The changelog as it ships with the code; the seeder writes it into the database.</summary>
/// <remarks>
/// Written for the person using the site, not for the person who built it. Three rules held here:
/// no technology in a line (no table, no service, no framework), one short sentence per line, and nothing at all for
/// a change nobody outside the repository can notice - renames, tests, documentation and refactors are absent on
/// purpose rather than summarised.
/// <para>
/// A version is authored here, not read from the build counter: BuildNumber.txt is gitignored and grows on every
/// build, so it is unknown while these lines are written and was never recorded for the past at all. The build number
/// is stamped later by the seeder, onto the newest release that has none.
/// </para>
/// <para>
/// Raise <see cref="Revision"/> when an existing line is reworded. The seeder then rewrites rows that nobody has
/// edited and leaves edited ones alone; a new line needs no bump because it is recognised by its missing key.
/// </para>
/// </remarks>
public static class ChangelogContent
{
    /// <summary>Revision of the shipped text. Raise it after rewording an existing line.</summary>
    public const int Revision = 1;

    /// <param name="Key">Stable handle; renaming one orphans the old row and creates a second.</param>
    public sealed record SeededEntry(string Key, ChangelogKind Kind, string Title, string? Area);

    public sealed record SeededRelease(string Version, DateTime Date, string Title, SeededEntry[] Entries);

    private static SeededEntry Neu(string key, string title, string area)
        => new(key, ChangelogKind.Neu, title, area);

    private static SeededEntry Besser(string key, string title, string area)
        => new(key, ChangelogKind.Verbessert, title, area);

    private static SeededEntry Fix(string key, string title, string area)
        => new(key, ChangelogKind.Behoben, title, area);

    public static readonly IReadOnlyList<SeededRelease> Releases =
    [
        new("0.1", new DateTime(2026, 6, 12), "Der Anfang: Akten, Suche und Lagezentrum",
        [
            Neu("0.1-personenakten", "Personenakten mit Steckbrief, Fotos und Protokollen.", "Personen"),
            Neu("0.1-gruppierungen", "Fraktionen, Parteien und Personengruppen als eigene Akten.", "Akten"),
            Neu("0.1-vorgaenge", "Vorgänge und Operationen bündeln zusammengehörige Ermittlungen.", "Ermittlung"),
            Neu("0.1-taskforces", "Taskforces mit eigenem Chat.", "Taskforces"),
            Neu("0.1-dashboard", "Lagezentrum mit den wichtigsten Zahlen auf einen Blick.", "Lagezentrum"),
            Neu("0.1-suche", "Suche über den gesamten Aktenbestand.", "Suche"),
            Neu("0.1-aufgaben", "Aufgaben und Schwarzes Brett für den Dienstbetrieb.", "Dienstbetrieb"),
            Neu("0.1-kalender", "Kalender für Termine der Behörde.", "Kalender"),
            Neu("0.1-druck", "Jede Akte lässt sich als Druckansicht ausgeben.", "Druck"),
            Besser("0.1-tabellen", "Alle Listen lassen sich sortieren, Datumsangaben sind einheitlich deutsch.", "Bedienung"),
        ]),

        new("0.2", new DateTime(2026, 6, 16), "Verschlusssachen, Ausbildung und Partnerbehörden",
        [
            Neu("0.2-vs", "Verschlusssachen: Dokumente nur für die Führung, für TRU oder für HRB.", "Dokumente"),
            Neu("0.2-ausbildung", "Ausbildungsmodule in der Personalakte.", "Personal"),
            Neu("0.2-gesetze", "Gesetzbuch zum Nachschlagen.", "Gesetze"),
            Neu("0.2-graph", "Beziehungsgraph zeigt, wer mit wem zu tun hat.", "Analyse"),
            Neu("0.2-score", "Bedrohungs-Score schätzt die Gefährdung je Person und Fraktion.", "Analyse"),
            Neu("0.2-partner", "Zugänge für LSPD, DoJ und LSMD, mit Freigabe je Akte.", "Partner"),
            Besser("0.2-fraktionsraenge", "Fraktionsränge lassen sich umbenennen und in der Reihenfolge ändern.", "Fraktionen"),
        ]),

        new("0.3", new DateTime(2026, 6, 26), "Bewerbungen, Demo-Modus und ein besserer Chat",
        [
            Neu("0.3-bewerbungen", "Bewerbungen werden auf der Seite eingereicht und bearbeitet.", "Bewerbungen"),
            Neu("0.3-demo", "Demo-Modus zum Vorführen der Seite ohne echte Daten.", "Betrieb"),
            Neu("0.3-dok-stichworte", "Dokumente lassen sich mit Stichworten versehen.", "Dokumente"),
            Besser("0.3-chat", "Der Taskforce-Chat wurde überarbeitet.", "Taskforces"),
            Besser("0.3-navigation", "Das Menü wurde aufgeräumt und reagiert schneller.", "Bedienung"),
            Fix("0.3-doks-loeschen", "Protokolle an einer Person lassen sich jetzt auch löschen.", "Personen"),
        ]),

        new("0.4", new DateTime(2026, 7, 3), "Bilder, Verknüpfungen und eigene Felder",
        [
            Neu("0.4-bilder", "Mehrere Bilder auf einmal hochladen, Vollbild-Ansicht per Klick.", "Bilder"),
            Neu("0.4-verknuepfungen", "Verknüpfungen zwischen Akten an einer Stelle gesammelt.", "Akten"),
            Neu("0.4-customfelder", "Eigene Felder je Aktentyp, von der Führung festgelegt.", "Konfiguration"),
            Neu("0.4-vorlagen", "Vorlagen für Dokumente und Protokolle.", "Vorlagen"),
            Neu("0.4-wartung", "Wartungsmodus und eigene Akzentfarben der Behörde.", "Betrieb"),
            Besser("0.4-partner-dokumente", "Partnerbehörden können eigene Dokumente anlegen und bearbeiten.", "Partner"),
            Besser("0.4-vs-eigene", "TRU und HRB dürfen ihre eigenen Verschlusssachen bearbeiten.", "Dokumente"),
        ]),

        new("0.5", new DateTime(2026, 7, 16), "Fahndung, Nachweis und Discord",
        [
            Neu("0.5-fahndungsbrett", "Fahndungsbrett mit allen ausgeschriebenen Personen.", "Fahndung"),
            Neu("0.5-nachweis", "Nachweis: wer hat wann was geändert und eingesehen.", "Nachweis"),
            Neu("0.5-discord", "Meldungen gehen automatisch in die passenden Discord-Kanäle.", "Discord"),
            Neu("0.5-ankuendigungen", "Ankündigungen mit Textgestaltung, Frist und Lesebestätigung.", "Brett"),
            Neu("0.5-anhaenge", "Anhänge lassen sich direkt in der Akte ansehen.", "Anhänge"),
            Besser("0.5-mitglieder", "Mitglieder einer Fraktion lassen sich in einem Rutsch pflegen.", "Fraktionen"),
            Besser("0.5-suche-relevanz", "Die Suche zeigt Passenderes zuerst.", "Suche"),
        ]),

        new("0.6", new DateTime(2026, 7, 28), "Besprechungen, Abmeldungen und Kündigungen",
        [
            Neu("0.6-besprechungen", "Besprechungen mit Tagesordnung und Protokoll.", "Besprechungen"),
            Neu("0.6-abmeldungen", "Abmeldungen zeigen, wer wann nicht erreichbar ist.", "Abmeldungen"),
            Neu("0.6-kuendigung", "Kündigungen laufen als eigener, nachvollziehbarer Ablauf.", "Personal"),
            Neu("0.6-vermerke", "Vermerke in der Personalakte, mit Vorlagen.", "Personal"),
            Neu("0.6-erwaehnungen", "Wer in einem Text erwähnt wird, bekommt eine Benachrichtigung.", "Benachrichtigungen"),
            Besser("0.6-bewerbung-status", "Der Status einer Bewerbung lässt sich nachträglich ändern.", "Bewerbungen"),
        ]),

        new("0.7", new DateTime(2026, 8, 2), "Neues Erscheinungsbild",
        [
            Besser("0.7-redesign", "Die Seite hat ein neues Erscheinungsbild bekommen.", "Bedienung"),
            Neu("0.7-bilder-text", "Bilder lassen sich direkt in Texte einfügen.", "Texte"),
            Neu("0.7-wertelisten", "Auswahllisten wie Status und Kategorien sind selbst pflegbar.", "Konfiguration"),
        ]),

        new("0.8", new DateTime(2026, 8, 8), "Asservatenkammer, Kasse und NOOSEI",
        [
            Neu("0.8-asservate", "Asservatenkammer für Beweismittel, mit Ein- und Auslagerung.", "Asservate"),
            Neu("0.8-kasse", "Kasse der Behörde mit Buchungen und Belegen.", "Kasse"),
            Neu("0.8-finanzierung", "Finanzierungsanträge mit Genehmigungsweg.", "Finanzierung"),
            Neu("0.8-entfuehrungen", "Entführungen als eigene Akte.", "Ermittlung"),
            Neu("0.8-noosei", "NOOSEI beantwortet Fragen zum Aktenbestand.", "NOOSEI"),
            Neu("0.8-bestenliste", "Bestenliste der Agenten.", "Bestenliste"),
            Neu("0.8-feedback", "Feedback-Seite für Rückmeldungen zur Website.", "Feedback"),
            Besser("0.8-kasse-offen", "Kasse und Asservatenkammer stehen allen Agenten offen.", "Berechtigungen"),
            Besser("0.8-statistik", "Die Statistik wurde um viele Auswertungen erweitert.", "Statistik"),
        ]),

        new("0.9", new DateTime(2026, 8, 12), "Die neue Suche und @-Erwähnungen",
        [
            Besser("0.9-suche-neu", "Die Suche wurde neu gebaut und findet jetzt auch bei Tippfehlern.", "Suche"),
            Neu("0.9-mentions", "@-Erwähnungen in allen Textfeldern, mit Sprung zur Akte.", "Texte"),
            Neu("0.9-informanten", "Informanten als eigene Akte.", "Ermittlung"),
            Neu("0.9-chronik", "Chronik als Tages-Feed statt langer Liste.", "Chronik"),
            Besser("0.9-nachweis-filter", "Im Nachweis lassen sich auch Teamleitungen und Partner filtern.", "Nachweis"),
        ]),

        new("1.0", new DateTime(2026, 8, 25), "Der öffentliche Bereich geht online",
        [
            Neu("1.0-startseite", "Öffentliche Startseite mit Karriere-Bereich.", "Öffentlich"),
            Neu("1.0-gesucht", "Öffentliche Fahndung: Bürger sehen gesuchte Personen.", "Fahndung"),
            Neu("1.0-kopfgeld", "Kopfgeld auf ausgeschriebene Personen.", "Fahndung"),
            Neu("1.0-buergerkonto", "Bürgerkonten mit eigenem Bereich.", "Bürger"),
            Neu("1.0-hinweise", "Bürger können Hinweise einreichen.", "Hinweise"),
            Neu("1.0-tickets", "Tickets: Bürger schreiben der Führung.", "Tickets"),
            Neu("1.0-presse", "Presse, Warnungen und Lageberichte für die Öffentlichkeit.", "Öffentlich"),
            Neu("1.0-gefahrenlage", "Gefahrenlage-Ampel auf der öffentlichen Seite.", "Öffentlich"),
            Neu("1.0-oeff-suche", "Öffentliche Suche über die freigegebenen Inhalte.", "Öffentlich"),
            Neu("1.0-organisationen", "Organisationsprofile bekannter Gruppierungen.", "Öffentlich"),
        ]),

        new("1.1", new DateTime(2026, 9, 2), "Belohnungen, Einspruch und ein gehärteter Außenbereich",
        [
            Neu("1.1-belohnung", "Belohnungen für nützliche Hinweise, mit Beleg.", "Hinweise"),
            Neu("1.1-einspruch", "Einspruch gegen eine Ausschreibung.", "Fahndung"),
            Neu("1.1-galerien", "Fotogalerien für Personengruppen und Parteien.", "Akten"),
            Besser("1.1-buerger-nav", "Der Bürgerbereich hat eine eigene Navigation bekommen.", "Bürger"),
            Fix("1.1-kpi", "Die Zahlen auf den öffentlichen Seiten stimmen jetzt.", "Öffentlich"),
            Fix("1.1-anmeldung", "Die Anmeldeseite lässt sich nicht mehr von außen blockieren.", "Anmeldung"),
            Fix("1.1-gesperrt", "Gesperrte Bürger bekommen eine ehrliche Auskunft statt einer leeren Seite.", "Bürger"),
            Fix("1.1-module-aus", "Abgeschaltete Bereiche antworten sauber, statt halb zu laden.", "Öffentlich"),
        ]),

        new("1.2", new DateTime(2026, 9, 4), "Ergreifungsmeldung, FAQ und Eignungstests mit Zeitlimit",
        [
            Neu("1.2-organigramm", "Öffentliches Organigramm der Führung.", "Öffentlich"),
            Neu("1.2-ergreifung", "Ergreifungsmeldung: Bürger melden, dass sie jemanden gestellt haben.", "Fahndung"),
            Neu("1.2-faq", "Häufige Fragen auf einer eigenen Seite.", "Öffentlich"),
            Neu("1.2-test-zeit", "Eignungstests mit Bearbeitungszeit.", "Bewerbungen"),
            Neu("1.2-ticket-beteiligte", "An einem Ticket beteiligte Agenten und ein interner Nebenstrang.", "Tickets"),
            Neu("1.2-buerger-name", "Bürger vergeben ihren Namen einmal selbst.", "Bürger"),
            Besser("1.2-nachricht-bearbeiten", "Bürger können ihre Nachricht nachträglich bearbeiten.", "Bürger"),
            Besser("1.2-gefahr-manuell", "Die Gefahrenlage lässt sich von Hand setzen.", "Öffentlich"),
        ]),

        new("1.3", new DateTime(2026, 9, 10), "Feinschliff",
        [
            Besser("1.3-kopfzeile", "Die Kopfzeile wurde aufgeräumt.", "Bedienung"),
            Besser("1.3-betraege", "Die Übersicht zeigt die Beträge mit an.", "Kasse"),
            Fix("1.3-belohnung-gebucht", "Eine Belohnung gilt erst als ausgezahlt, wenn sie gebucht ist.", "Hinweise"),
            Fix("1.3-test-antworten", "Antworten auf einen Test werden nicht mehr mit veralteten Angaben gespeichert.", "Bewerbungen"),
        ]),
    ];
}
