# Etappe 10 — Neu seit deinem letzten Besuch

Roadmap-Idee **#10** aus `IdeenBacklog.md:236` (Aufwand mittel, Jury-Schnitt 6,3). Nachgelesen am
23.09.2026, bevor eine Zeile entstand. Etappen #1–#9 sind gebaut und gepusht.

## Kontext

Eine gut gepflegte Personenakte hat fünfzehn Abschnitte. Wer sie nach einer Woche wieder öffnet, muss
jeden einzeln nach Zeitstempeln absuchen, um zu sehen, was passiert ist. Die Beobachtungsliste meldet nur
„geändert", nicht **was**.

Ergebnis dieser Etappe: Oben auf der Akte steht eine Zeile *„Seit deinem letzten Besuch am 16.09. um
14:20: 3 Kommentare, 1 Dok, Einstufung geändert"* mit einem Sprung in den Zeitstrahl, und im Zeitstrahl
trägt jeder neue Eintrag ein **Neu**. Ein Filter-Chip *Neu* zeigt nur diese.

## Der Befund aus dem Code

- **Der Besuch steht schon in der Datenbank.** Jede Detailseite ruft `IAccessLogService.LogViewAsync`
  und schreibt eine `AccessLog`-Zeile (`AgentId`, `EntitaetTyp`, `EntitaetId`, `Zeitpunkt`). Das ist
  genau der „Zeitstempel je Agent **und** Akte", den der Backlog als neue Tabelle ansetzt — nur als
  Protokoll statt als Stempel. `AccessLog` steht in der Whitelist des `ReadOnlyBarrierInterceptor`,
  wird also auch für Nur-Lese-Aufsicht, Partner und Demo-Besucher geschrieben.
- **Aber es fehlt der passende Index.** Vorhanden sind `(EntitaetTyp, EntitaetId)` (alle Besuche aller
  Agenten an der Akte), `(Zeitpunkt)` und `(AgentId, Zeitpunkt)` (alle Besuche des Agenten an allen
  Akten). Die neue Abfrage „meine letzten Besuche an **dieser** Akte" liefe damit über einen der beiden
  Bereiche und filterte den Rest — und das bei **jedem** Öffnen einer Akte.
- **Der Zeitstrahl ist der fertige Diff.** `TimelineService.BuildAsync` sammelt Audit-Zeilen samt
  Kind-Fan-out, Einstufungsverlauf, Kommentare, Quellen, Wiedervorlagen, Verknüpfungen, Observationen,
  Beziehungen und Aktivitäten, alles schon hinter den Sichtbarkeits- und Partner-Gattern. Die
  Zusammenfassung ist eine Zählung über diese Einträge, keine zweite Fan-out-Logik.
- **`TimelineEntry` kennt den Handelnden nur als Namen.** Um die **eigenen** Einträge nicht als neu zu
  melden, braucht der Eintrag die Id. `Raw` trägt sie für die meisten Quellen schon (`ActorId`), für
  Audit, Einstufung und Kommentar fehlt sie nur in der Projektion (`AgentId`/`CreatedById` stehen in den
  Tabellen).
- **Zwei Quellen sind rückdatierbar.** Die Observation steht mit ihrem Beginn (`Start`) im Zeitstrahl,
  die Aktivität mit `ActivityDate`. Eine heute erfasste Observation von letzter Woche läge zeitlich
  **vor** dem letzten Besuch und wäre nach dem Zeitstempel nie neu. Ob etwas neu ist, entscheidet
  deshalb der **Erfassungszeitpunkt** (`CreatedAt`), nicht der Ereigniszeitpunkt.
- **Vorbild Chronik:** `NavPreferences.ChronikLastSeenUtc` treibt dort einen Trenner
  (`ChronikFeed.FirstOlderIndex`, CSS `.chronik-neu`). Ein Trenner hilft hier nicht: wegen der
  rückdatierten Einträge liegt Neues nicht zwingend über einer Zeitlinie. Also je Eintrag ein **Neu**.
