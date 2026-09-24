# Etappe 12: Benachrichtigungs-Posteingang

Roadmap-Idee **#12** aus `IdeenBacklog.md` (Gruppe C, Aufwand klein, Jury-Schnitt 8,3). Gebaut wurde sie in
einem eigenen Worktree (`etappe-12-posteingang`), während Tristan #11 auf einem anderen Zweig gebaut hat.

## Entscheidungen (Tristan, 24.09.)

- **Filter nach einzelnen Arten.** Angeboten werden nur die Arten, die der Agent selbst schon bekommen hat, mit
  ihrer Anzahl. Es gibt keine neue Gruppierung der 36 Arten.
- **Kein Entfernen.** Es gibt nur „gelesen“ und „ungelesen“. Nichts verschwindet, und die 21. Meldung bleibt
  auffindbar.
- **Nur intern.** Die Seite trägt `Policies.InternalAgent`, und der Link in der Glocke erscheint nur bei
  `IsInternalAgent()`. Die Glocke hängt auch im öffentlichen Kopf, also bei Bürgern, Bewerbern und Partnern.
  Die sehen den Link nicht.

## Ausgangslage

`NotificationBell.razor` lud fest `GetOwnAsync(User, 20)`. Die 21. Meldung stand in der Datenbank, war in der
Oberfläche aber nicht mehr zu erreichen. Ein Klick markierte als gelesen und navigierte weg. Ein versehentlich
verlorener Merker ließ sich nicht zurückholen.

## Was gebaut ist

| Teil | Datei |
|---|---|
| Abfrage-, Seiten- und Filtermodell | `Models/Notifications/NotificationInbox.cs` |
| `GetInboxAsync`, `GetOwnTypesAsync`, `AsUnreadMarkAsync` | `Services/NotificationService.cs` (+ Interface) |
| Filter ↔ Adresse, lokale Tage → UTC-Fenster | `Services/NotificationInboxUrl.cs` |
| Seite `/benachrichtigungen` | `Components/Pages/Notifications/NotificationInbox.razor` |
| „Alle Benachrichtigungen“ unten in der Glocke | `Components/Layout/NotificationBell.razor` |
| Menüeintrag „Mein Dienst → Benachrichtigungen“ | `Navigation/NavCatalog.cs` |
| Handbuch: `art-benachrichtigungen` mit `NavKey` und drei Schritt-Karten, Revision 15 → 16 | `Infrastructure/Handbook/Content/DutyChapter.cs` |
| Changelog `2.2.11-posteingang` | `Infrastructure/Changelog/ChangelogContent.cs` |

- **Es braucht keine Migration.** Der Index `(RecipientId, ReadAt)` genügt, weil jede Abfrage auf einen einzigen
  Empfänger eingegrenzt ist. Die Schreibsperre nimmt `Notification` schon aus, also können auch die Nur-Lese-
  Aufsicht und der Demo-Besucher markieren.
- **So wird geblättert:**
  - Zuerst `CountAsync`, dann `Skip/Take`, 25 Einträge je Seite (erlaubt sind 10 bis 100).
  - Sortiert wird nach `CreatedAt` absteigend und bei gleichem Wert nach `Id`. Eine Sammelmeldung stempelt viele
    Zeilen gleich, und ohne diese zweite Stufe würden Einträge zwischen den Seiten springen.
  - Eine zu hohe Seitenzahl wird auf die letzte Seite gekappt.
- **Zeitraum:** „von“ und „bis“ sind lokale Kalendertage, und „bis“ schließt den ganzen Tag ein
  (`NotificationInboxUrl.ToUtcRange`, halboffenes UTC-Fenster). Liegt „von“ nach „bis“, werden die beiden
  getauscht. Ein von Hand getipptes `bis=9999-12-31` lässt das Ende offen, statt zu werfen.
- **Live:** Die Seite abonniert `NotificationBroadcaster.Received`, erst nach dem ersten interaktiven Render und nie
  im Prerender, und lädt die aktuelle Seite neu, wenn das eigene Signal kommt.
- **Gemerkte Ansichten:** `SavedViewButton` ist eingebunden. Die Seite hält die vier Zusagen aus
  `SavedViewPageScanTests`: `FilterState`, `ReadFilters`, `ApplyViewAsync` und den Generationszähler in `LoadAsync`.

## Abweichung vom Plan

