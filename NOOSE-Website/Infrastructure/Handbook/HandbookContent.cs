namespace NOOSE_Website.Infrastructure.Handbook;

/// <summary>The handbook as it ships with the code; the seeder writes it into the database.</summary>
/// <remarks>
/// Written for the agent using the site. Direct address, short sentences, the names of buttons in italics - and a
/// term is explained the first time it appears rather than assumed.
/// <para>
/// Two kinds of content are deliberately not prose: <c>DiagramKey</c> names a drawing that lives in
/// Components/Pages/Handbook/Diagrams, because the HTML filter allows no <c>svg</c> and would strip a real one on the
/// first save; and steps are rows, so a walkthrough keeps its shape no matter what an author does to the text around it.
/// </para>
/// <para>
/// Raise <see cref="Revision"/> when rewording an existing article. The seeder then rewrites rows nobody has edited
/// and leaves edited ones alone; a new article needs no bump because it is recognised by its missing key.
/// </para>
/// </remarks>
public static class HandbookContent
{
    /// <summary>Revision of the shipped text. Raise it after rewording an existing article, chapter or term.</summary>
    public const int Revision = 1;

    /// <param name="Key">Stable handle; renaming one orphans the old row and creates a second.</param>
    public sealed record SeededStep(string Icon, string Title, string Text);

    public sealed record SeededArticle(
        string Key,
        string Slug,
        string Title,
        string Summary,
        string ContentHtml,
        string? RoleplayHtml = null,
        string? DiagramKey = null,
        string? NavKey = null,
        SeededStep[]? Steps = null);

    public sealed record SeededChapter(
        string Key,
        string Slug,
        string Title,
        string Description,
        string Icon,
        SeededArticle[] Articles);

    public sealed record SeededTerm(
        string Key,
        string Term,
        string ShortDefinition,
        string? Synonyms = null,
        string? ExplanationHtml = null,
        string? ArticleKey = null);

    // ---------------------------------------------------------------------
    // Chapters. Only the first is written out; the rest are the shape of the
    // book, so the rail shows where the remaining material will go.
    // ---------------------------------------------------------------------