- **Zwölf Seiten tragen einen Zeitstrahl-Abschnitt `historie` im Rail, aber nur sieben protokollieren
  Besuche**: Person, Fraktion, Partei, Personengruppe, Vorgang, Operation, Taskforce. Alle sieben laden in
  `OnParametersSetAsync` mit `_loadedId`-Wächter und protokollieren den Besuch dort. Entführung, Asservat
  (Gegenstand und Buchung), Finanzierungsantrag und Kassenbuchung haben den Abschnitt, schreiben aber
  keine Zugriffszeile. Aufgabe, Termin und Besprechung protokollieren, zeigen den Zeitstrahl aber in
  `MudTabs` ohne URL-Reiter.
- **`RecordSectionRail` hört auf `LocationChanged`** und liest dann `?tab=` neu. Ein Sprung über
  `Nav.NavigateTo(… ?tab=historie)` wechselt also den Abschnitt, ohne die Seite neu zu laden (der
  `_loadedId`-Wächter lässt eine reine Query-Änderung durch).

## Entscheidungen

**1. Keine neue Tabelle — das Zugriffsprotokoll plus ein Index.** Eine eigene Stempel-Tabelle
(`AktenBesuche`) bräuchte Upsert, Aufräumregel für gelöschte Akten (polymorph, kein FK), Whitelist im
Schreibschutz, `AgentDeleteCoverage`, `PublicVisibility`-Eintrag. Das Protokoll gibt es schon, es wird
schon geschrieben, und es wird nie aufgeräumt. Neu ist nur der Index
`(AgentId, EntitaetTyp, EntitaetId, Zeitpunkt)` auf `ZugriffsLogs` — Migration **`Phase84_BesuchsIndex`**. Sollte das
Protokoll je eine Aufbewahrungsfrist bekommen, verliert die Funktion nur alte Besuche und zeigt dann
nichts statt Falsches.

**2. Ein Besuch ist eine Sitzung, nicht ein Seitenaufruf.** Nimmt man stumpf den vorigen Protokolleintrag,
verschwinden alle Markierungen beim nächsten Neuladen, beim Zurück-Knopf und schon beim Prerendern (die
Seite läuft zweimal und schreibt zwei Zeilen). Stattdessen: Aufrufe, zwischen denen weniger als
**30 Minuten** liegen, gehören zu einer Sitzung; „letzter Besuch" ist der letzte Aufruf der **vorigen**
Sitzung. Wer die Akte öffnet, einer Verknüpfung folgt und zurückkommt, sieht dieselben Markierungen. Die
Regel ist rein und steht in `Services/RecordVisits.cs` (statischer Helfer, wie `Onboarding`). Damit ist
auch die Reihenfolge von Lesen und Protokollieren egal.

**3. Eigenes ist nicht neu.** Ein Eintrag ist neu, wenn er **nach** dem letzten Besuch **erfasst** wurde
und **nicht** vom Betrachter stammt. `TimelineEntry` bekommt dafür `ActorId` und `RecordedAt` (beide
optional am Ende, damit kein Aufrufer bricht). Wo der Handelnde verborgen wird (Bürgerhinweis,
`TipAnonymity`), bleibt auch die Id leer — sonst stünde sie im Eintrag, obwohl der Name gestrichen ist.

**4. Die Zusammenfassung kommt aus dem Zeitstrahl, gefiltert im Dienst.**
`ITimelineService.GetNewSinceAsync(type, id, viewer, sinceUtc)` baut den Zeitstrahl hinter denselben
Gattern und gibt nur die neuen Einträge zurück. Die Zeile zählt je Kategorie mit deutschem Plural
(`TimelineCategoryDisplay.Plural`); „Einstufung geändert" ohne Zahl. Reihenfolge nach Gewicht
(Einstufung, Doks, Observationen, Kommentare, …), höchstens sechs Teile, danach „und weitere".

**5. Die Zeile lädt für sich.** `SinceLastVisitBar` holt die neuen Einträge in einem eigenen Lebenszyklus;
die Akte selbst wartet nicht darauf. Kein letzter Besuch oder nichts Neues ⇒ **keine** Zeile (sonst stünde
auf jeder Akte bei jedem Öffnen eine Leerzeile).