- **Die Seitenzahl steht nicht in der Adresse.** Geplant war ein Parameter `seite`. Zwei Gründe sprechen dagegen:
  - Eine gemerkte Ansicht speichert genau die Paare aus `FilterState`, und eine Ansicht, die „Seite 3“ mitnimmt,
    öffnet morgen an einer anderen Stelle.
  - `QueryState.WriteAsync` baut die Adresse aus dem veralteten `Nav.Uri`. Ein zweiter, getrennter Schreibaufruf
    nur für die Seite würde deshalb die gerade geschriebenen Filter wieder überschreiben.

  Nach dem Zurückkommen aus einer Akte steht man daher wieder auf Seite 1, aber mit denselben Filtern.
  `NotificationInboxUrlTests.The_page_number_is_not_part_of_the_filter` hält das fest.

## Bekannt und gewollt: eine wieder ungelesene Meldung hält ihr Ziel offen

Je nach Art verhält sich eine ungelesene Meldung unterschiedlich:
- **Beobachtete Akten:** `WatchlistFanout` unterdrückt eine neue „Akte geändert“-Meldung, solange zur selben Akte
  eine ungelesen ist.
- **Tickets:** Sie laufen als einzige über `NotifyOnceAsync`/`NotifyManyOnceAsync`. `TryFoldAsync` legt eine neue
  Meldung derselben Art mit demselben `Href` auf die jüngste ungelesene und setzt deren `CreatedAt` auf jetzt.
- **Alle anderen Arten** (Erwähnungen, Aufgaben, Wiedervorlagen …) legen einfach eine weitere Zeile an.

Wer eine alte Meldung zu einer beobachteten Akte wieder als ungelesen markiert, bekommt zu dieser Akte also bis
zum Lesen keine neue Meldung. Die offene sagt schon, dass sich dort etwas getan hat, und die Akte selbst zeigt seit
Etappe 10, was neu ist. Ist die Meldung älter als die zwanzig neuesten, zählt sie in der Glocke mit, steht dort
aber nicht in der Liste. Im Posteingang findet man sie unter „Nur ungelesene“. So steht es im Handbuch.
`AsUnreadMarkAsync_ReopenedNotice_CollectsTheNextOneForItsTarget` hält die Ticket-Seite fest.

Die erste Fassung von Handbuch und Plan behauptete, jede Art sammle so. Der Test- und Doku-Prüfer hat das
widerlegt.

## Tests

- `NotificationServiceTests`, 14 neue Fälle:
  - nur eigene Meldungen, auch innerhalb des Zeitfensters
  - lückenloses Blättern bei gleichen Zeitstempeln
  - die Seitenkappung und die Kappung der Seitengröße
  - die Filter nach Art, Zeitfenster (beide Grenzen) und „nur ungelesen“
  - ein anonymer Aufrufer bekommt eine leere Seite
  - die Zählung der Arten
  - „wieder ungelesen“: eigen, fremd, schon ungelesen, anonym und der Sammeleffekt
- `NotificationInboxUrlTests`, 11 neue Fälle:
  - unbekannte oder numerische Arten fallen weg
  - beide Schreibweisen des Ungelesen-Schalters
  - ein kaputtes Datum ergibt keinen Tag
  - der Hin- und Rückweg über die Adresse
  - die Seite gehört nicht zum Filter
  - der letzte Tag zählt ganz
  - eine offene Seite bleibt offen
  - vertauschte Tage ergeben dasselbe Fenster
  - die Kalenderenden in einer getippten Adresse werfen nicht
- `SavedViewPageScanTests.The_six_lists_carry_the_button` nimmt die neue Seite in die Liste auf.

### Gegenprobe (jede Schranke einmal gebrochen)

| Sabotage | Rot wird |
|---|---|
| Empfänger-Filter in `GetInboxAsync` entfernt | `GetInboxAsync_ReturnsOnlyOwn_EvenInsideTheWindow` |
| zweite Sortierstufe `ThenByDescending(Id)` entfernt | `GetInboxAsync_PagesWithoutGapOrDouble_EvenOnEqualStamps` |
| Seitenkappung durch `Math.Max(page, 1)` ersetzt | `GetInboxAsync_ClampsAPageBeyondTheEnd_ToTheLastPage` |
| Eigentumsprüfung in `AsUnreadMarkAsync` entfernt | `AsUnreadMarkAsync_OthersNotification_IsNoOp` |
| Zahlen als Art zugelassen | `Unknown_and_numeric_types_drop_out` (erst nach dem Nachschärfen: die erste Fassung enthielt `Mention` schon als Namen, und die Zahl `1` fiel als Dublette weg) |
| „bis“ ohne den ganzen Tag | `The_last_day_counts_whole` |
| Tausch bei „von“ nach „bis“ entfernt | `Days_picked_the_wrong_way_round_still_give_the_window_between_them` |
| Schutz vor dem letzten Kalendertag entfernt | `The_ends_of_the_calendar_in_a_typed_address_do_not_throw` |