    public static readonly IReadOnlyList<SeededChapter> Chapters =
    [
        new("kap-erste-schritte", "erste-schritte", "Erste Schritte",
            "Anmelden, zurechtfinden, die erste Akte öffnen.", "Start",
        [
            new("art-anmelden", "anmelden", "Anmelden und freigeschaltet werden",
                "Wie du hereinkommst - und warum es beim ersten Mal nicht sofort weitergeht.",
                """
                <p>Die Anmeldung läuft über <strong>Discord</strong>. Ein eigenes Passwort gibt es nicht:
                du klickst auf <em>Mit Discord anmelden</em>, bestätigst dort einmal, und bist zurück auf der
                Seite.</p>
                <p>Danach ist dein Konto zunächst <strong>ausstehend</strong>. Das ist kein Fehler. Jemand aus
                der Führung muss dich erst freigeben und dabei deinen Dienstgrad setzen. Bis dahin siehst du
                nur eine Hinweisseite.</p>
                <p>Wenn dein Dienstgrad oder deine Kennzeichen später geändert werden, wirst du einmal
                abgemeldet. Melde dich einfach neu an - das ist so gewollt, damit deine neuen Rechte auch
                wirklich greifen.</p>
                """,
                RoleplayHtml:
                """
                <p>Dein Konto gehört deinem Discord-Konto, nicht deinem Namen. Wer die Behörde verlässt und
                später zurückkehrt, kommt mit demselben Konto wieder - die Personalakte ist dann noch da.</p>
                """,
                Steps:
                [
                    new("Login", "Mit Discord anmelden",
                        "Auf der Startseite auf „Mit Discord anmelden\" klicken und in Discord bestätigen."),
                    new("HourglassTop", "Freigabe abwarten",
                        "Dein Konto ist erst „ausstehend\". Melde dich bei der Führung, damit dich jemand freigibt."),
                    new("Badge", "Dienstgrad erhalten",
                        "Mit der Freigabe bekommst du Dienstgrad und Kennzeichen. Erst dann siehst du die Akten."),
                ]),

            new("art-oberflaeche", "oberflaeche", "Aufbau der Oberfläche",
                "Was wo liegt: Menüleiste, Bereiche, Kopfzeile, Glocke.",
                """
                <p>Links liegt eine schmale <strong>Symbolleiste</strong>. Jedes Symbol steht für einen
                Bereich - Akten, Ermittlung &amp; Wissen, Dienststelle, Mein Dienst, Verwaltung. Ein Klick
                darauf <em>wechselt nur die Liste daneben</em>; er springt nicht auf eine Seite.</p>
                <p>Daneben steht die eigentliche Liste der Seiten des gewählten Bereichs. Oben rechts findest
                du die Suche, die Glocke für Benachrichtigungen und dein Konto.</p>
                <p>Was du im Menü nicht siehst, darfst du meistens auch nicht öffnen: die Leiste zeigt nur,
                wofür deine Rechte reichen.</p>
                """,
                DiagramKey: "oberflaeche",
                NavKey: "dashboard"),

            new("art-suchen", "suchen", "Suchen und finden",
                "Die Suche über alles - und die Schnellsuche mit Strg+K.",
                """
                <p>Die Suche durchsucht den <strong>gesamten Bestand</strong>: Personen, Fraktionen, Vorgänge,
                Dokumente, Gesetze und mehr. Sie verzeiht Tippfehler und findet auch klangähnliche Namen -
                „Meier\" findet also auch „Mayer\".</p>
                <p>Für den schnellen Sprung brauchst du die Seite gar nicht: <strong>Strg+K</strong> öffnet
                überall ein Suchfeld. Tippen, mit den Pfeiltasten auswählen, Enter.</p>
                <p>Findest du nichts, liegt es oft nicht am Suchbegriff, sondern an den Rechten: Akten, die du
                nicht sehen darfst, tauchen in den Treffern gar nicht erst auf.</p>
                """,
                NavKey: "suche",
                Steps:
                [
                    new("Search", "Strg+K drücken",
                        "Funktioniert auf jeder Seite, ohne die aktuelle Arbeit zu verlassen."),
                    new("Keyboard", "Tippen statt klicken",
                        "Schon nach wenigen Buchstaben erscheinen Treffer. Pfeiltasten wählen aus, Enter öffnet."),
                    new("FilterAlt", "Auf einen Typ eingrenzen",
                        "Auf der Suchseite grenzt die Leiste über den Treffern auf Personen, Fraktionen und so weiter ein."),
                ]),

            new("art-menue-anpassen", "menue-anpassen", "Das Menü anpassen",
                "Favoriten setzen, Einträge ausblenden, Startseite wählen.",
                """
                <p>Du musst nicht mit dem Menü leben, wie es ist. Über das Zahnrad in der Menüleiste kannst du
                <strong>Einträge ausblenden</strong>, die du nie brauchst, und die Reihenfolge ändern.</p>
                <p>Seiten und einzelne Akten lassen sich als <strong>Favorit</strong> anheften; sie stehen
                danach ganz oben. Und du kannst festlegen, welche Seite nach dem Anmelden zuerst erscheint -
                nicht jeder fängt sinnvollerweise im Lagezentrum an.</p>
                """,
                Steps:
                [
                    new("Tune", "Menü anpassen öffnen",
                        "Das Zahnrad unten in der Menüleiste öffnet die Einstellungen der Navigation."),
                    new("VisibilityOff", "Unnötiges ausblenden",
                        "Ausgeblendete Einträge sind nicht gesperrt - du erreichst sie weiter über die Suche."),
                    new("PushPin", "Favoriten anheften",
                        "Das Stecknadel-Symbol an einer Akte oder Seite heftet sie oben an die Menüleiste."),
                ]),

            new("art-profil", "profil", "Dein Profil",
                "Codename, Foto und was andere von dir sehen.",
                """
                <p>Unter <em>Mein Profil</em> pflegst du deinen <strong>Codename</strong> und dein Foto. Der
                Codename ist der Name, unter dem dich alle anderen in der Behörde sehen - in Kommentaren, im
                Protokoll, in Auswahllisten.</p>
                <p>Dein <strong>Klarname</strong> ist etwas anderes. Ihn sieht nur die Führung. Das ist keine
                Kleinigkeit, sondern der Grund, warum die ganze Seite mit Codenamen arbeitet.</p>
                """,
                RoleplayHtml:
                """
                <p>Nach außen ist die NOOSE anonym. Auf den öffentlichen Seiten erscheint kein Agent mit Namen -
                die einzige Ausnahme sind Führungskräfte, die einzeln und von Hand für das öffentliche
                Organigramm freigegeben wurden.</p>
                """,
                NavKey: "profil"),

            new("art-rechte", "wer-darf-was", "Wer darf was",
                "Dienstgrad, Kennzeichen und warum beides zusammen zählt.",
                """
                <p>Deine Rechte ergeben sich aus <strong>zwei unabhängigen Dingen</strong>. Erstens dein
                <strong>Dienstgrad</strong> - von Junior Agent bis Director. Zweitens deine
                <strong>Kennzeichen</strong>: Admin, TRU, HRB. Ein Kennzeichen hängt nicht am Dienstgrad; ein
                Junior Agent kann HRB sein.</p>
                <p>Ab <strong>Supervisory Special Agent</strong> zählst du zur <strong>Führung</strong>. Das
                ist die Schwelle, ab der Verschlusssachen, Klarnamen und die meisten Entscheidungen sichtbar
                werden.</p>
                <p>Es gibt zusätzlich Konten, die <strong>alles lesen, aber nichts schreiben</strong> dürfen -
                die Aufsicht. Und Partnerkonten von LSPD, DoJ und LSMD, die nur einzeln freigegebene Akten
                sehen.</p>
                """,
                DiagramKey: "rechte-matrix"),
        ]),

        new("kap-akten", "akten-fuehren", "Akten führen",
            "Personen, Fraktionen, Einstufungen, Verknüpfungen und der Papierkorb.", "FolderShared", []),

        new("kap-ermitteln", "ermitteln", "Ermitteln",
            "Vorgänge, Operationen, Taskforces, Observationen und der Bedrohungs-Score.", "Radar", []),

        new("kap-fahndung", "fahndung-und-oeffentlichkeit", "Fahndung & Öffentlichkeit",
            "Ausschreibung, Kopfgeld, Bürgerhinweise, Tickets und Presse.", "PersonSearch", []),

        new("kap-dienstbetrieb", "dienstbetrieb", "Dienstbetrieb",
            "Aufgaben, Kalender, Besprechungen, Abmeldungen und das Schwarze Brett.", "EventNote", []),

        new("kap-personal", "personal-und-fuehrung", "Personal & Führung",
            "Personalakte, Beförderung, Ausbildung, Bewerbungen, Kasse.", "Groups", []),

        new("kap-werkzeuge", "werkzeuge", "Werkzeuge",
            "NOOSEI, Statistik, Nachweis, Druckansichten und Einstellungen.", "Handyman", []),
    ];