**6. Wo die Zeile nicht erscheint.**
- **Demo-Besucher:** alle anonymen Besucher sind **ein** Konto. „Dein letzter Besuch" wäre der eines
  Fremden. `PreviousVisitAsync` gibt dort `null`.
- **Partner ohne freigegebenen Zeitstrahl:** die Zeile verrät Kategorien und Zahlen aus dem Zeitstrahl.
  Sie hängt deshalb an **derselben** Bedingung wie der Abschnitt (`TabOn("historie")`).

**7. Nur die sieben Akten, die Besuche protokollieren und einen Zeitstrahl im Rail haben.** Das sind die
Akten mit den fünfzehn Abschnitten, um die es im Backlog geht. Aufgabe, Termin und Besprechung sind kurz
und ihre Reiter nicht adressierbar. Den fünf übrigen Rail-Seiten (Entführung, Asservate, Finanzierung,
Kasse) fehlt der Besuch; ihn dort nachzurüsten hieße, das Zugriffsprotokoll — und damit die
Gegenaufklärung — um neue Zeilen zu erweitern. Das ist eine eigene Entscheidung, keine Nebenwirkung.

## Was bewusst **nicht** gebaut wird

- **Kein „Als gelesen markieren".** Die Sitzungsregel beantwortet das: nach einer halben Stunde Pause ist
  der Stand von heute der neue Bezugspunkt.
- **Keine Markierungen in den übrigen Abschnitten** (Kommentarliste, Doks). Der Zeitstrahl ist der eine Ort,
  an dem alles zusammenläuft; fünfzehn Abschnitte einzeln zu markieren wäre die Diff-Logik fünfzehnmal.
- **Keine Live-Aktualisierung** der Zeile, während die Akte offen ist.
- **Keine Verbindung zur Beobachtungsliste** — deren Meldung sagt weiter nur „geändert".
- **Kein Rückkehrer-Block auf dem Lagezentrum** — der ist Teil von Idee #15 („Meine Schicht").

## Aufbau

1. **Plan festschreiben** — diese Datei.
2. **Index** — `AppDbContext`: `HasIndex(a => new { a.AgentId, a.EntityType, a.EntityId, a.Timestamp })`,
   Migration `Phase84_BesuchsIndex` über den Offline-Weg (Design-Time-Fabrik vorübergehend auf
   `ServerVersion.Parse`, Dummy-Verbindung, danach zurückbauen).
3. **Regel** — `Services/RecordVisits.cs`: `SessionGap`, `PreviousSessionEnd(visitsNewestFirst, nowUtc)`,
   `IsNew(entry, sinceUtc, viewerId)`, `Summarise(entries)`; Plural in `TimelineCategoryDisplay`.
4. **Letzter Besuch** — `IAccessLogService.PreviousVisitAsync(type, id)`: die letzten 100 eigenen
   Aufrufe dieser Akte, neueste zuerst, durch die Sitzungsregel. `null` ohne Agent und für den Demo-Besucher.
5. **Zeitstrahl** — `TimelineEntry` + `ActorId`, `RecordedAt`; Projektion in `TimelineService`;
   `GetNewSinceAsync`.
6. **Oberfläche** — `Components/Common/Shared/SinceLastVisitBar.razor` (neu); `TimelinePanel` bekommt
   `NewSince`, einen Chip *Neu* je neuem Eintrag und einen Filter-Chip *Neu (n)*.
7. **Sieben Seiten verdrahten** — `_since` vor dem Protokollieren lesen (bei A→B zurücksetzen), Zeile über
   dem Rail unter `TabOn("historie")`, `NewSince="_since"` am `TimelinePanel`.
8. **Changelog** `2.2.09-neu-seit-besuch`, **Handbuch** neuer Artikel „Was ist neu an dieser Akte?" im
   Kapitel *Akten führen* hinter *Beobachtete Akten* (neu ⇒ keine Revisions-Erhöhung).
9. **Bauen, gezielt testen, sabotieren, Review, Commit, Push.**