## Was der Review ergab

Vier Prüfer auf Opus 5.5 haben den Stand gelesen, je einer für Rechte und Eigentum, Blättern und Filter,
Oberfläche und Live-Aktualisierung sowie Tests und Doku. Keiner fand einen Fehler, der fremde Meldungen zeigt oder
verändert.

**Behoben:**
- **Tastatur:** Enter oder Leertaste auf dem Umschaltknopf erreichten auch die Zeile. `MudListItem` 9.5 hängt einen
  eigenen `onkeydown` an, und der öffnete das Ziel. Wer nur umschalten wollte, landete auf einer anderen Seite.
  Jetzt stoppt die Hülle des Knopfs auch `keydown`. Das muss im Browser geprüft werden, siehe Klickstrecke Schritt 8.
- **Doppeltes Laden:** Jede eigene Aktion lud doppelt, einmal im Handler und einmal über das eigene Signal, und
  zeigte dazwischen kurz den alten Stand. Jetzt läuft die Aktion über `WriteAsync`, und das Echo der eigenen
  Schreibaktion wird ignoriert.
- **Fehlermeldung eines veralteten Ladevorgangs:** Schlug ein Ladevorgang fehl, den ein neuerer schon ersetzt
  hatte, kam trotzdem eine Fehlermeldung. Jetzt meldet nur der aktuelle.
- **Scheitern beim ersten Laden:** Die Seite zeigte dann „Keine Benachrichtigungen“. Jetzt zeigt sie einen eigenen
  Fehlerzustand mit „Neu laden“.
- **Getippte Adressen:** `bis=9999-12-31` warf bei jedem Laden, und „von“ nach „bis“ ergab kommentarlos nichts.
- **Texte:** Handbuch, Plan und Dienst-Kommentar behaupteten, jede Art sammle auf die wieder ungelesene Meldung
  (siehe oben). Der Sammel-Test nutzte eine Erwähnung, obwohl Erwähnungen nie gesammelt werden, und läuft jetzt über
  eine Ticket-Meldung.

**Bewusst so gelassen:**
- **Demo-Besucher:** Sie teilen sich ein Konto und können Meldungen wieder auf ungelesen setzen, so wie sie über
  die Glocke schon als gelesen markieren konnten. Es wird kein Text geschrieben.
- **Verrutschen beim Blättern:** Eine Sammel-Meldung springt nach oben, und eine neue schiebt alles weiter. Wer
  gerade blättert, kann einen Eintrag doppelt sehen oder einen an der Seitengrenze verpassen. Das gehört zum
  seitenweisen Blättern.
- **Zahlen im Filter:** Die Zahl hinter einer Art zählt alle eigenen Meldungen dieser Art, nicht nur die im
  aktuellen Filter.
- **Zeitzone im Test:** `The_last_day_counts_whole` prüft die Zeitzonen-Umrechnung nur auf einem Rechner, der nicht
  auf UTC läuft. Den ganzen Tag prüft er überall.
- **Keine Tests für die Meldungen zu beobachteten Akten:** Für `WatchlistFanout` gibt es keine. Dass eine wieder
  ungelesene Meldung dort neue unterdrückt, steht deshalb nur in diesem Dokument und im Handbuch.

## Abnahme im Browser (Klickstrecke)

1. Glocke öffnen → unten „Alle Benachrichtigungen“ → `/benachrichtigungen` zeigt auch die Meldungen nach der 20.,
   und Seite 2 lässt sich ansteuern.
2. Filter „Erwähnung“ + „Nur ungelesene“ → die Adresszeile trägt `arten=Mention&ungelesen=1` → F5 → der Filter
   steht noch.
3. Bei einer gelesenen Meldung „Als ungelesen markieren“ → der Zähler an der Glocke steigt sofort, und die Seite
   wechselt nicht.
4. Eine Zeile anklicken → sie wird als gelesen markiert und navigiert zum Ziel. Zurück → sie ist nicht mehr fett.
5. Ein zweiter Agent erwähnt dich, während die Seite offen ist → die Meldung erscheint ohne Neuladen.
6. Als Bürger im öffentlichen Bereich die Glocke öffnen → kein Link „Alle Benachrichtigungen“. Als Partner:
   ebenfalls keiner, und `/benachrichtigungen` wird verweigert.
7. „Ansichten → Diese Ansicht merken“ → die Ansicht steht unter Strg+K.
8. Mit Tab auf den Umschaltknopf einer Zeile, dann Enter drücken → die Meldung schaltet um, und die Seite
   **wechselt nicht**. Enter auf der Zeile selbst öffnet dagegen das Ziel.
