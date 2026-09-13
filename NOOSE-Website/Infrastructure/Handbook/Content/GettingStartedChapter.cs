using Article = NOOSE_Website.Infrastructure.Handbook.HandbookContent.SeededArticle;
using Chapter = NOOSE_Website.Infrastructure.Handbook.HandbookContent.SeededChapter;
using Step = NOOSE_Website.Infrastructure.Handbook.HandbookContent.SeededStep;

namespace NOOSE_Website.Infrastructure.Handbook.Content;

/// <summary>Chapter one: arriving, finding your way around, opening the first record.</summary>
internal static class GettingStartedChapter
{
    internal static readonly Chapter Chapter = new(
        "kap-erste-schritte", "erste-schritte", "Erste Schritte",
        "Anmelden, zurechtfinden, das Menü einrichten und verstehen, wer was sehen darf.", "Start",
        [
            new Article("art-anmelden", "anmelden", "Anmelden und freigeschaltet werden",
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
                    new Step("Login", "Mit Discord anmelden",
                        "Auf der Startseite auf „Mit Discord anmelden“ klicken und in Discord bestätigen."),
                    new Step("HourglassTop", "Freigabe abwarten",
                        "Dein Konto ist erst „ausstehend“. Melde dich bei der Führung, damit dich jemand freigibt."),
                    new Step("Badge", "Dienstgrad erhalten",
                        "Mit der Freigabe bekommst du Dienstgrad und Kennzeichen. Erst dann siehst du die Akten."),
                ]),

            new Article("art-oberflaeche", "oberflaeche", "Aufbau der Oberfläche",
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

            new Article("art-suchen", "suchen", "Suchen und finden",
                "Die Suche über alles - und die Schnellsuche mit Strg+K.",
                """
                <p>Die Suche durchsucht den <strong>gesamten Bestand</strong>: Personen, Fraktionen, Vorgänge,
                Dokumente, Gesetze und mehr. Sie verzeiht Tippfehler und findet auch klangähnliche Namen -
                „Meier“ findet also auch „Mayer“.</p>
                <p>Für den schnellen Sprung brauchst du die Seite gar nicht: <strong>Strg+K</strong> öffnet
                überall ein Suchfeld. Tippen, mit den Pfeiltasten auswählen, Enter.</p>
                <p>Findest du nichts, liegt es oft nicht am Suchbegriff, sondern an den Rechten: Akten, die du
                nicht sehen darfst, tauchen in den Treffern gar nicht erst auf.</p>
                """,
                NavKey: "suche",
                Steps:
                [
                    new Step("Search", "Strg+K drücken",
                        "Funktioniert auf jeder Seite, ohne die aktuelle Arbeit zu verlassen."),
                    new Step("Keyboard", "Tippen statt klicken",
                        "Schon nach wenigen Buchstaben erscheinen Treffer. Pfeiltasten wählen aus, Enter öffnet."),
                    new Step("FilterAlt", "Auf einen Typ eingrenzen",
                        "Auf der Suchseite grenzt die Leiste über den Treffern auf Personen, Fraktionen und so weiter ein."),
                ]),

            new Article("art-menue-anpassen", "menue-anpassen", "Das Menü anpassen",
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
                    new Step("Tune", "Menü anpassen öffnen",
                        "Das Zahnrad unten in der Menüleiste öffnet die Einstellungen der Navigation."),
                    new Step("VisibilityOff", "Unnötiges ausblenden",
                        "Ausgeblendete Einträge sind nicht gesperrt - du erreichst sie weiter über die Suche."),
                    new Step("PushPin", "Favoriten anheften",
                        "Das Stecknadel-Symbol an einer Akte oder Seite heftet sie oben an die Menüleiste."),
                ]),

            new Article("art-profil", "profil", "Dein Profil",
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

            new Article("art-rechte", "wer-darf-was", "Wer darf was",
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

            new Article("art-hilfe-finden", "hilfe-finden", "Wo du Hilfe findest",
                "Handbuch, Glossar, Neuerungen und Feedback - was wofür da ist.",
                """
                <p>Dieses <strong>Handbuch</strong> erklärt die Bedienung. Oben steht ein Suchfeld, das
                gleichzeitig Artikel und Glossarbegriffe durchsucht - schneller als sich durch die Kapitel zu
                klicken.</p>
                <p>Das <strong>Glossar</strong> am Ende des Handbuchs erklärt jedes Fachwort in einem Satz.
                Wenn dir irgendwo ein Begriff begegnet, den du nicht kennst, schlag ihn dort nach, bevor du
                rätst.</p>
                <p>Im Lagezentrum steht für den Anfang eine Karte <strong>Erste Schritte</strong> mit sechs
                Punkten. Die haken sich von selbst ab, sobald du die jeweilige Sache einmal gemacht hast -
                niemand muss dich freigeben, und wenn alle sechs erledigt sind, verschwindet die Karte.</p>
                <p>Unter <em>Neuerungen</em> steht in Alltagssprache, was sich zuletzt geändert hat. Und wenn
                etwas nicht funktioniert oder dir fehlt: <em>Feedback</em> im Bereich <em>Mein Dienst</em>.
                Das landet direkt bei denen, die die Seite pflegen.</p>
                """,
                NavKey: "handbuch"),
        ]);
}