## Tests (kein bUnit)

- `RecordVisitsTests` (rein): kein Besuch ⇒ `null`; ein Besuch vor zwei Stunden ⇒ der; ein Besuch vor
  zehn Minuten ⇒ `null` (dieselbe Sitzung, es gab keinen davor); Kette aus kurzen Abständen wird
  übersprungen bis zur ersten Lücke; Doppelzeile aus dem Prerendern; Grenze genau 30 Minuten; ein
  Eintrag in der Zukunft (Uhrenversatz) bricht die Kette nicht. `IsNew`: später und fremd ⇒ neu; eigener
  ⇒ nicht; genau auf dem Besuch ⇒ nicht; rückdatierte Observation mit späterem `RecordedAt` ⇒ neu; ohne
  Bezugspunkt ⇒ nie. `Summarise`: Singular/Plural, „Einstufung geändert" ohne Zahl, Reihenfolge, Deckel.
- `AccessLogServiceTests` (+): nur eigene Aufrufe **dieser** Akte zählen (fremder Agent, andere Akte,
  gleiche Id anderer Typ); Demo ⇒ `null`; System ⇒ `null`.
- `TimelineServiceTests` (+): `GetNewSinceAsync` lässt Eigenes und Älteres weg, nimmt die rückdatierte
  Observation, trägt `ActorId`; ein Bürgerhinweis trägt **keine** `ActorId`; unsichtbare Akte ⇒ leer.
- `SinceLastVisitScanTests` (Quelltext-Scan jeder Seite mit Besuchsprotokoll **und** Zeitstrahl-Abschnitt —
  genau die sieben): jede liest `PreviousVisitAsync`, setzt ihn beim Aktenwechsel zurück, gibt
  `NewSince` an ihren `TimelinePanel`, rendert die Zeile unter derselben Bedingung wie den Abschnitt
  `historie`, und zeigt mit `Slug` auf `historie`.
- Jede neue Schranke einmal sabotieren, Sicherung im Scratchpad, nie `git checkout`.

## Abnahme im Browser (Klickstrecke)

1. Als Agent A eine Personenakte öffnen, schließen. Als Agent B einen Kommentar schreiben, ein Dok
   anlegen, die Einstufung ändern.
2. Uhr 30 Minuten vordrehen (oder warten), als A die Akte öffnen ⇒ Zeile „Seit deinem letzten Besuch …:
   Einstufung geändert, 1 Dok, 1 Kommentar". *Im Zeitstrahl ansehen* ⇒ Abschnitt wechselt, drei Einträge
   tragen *Neu*, Chip *Neu (3)* filtert auf sie.
3. F5 ⇒ Zeile und Markierungen bleiben (dieselbe Sitzung).
4. Als A selbst einen Kommentar schreiben, neu laden ⇒ er zählt **nicht** mit.
5. Als B eine Observation mit Beginn letzte Woche erfassen ⇒ bei A nach Pause *Neu*, obwohl sie weit
   unten im Zeitstrahl steht.
6. Partner ohne Freigabe für den Zeitstrahl ⇒ keine Zeile. Demo-Besucher ⇒ keine Zeile.
7. Erste Öffnung einer Akte überhaupt ⇒ keine Zeile.

## Was gebaut wurde