    // ---------------------------------------------------------------------
    // Glossary. The vocabulary the first chapter uses; the rest follows with
    // the chapters that need it.
    // ---------------------------------------------------------------------

    public static readonly IReadOnlyList<SeededTerm> Terms =
    [
        new("beg-agent", "Agent", "Ein Mitglied der NOOSE mit eigenem Konto auf dieser Seite.",
            ArticleKey: "art-rechte"),
        new("beg-codename", "Codename",
            "Der Name, unter dem dich alle in der Behörde sehen. Nicht dein Klarname.",
            Synonyms: "Deckname", ArticleKey: "art-profil"),
        new("beg-klarname", "Klarname",
            "Der echte Name einer Person. Innerhalb der Behörde sieht ihn nur die Führung.",
            Synonyms: "Realname", ArticleKey: "art-profil"),
        new("beg-dienstgrad", "Dienstgrad",
            "Deine Stufe von Junior Agent bis Director. Entscheidet zusammen mit den Kennzeichen über deine Rechte.",
            Synonyms: "Rang", ArticleKey: "art-rechte"),
        new("beg-fuehrung", "Führung",
            "Alle ab Supervisory Special Agent, dazu Admins. Die Schwelle, ab der Verschlusssachen sichtbar werden.",
            Synonyms: "Leadership", ArticleKey: "art-rechte"),
        new("beg-tru", "TRU",
            "Tactical Response Unit - ein Kennzeichen am Konto, unabhängig vom Dienstgrad.",
            Synonyms: "Tactical Response Unit", ArticleKey: "art-rechte"),
        new("beg-hrb", "HRB",
            "Human Resources Branch - das Kennzeichen für Personal und Bewerbungen, unabhängig vom Dienstgrad.",
            Synonyms: "Human Resources Branch", ArticleKey: "art-rechte"),
        new("beg-vs", "Verschlusssache",
            "Ein Inhalt, den nur die Führung sieht - oder nur TRU beziehungsweise nur HRB.",
            Synonyms: "VS, Verschlusssachen"),
        new("beg-aktenzeichen", "Aktenzeichen",
            "Die menschenlesbare Nummer einer Akte, etwa NOOSE-P-2026-0001. Wird automatisch vergeben."),
        new("beg-einstufung", "Einstufung",
            "Wie gefährlich eine Person oder Fraktion gilt: Prüffall, Verdachtsfall oder gesichert staatsgefährdend."),
        new("beg-lagezentrum", "Lagezentrum",
            "Die Übersichtsseite der Behörde mit den wichtigsten Zahlen und Gefährdungen.",
            Synonyms: "Dashboard", ArticleKey: "art-oberflaeche"),
        new("beg-aufsicht", "Nur-Lese-Aufsicht",
            "Ein Konto, das alles lesen, aber nichts speichern darf - und nie Klarnamen sieht.",
            Synonyms: "Aufsicht, Teamleitung", ArticleKey: "art-rechte"),
        new("beg-partner", "Partnerbehörde",
            "LSPD, DoJ oder LSMD. Sieht ausschließlich Akten, die einzeln für sie freigegeben wurden.",
            Synonyms: "LSPD, DoJ, LSMD, Partner"),
    ];
}