| Datei | Was |
|---|---|
| `Data/AppDbContext.cs`, Migration `Phase84_BesuchsIndex` | Index `(AgentId, EntitaetTyp, EntitaetId, Zeitpunkt)` auf `ZugriffsLogs` |
| `Services/RecordVisits.cs` (neu) | Sitzungsregel (30 Min), `IsNew`, `Summarise`, `VisitLabel` („heute/gestern/am … um …“) |
| `Services/TimelineCategoryDisplay.cs` | `Counted`: Zahl mit Singular/Plural je Kategorie |
| `Services/IAccessLogService.cs` + Impl. | `PreviousVisitAsync`: letzte 100 eigene Aufrufe der Akte, `null` für Demo und System |
| `Models/Timeline/TimelineModelle.cs`, `Services/TimelineService.cs` | `TimelineEntry` + `ActorId`, `RecordedAt`; Audit, Einstufung und Kommentar reichen die Id durch, Hinweis nicht; Observation und Aktivität tragen `CreatedAt`; `GetNewSinceAsync` |
| `Components/Common/Shared/SinceLastVisitBar.razor` (neu) | Zeile über dem Rail, lädt erst interaktiv (kein doppelter Zeitstrahl beim Prerendern), schließbar |
| `Components/Common/Shared/TimelinePanel.razor` | `NewSince`, Chip *Neu* je Eintrag, Filter-Chip *Neu (n)* |
| sieben Detailseiten | `_since` lesen/zurücksetzen, Zeile unter `TabOn("historie")`, `NewSince` am Zeitstrahl |
| Changelog `2.2.09-neu-seit-besuch`, Handbuch `art-neu-seit-besuch` | neu, also ohne Handbuch-Revision |
| `CLAUDE.md` | Gotcha: neue Zeitstrahl-Quelle braucht `ActorId` (und ggf. `RecordedAt`) |

## Abweichungen vom Plan

- **Zwölf statt sieben Rail-Seiten mit Zeitstrahl.** Der Scan-Test fand beim ersten Lauf fünf weitere
  (Entführung, zwei Asservat-Seiten, Finanzierung, Kasse), die gar keinen Besuch protokollieren. Der Plan
  ist korrigiert; der Scan wählt jetzt „protokolliert Besuche **und** hat `historie`" — das sind genau die sieben.
- **Die Zeile lädt nicht beim Prerendern** (`RendererInfo.IsInteractive`). Sonst baute jede Akte ihren
  Zeitstrahl zweimal, nur für eine Zeile, die im statischen Durchgang niemand sieht.

## Nebenbefunde, mitgenommen

1. **Der Changelog-Seeder scheiterte auf MySQL komplett.** Die Spalte `Titel` fasst 300 Zeichen,
   `2.2.07-ansichten` hatte 335, `2.2.08-entwuerfe` 312. Der Seeder speichert alle neuen Zeilen in einem
   Rutsch; MySQL (strikter Modus) verwirft den ganzen Batch, der Start loggt nur „Seeding Neuerungen
   fehlgeschlagen" und läuft weiter. **Auf `/neuerungen` wäre seit Etappe 8 keine neue Zeile angekommen** —
   auch diese nicht. SQLite prüft Längen nicht, deshalb war kein Test rot. Beide Zeilen gekürzt,
   `ChangelogContent.Revision` 3 → 4, neuer Test `Every_shipped_line_fits_its_column` liest die Längen aus dem
   EF-Modell. Gefunden erst durch den echten Start gegen MariaDB.
2. **`ConfirmDialogLabelTests` war auf dem Ausgangsstand rot:** die Rückfrage „Ausführlich erfassen?" der
   Schnellerfassung (Etappe 6) bot einen roten *Löschen*-Knopf an, obwohl sie nichts löscht. Jetzt
   *Zum Formular* in Warnfarbe.
3. **`RecordSectionRail.razor` baut mit SDK 10.0.112 nicht:** die Schleifenvariable `section` macht aus
   `@section.Icon` eine Razor-Direktive. Umbenannt in `entry`; auf dem älteren SDK des Hauptrechners ändert
   das nichts.

## Tests

| Klasse | Fälle | hält |
|---|---|---|
| `RecordVisitsTests` (neu) | 20 | Sitzungsregel inkl. Prerender-Doppelzeile, Grenze, Uhrenversatz; `IsNew` inkl. Nachtrag; Zusammenfassung; Plural je Kategorie; Datumsbeschriftung |
| `AccessLogServiceTests` (+5) | 8 | nur eigene Aufrufe **dieser** Akte (anderer Agent, andere Akte, gleiche Id anderer Typ), Erstbesuch, gleiches Ergebnis vor und nach dem Protokollieren, Demo, System |
| `TimelineServiceTests` (+5) | 30 | `ActorId` aus Kommentar/Einstufung/Audit, Hinweis ohne Name **und** Id, `GetNewSinceAsync` ohne Eigenes und Älteres, Observation nach Erfassungszeit, VS für Junior leer |
| `SinceLastVisitScanTests` (neu) | 5 | genau sieben Seiten; lesen und zurücksetzen; `NewSince` am Zeitstrahl; Zeile für die eigene Akte; gleiche Sperre wie der Abschnitt |
| `ChangelogTests` (+1) | — | jede Zeile passt in ihre Spalte |

Bau: **0 Fehler, 39 Warnungen** (Ausgangsstand). Volle Suite unter `LANG=de_DE.UTF-8`: **7.930 Tests, 0 rot.**
Ohne deutsche Locale sind vier Zahlenformat-Tests rot (`125,000` statt `125.000`) — Container, nicht Code.

### Falsifikation

Jede Schranke einmal sabotiert (Sicherung im Scratchpad, Rückspielung per Kopie, `git diff`-Prüfsumme davor
und danach identisch). Zwei Läufe mit disjunkten Erwartungen:

| Sabotage | rot wurde |
|---|---|
| eigener Eintrag zählt mit | `The_viewers_own_entry_is_never_new`, `GetNewSinceAsync_LeavesOutTheViewersOwn…` |
| Hinweis-Id sichtbar | `GetTimelineAsync_Tip_CarriesNeitherTheSubmittersNameNorHisId` |
| Demo-Sperre entfernt | `PreviousVisitAsync_SharedDemoAccount_ReturnsNull` |
| Typ-Filter im Besuch entfernt | `PreviousVisitAsync_CountsOnlyTheAgentsOwnViewsOfThatRecord` |
| `NewSince` an der Fraktion entfernt | `Every_timeline_marks_what_is_new` |
| `_since = null` im Vorgang entfernt | `Every_record_reads_the_previous_visit_and_forgets_it_on_the_next_record` |
| Zeile der Partei ohne Sperre | `The_line_is_gated_like_the_timeline_section_itself` |
| Zeile der Gruppe mit falschem Typ | `Every_record_shows_the_line_for_itself` |
| Changelog-Zeile über 300 Zeichen | `Every_shipped_line_fits_its_column` |
| Einstufung mit Zahl | `A_classification_is_named_without_a_count_and_leads_the_line` |
| keine Sitzungsregel | sechs `RecordVisitsTests`, `PreviousVisitAsync_FirstVisit_ReturnsNull`, `…_IsTheSameBeforeAndAfter…` |
| Ereigniszeit statt Erfassungszeit | beide Nachtrag-Tests (rein und Integration) |
| Kommentar ohne `ActorId` | `GetTimelineAsync_Entries_CarryTheActingAgentsId`, `GetNewSinceAsync_LeavesOut…` |

### Im Browser gesehen

App gegen MariaDB 10.11 gestartet (`Phase84` angewandt, Demo-Daten geseedet), Demo-Sperre **nur lokal**
vorübergehend entfernt, ein Besuch vor drei Stunden und Einträge eines Kollegen per SQL eingefügt. Ergebnis:
Zeile „Seit deinem letzten Besuch gestern um 22:53: Einstufung geändert, 1 Observation, 2 Kommentare,
3 Änderungen und 1 Anlage"; *Im Zeitstrahl ansehen* wechselt auf `?tab=historie`; acht Einträge tragen *Neu*,
der eigene Kommentar nicht; *Neu (8)* filtert. Die Klickstrecke oben (zwei Agenten, Partner) bleibt offen —
dafür braucht es echte Anmeldungen.

## Bekannt und bewusst offen

- **Druckansicht und NOOSEI zählen als Besuch.** Beide schreiben dieselbe Zugriffszeile. Wer die Akte nur
  druckt oder NOOSEI fragt, hat sie danach „besucht".
- **Einstufungswechsel zählen doppelt in der Zeile** — als „Einstufung geändert" und als Änderung an der
  Akte, weil der Zeitstrahl beide Zeilen führt.
- **Die Zeile aktualisiert sich nicht**, während die Akte offen ist.
