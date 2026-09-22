# Schnellerfassung, die mehr kann als anlegen

Roadmap-Idee **#6** aus `IdeenBacklog.md:164` (Aufwand mittel, Jury-Schnitt 7). Nachgelesen am
21.09.2026, bevor eine Zeile entstand. Die Etappen #1–#5 sind gebaut und gepusht.

## Kontext

Der Plus-Knopf in der Kopfzeile legt heute ausschließlich **leere Akten mit einem Namen** an. Die
häufigste tägliche Handlung ist aber nicht „neue Akte", sondern „ich habe gerade etwas beobachtet
und will es an der richtigen Akte festhalten" — und das kostet heute: suchen, Akte öffnen,
Abschnitt finden, tippen. Vier Klicks, jedes Mal.

Die Schnellerfassung bekommt deshalb zwei weitere Einträge: **einen Vermerk an eine gesuchte Akte
hängen** und **eine Dienst-Aktivität protokollieren**. Beide Schreibpfade existieren bereits; es
kommt ein Aktenwähler über die vorhandene Suche dazu. Keine Schemaänderung, keine Migration.

## Der Befund aus dem Code

- **`QuickAddDialog.razor`** (130 Zeilen) ist ein Service-Dialog ohne Parameter, geöffnet aus
  `MainLayout.razor:34`/`:257` hinter `Policies.WriteAccess`. Acht Aktentypen, **ein** Eingabefeld,
  ein `switch` auf die acht `CreateAsync`, danach Snackbar → schließen → zur neuen Akte navigieren.
- **`CommentService.CreateAsync(entityType, entityId, text, actor)`** prüft die Sichtbarkeit der
  **Trägerakte selbst** (`Visibility.IsRecordVisibleAsync`, wirft `UnauthorizedAccessException`),
  Autor und Audit stempelt der Interceptor, Erwähnungen werden gepingt. **Es gibt dort keinen
  `Permission.Require*`-Guard** — die Schreibsperre ist heute allein der
  `ReadOnlyBarrierInterceptor`, also erst beim Speichern.
- **`AgentActivityService.CreateAsync(input, actor)`** trägt `Permission.RequireWriteAccess` als
  erste Anweisung und stempelt nach dem Speichern `FactionRecency` + Bedrohungs-Score der
  verknüpften Fraktionen. Verknüpfen geht **nur** auf Fraktion und Personengruppe
  (`AgentActivityLink`, serverseitig erzwungen). Der Eigentümer ist immer der Anlegende; es gibt
  kein Feld dafür.
- **`ISearchService.QuickSearchAsync(text, actor, max, ct)`** liefert
  `QuickHit(Category, TargetId, Name, CaseNumber)` und ist auf Kategorien mit `SearchTraits.Quick`
  gedeckelt — **14** Stück. `MentionService.CandidatesAsync:221` ist das Vorbild: Quick-Treffer
  holen, auf eine erlaubte Typmenge eindampfen.
- **`CommentPanel`** hängt an **16** Aktentypen, überall mit derselben Signatur
  (`EntityType="@nameof(X)" EntityId=… User=…`).

### Die Schnittmenge ist der Kern der Etappe

Von den 14 Quick-Kategorien tragen nur **zehn** überhaupt einen Kommentar-Abschnitt. Die übrigen
vier sind kein Schönheitsfehler:

- **Handbuch-Artikel, Glossar-Begriff, Funkkanal** fallen in `Visibility.IsRecordVisibleAsync` durch
  den Schluss `_ => true` (`Visibility.cs:197` — „unbekannter Typ = sichtbar"). Ein Wähler, der sie
  anbietet, schriebe einen Kommentar **ohne Sichtbarkeitsprüfung** an eine Akte, die ihn nie
  anzeigt — beim Handbuch-Artikel obendrein auf den **Slug** statt auf eine Id
  (`SearchCatalog.cs:392`).
- **Dokument** ist zwar sauber gegated (`DocumentVisibility`, `Visibility.cs:71`), hat aber keinen
  Kommentar-Abschnitt: der Vermerk wäre geschrieben und für immer unsichtbar.

Deshalb ist die erlaubte Typmenge eine **ausdrückliche Liste im Code mit Deckungstest**, kein
Nebeneffekt der Suche.

## Was gebaut wird

Der Dialog bekommt drei Reiter. Titel bleibt „Schnellerfassung", Breite `ExtraSmall` → `Small`.

| Reiter | Inhalt |
|---|---|
| **Neue Akte** | unverändert: Typ + Bezeichnung, danach zur neuen Akte |
| **Vermerk** | Aktenwähler (9 Typen) → Text über `MentionInput` → an die Akte gehängt |
| **Aktivität** | Titel, Art, Datum/Uhrzeit (auf jetzt vorbelegt), optionaler Text, optionale Fraktion/Gruppe |

**Vermerk-Ziele (9):** Person, Fraktion, Personengruppe, Partei, Operation, Vorgang, Taskforce,
Aufgabe, Besprechung.
**Aktivitäts-Ziele (2):** Fraktion, Personengruppe — mehr nimmt die Tabelle nicht.

**Vermerk und Aktivität navigieren nicht.** Sie schließen den Dialog, zeigen einen Hinweis, der die
Akte benennt, und lassen den Agenten auf der Seite, auf der er war. Das ist der ganze Zweck: nicht
die Arbeit verlieren, um etwas festzuhalten. „Neue Akte" navigiert weiterhin, denn dort ist die neue
Akte das Ziel.

**Reihenfolge im Vermerk-Reiter ist load-bearing:** erst die Akte wählen, dann tippen. Nur so kann
`MentionInput` `ImageOwnerType`/`ImageOwnerId` gesetzt bekommen und **Strg+V für Bilder** weiter
funktionieren — die Trägerakte muss beim Einfügen feststehen (`CLAUDE.md`: „Ein Feld, das seine
Trägerakte nicht benennen kann, darf kein Bild annehmen").

## Was bewusst **nicht** gebaut wird

- **Die Personalakte als Vermerk-Ziel.** Sie ist die zehnte Kategorie der Schnittmenge, fliegt aber
  raus: die Schnellsuche liefert Personalakten **jedem** (`PersonnelSearchProviders.QuickAsync`
  filtert nur auf Codename), `Visibility` gibt sie nur der Führung (`Visibility.cs:53`). Der Eintrag
  verspräche also neun von zehn Agenten etwas, das der Dienst danach ablehnt. Dazu kommt die
  Wortgleichheit: der *Vermerk* in der Personalakte ist eine eigene Akte (`AgentNote`,
  Tabelle `AgentVermerke`) mit eigenem Formular — zwei verschiedene Dinge unter einem Namen im
  selben Dialog wäre die schlechtere Hälfte der Ersparnis.
- **Vorlagen im Aktivitäts-Reiter.** `ActivityTemplate.ContentHtml` ist HTML und wird über
  `PlaceholderService` expandiert; das Schnellfeld ist Klartext. Eine Vorlage würde dort rohes
  Markup hineinkippen. Der Reiter bekommt stattdessen einen Link **„Ausführlich erfassen"** auf
  `/aktivitaeten/neu?fraktion={id}` — mit der gewählten Organisation vorbelegt.
- **Bild einfügen im Aktivitäts-Reiter.** Die Aktivität hat beim Einfügen noch keine Id, kann also
  ihre Trägerakte nicht benennen. Dieselbe Entscheidung wie bei `DocCreateDialog`.
- **Sechs weitere kommentierbare Typen** (Entführung, Termin, Asservat, Asservatposten,
  Finanzierung, Kassenbuchung) bleiben draußen, weil sie den `Quick`-Trait nicht tragen und die
  Schnellsuche sie gar nicht findet. Ihnen den Trait zu geben ist eine andere Etappe.

## Aufbau

1. **Plan festschreiben** — `docs/superpowers/plans/2026-09-21-schnellerfassung.md` (dieser Text).

2. **`Services/QuickCapture.cs`** (neu, statisch, wie `Permission`/`AgentSelection`) — die ganze
   Entscheidungslogik des Dialogs an der einen testbaren Stelle, weil es kein bUnit gibt:
   - `CommentTargets` — die neun CLR-Typnamen, mit der Begründung als `<remarks>`.
   - `ActivityTargets` — `Faction`, `PersonGroup`.
   - `Only(hits, allowed, max)` — Quick-Treffer auf die erlaubte Menge eindampfen und deckeln.
   - `HoldsImage(text)` — trägt der Text bereits ein `@{TextImage:…}`-Token?

3. **`HtmlCleanup.FromPlain(string?)`** (neu) — Klartext → `<p>`-Zeilen, HTML-kodiert. Die
   Aktivität speichert `ContentHtml`; das Schnellfeld liefert Klartext mit Zeilenumbrüchen.
   Dasselbe Muster steht heute dreimal inline im Repo (`AgentManagementService.cs:818`,
   `DemoDataService.cs:182`, `DemoDataServicePublic.cs:394`) — die rühre ich **nicht** an, das wäre
   eine eigene Aufräumetappe.

4. **`Components/Common/Shared/QuickRecordPicker.razor`** (neu) — ein `MudAutocomplete` über
   `QuickSearchAsync`, gefiltert über `QuickCapture.Only`, gibt den gewählten `QuickHit` per
   `EventCallback` zurück. Darstellung wie in der Befehlspalette: Symbol und deutsches Label aus
   `SearchCatalog`, Aktenzeichen als Unterzeile. Holt bewusst mehr Treffer (24) als es zeigt (8),
   weil `QuickSearchAsync` im Round-Robin über 14 Kategorien verteilt und das Eindampfen sonst
   fast alles wegwirft.

5. **`QuickAddDialog.razor` umbauen** — drei `MudTabPanel`, der vorhandene Zweig wandert unverändert
   in den ersten. Vermerk-Reiter: Wähler → `MentionInput` (mit `ImageOwnerType`/`ImageOwnerId` der
   gewählten Akte) → `CommentService.CreateAsync`. Aktivitäts-Reiter: Titel,
   `MudAutocomplete` über `GetKindsAsync()`, `MudDatePicker`+`MudTimePicker` auf jetzt vorbelegt,
   `MentionInput`, optionaler Wähler → `AgentActivityService.CreateAsync`.
   **Eine Kante, die der Wähler abfangen muss:** ist ein Bild eingefügt, liegt es schon an der
   gewählten Akte. Ein Wechsel des Ziels danach würde den Vermerk an Akte B hängen, während das Bild
   an Akte A hängt — kein Leck (das Bild bleibt hinter A gegated), aber ein Bild, das ins Leere
   zeigt. Der Wähler ist deshalb gesperrt, sobald `QuickCapture.HoldsImage` wahr ist, mit Hinweis.

6. **`CommentService.CreateAsync` bekommt `Permission.RequireWriteAccess(actor)`** als erste
   Anweisung. Heute hängt die Schreibsperre dort allein am Interceptor, und die Schnellerfassung ist
   ein **neuer globaler Schreibeinstieg** — genau der Fall, für den `CLAUDE.md` den Guard im Dienst
   verlangt. Verhalten ändert sich nicht (`MayWrite()` = nicht Nur-Lese, nicht Partner, nicht Demo,
   und alle drei blockt der Interceptor bereits), nur die Meldung kommt früher und lesbar.
   `EditAsync`/`DeleteAsync` bleiben unangetastet — sie gehören nicht zu dieser Etappe.

7. **Tests** (`NOOSE-Website.Tests/Services/`):
   - `QuickCaptureTests` — jedes Vermerk-Ziel trägt `SearchTraits.Quick`; jedes ist über
     `SearchNavigation.For` adressierbar (die Erwähnungs-Benachrichtigung hängt an dieser Route);
     `Only` wirft Dokument, Handbuch-Artikel, Funkkanal und Personalakte raus und deckelt;
     `HoldsImage` für Token, Klartext und `null`.
   - `CommentTargetScanTests` — Quellscan nach dem Muster von `PrintRichHtmlScanTests.cs:16`: jeder
     Typ der Liste hat wirklich einen `<CommentPanel>`-Einbau, und die Differenz
     „kommentierbar ∩ Quick" minus Liste ist **genau** `{ Agent }`. Damit wird jede künftige
     Trait-Vergabe zu einer Entscheidung statt zu einem stillen neuen Ziel.
   - `HtmlCleanupTests` — `FromPlain`: kodiert, je Zeile ein `<p>`, leer → leer, CRLF.
   - `CommentServiceTests` +1 — Nur-Lese-Aufsicht wird vom neuen Guard abgewiesen
     (`SqliteTestContext` hängt **keine** Interceptors an, der Test misst also wirklich den Guard).
   - Jede neue Schranke wird einmal sabotiert, um zu sehen, dass der richtige Test rot wird;
     Sicherung vorher in den Scratchpad, Rückspielung von dort — **nie** `git checkout`.

8. **Changelog** `2.1.70-schnellerfassung`, Bereich „Bedienung", in Alltagssprache.

9. **Handbuch** — `HandbookContent.Revision` **12 → 13**, weil zwei bestehende Zeilen angefasst
   werden:
   - **neu:** `art-schnellerfassung` („Schnell erfassen") im Kapitel *Erste Schritte*. Der Dialog
     ist heute **überhaupt nicht** dokumentiert; der Artikel erklärt alle drei Reiter.
   - **Korrektur:** `art-aktivitaeten` (`DutyChapter.cs:98`) behauptet, Dienst-Aktivitäten seien
     „die Grundlage der Bestenliste". Nachgemessen: `GamificationService.ComputeForAgentAsync:230`
     zählt Akten, Doks, Verknüpfungen, Einstufungen, Observationen und gelöste Vorgänge —
     **Aktivitäten kommen darin nicht vor**, kein Gamification-Modul liest die Tabelle. Der Satz
     wird ersetzt.
   - **Querverweis** in `art-kommentare`: ein Vermerk geht auch aus der Kopfzeile.

10. **Bauen** (`dotnet build NOOSE-Website.slnx`) und **gezielt testen** (`--filter` auf die vier
    Klassen oben plus `SearchCatalogTests`, `HandbookTests`, `ChangelogTests`). Die volle Suite
    läuft nicht.

11. **Review durch Agents** über den frisch geschriebenen Code, jeder Befund adversarisch
    gegengeprüft; die widerlegten selbst durchsehen. Danach committen und pushen **in Tristans
    Namen**.

## Abnahme im Browser (Tristans Klickstrecke)

1. Plus-Knopf → Reiter **Vermerk** → eine Person suchen, auswählen, Text tippen, speichern. Die
   Seite darunter bleibt stehen, der Hinweis nennt die Akte, der Vermerk steht an der Akte.
2. Im selben Reiter ein Bild mit **Strg+V** einfügen → es hängt an der gewählten Akte, und der
   Wähler lässt sich danach nicht mehr umstellen.
3. Reiter **Aktivität** → Titel, Art, Zeit steht auf jetzt, ein paar Sätze, Fraktion verknüpfen,
   speichern. Der Eintrag steht unter `/aktivitaeten` und im Aktivitäts-Abschnitt der Fraktion.
4. In der Suche nach einem **Handbuch-Artikel** oder **Funkkanal** tippen → er erscheint im
   Vermerk-Wähler **nicht**.
5. Reiter **Neue Akte** wie bisher: Person anlegen → landet auf der neuen Akte.

## Offen aus früheren Etappen

Sechs Vorhaben sind gebaut und gepusht, aber **nie im Browser gesehen**: Handbuch/Changelog/NOOSEI,
Aktenarchiv, Score-Verlauf, Funkplan, Abwesenheits-Hinweis, Tastatur-Kurzbefehle. Es gibt kein
bUnit; `.razor` läuft in keinem Testlauf. Die Klickstrecken je Vorhaben stehen in
`docs/superpowers/plans/`.

## Umgesetzt am 22.09.2026

Aufgaben 1-10 stehen; die Abnahme im Browser ist offen.

**Abweichungen vom Plan, alle bewusst:**

1. **`HtmlCleanup.FromPlain` kodiert nicht mit `WebUtility.HtmlEncode`.** Der Plan sagte „HTML-kodiert",
   und der erste Wurf tat genau das - bis ein Test zeigte, dass diese Methode **jedes Zeichen über 159**
   in eine Zahlen-Entität verwandelt. Aus „Prüffall" würde `Pr&#252;ffall`: in der Spalte und im
   Änderungsprotokoll unlesbar, und obendrein nicht mehr seine eigene Ausgabe, weil der Sanitizer die
   Entität sofort wieder auflöst - der Rundlauf `Clean(FromPlain(x)) == FromPlain(x)` wäre gebrochen.
   Ersetzt werden jetzt genau die drei Zeichen, die im Textknoten Markup wären: `&`, `<`, `>`.
2. **`MudTabs` heißt der Abstand `TabPanelsClass`, nicht `PanelClass`.** Der MudBlazor-Analysator
   meldete das als 42. Warnung; mit dem richtigen Namen steht der Bau wieder bei 41.

## Nachträglich beauftragt: der Changelog-Abschnitt 2.2

Tristan wollte mitten in der Etappe alle Zeilen **ab 2.1.65 einschließlich der neuen** in einen eigenen
Abschnitt **2.2** verlagert haben. Das ist mehr als ein Verschieben im Quelltext:

- Der Test `Every_shipped_version_and_key_uses_sequential_two_digit_updates` verlangt, dass der
  Schlüssel jeder Zeile zu **ihrer** Fassung passt und lückenlos bei `00` beginnt. Die sechs Zeilen
  mussten also von `2.1.65…70` auf `2.2.00…05` umbenannt werden.
- Genau davor warnt der Seeder aber selbst: *„Stable handle; renaming one orphans the old row and
  creates a second."* In jeder Datenbank, die `2.1.65-archiv` schon kennt, wäre die alte Zeile in der
  alten Fassung stehengeblieben und eine zweite daneben entstanden - **sichtbar doppelt** auf
  `/neuerungen`.
- Das vorhandene `LegacyVersion` an der Fassung kann das nicht auffangen: es schreibt nur den Präfix
  **aller** Schlüssel einer Fassung um (`1.5-archiv` → `2.1.65-archiv`), und eine umziehende Zeile
  bekommt eine **neue laufende Nummer** - sechs verschiedene alte Präfixe lassen sich mit einer
  Zeichenkette nicht abbilden.

Deshalb nennt eine umziehende Zeile jetzt ihren alten Schlüssel selbst (`SeededEntry.LegacyKey`,
fünftes Argument von `Neu`/`Besser`/`Fix`), und `ChangelogSeeder.LegacyKeyOf` wertet beide Formen aus -
die alte je Fassung, die neue je Zeile. Der Umzugspfad ist damit allgemein und beim nächsten Neuschnitt
wieder benutzbar. Ein Test (`A_line_moved_into_another_release_keeps_its_row`) hält fest, dass es
**dieselbe Zeile** ist, die umzieht - gleiche Id, neue Fassung, keine zweite daneben. Die Regel steht
jetzt auch in `CLAUDE.md`.

## Falsifikation

Jede neue Schranke wurde einmal sabotiert, um zu sehen, dass der richtige Test rot wird - Sicherung
vorher in den Scratchpad, Rückspielung von dort, nie `git checkout`:

| Sabotage | Erwartet rot | Ergebnis |
|---|---|---|
| `Permission.RequireWriteAccess` aus `CommentService.CreateAsync` entfernt | `CreateAsync_Throws_ForReadOnlySupervision` | rot |
| `QuickCapture.Only` filtert nicht mehr | `OnlyDropsAQuickCategoryThatIsNoNoteTarget` (5 Fälle), `TheCapCountsWhatSurvivesTheFilter`, `OnlyNarrowsAnActivityPickerFurtherThanANote` | rot |
| `Escape` wieder auf `WebUtility.HtmlEncode` | `FromPlain_Umlauts_ArePassedThroughVerbatim` | rot |
| `nameof(Meeting)` aus `CommentTargets` entfernt | `Only_the_personnel_file_is_commentable_quick_and_left_out` | rot |
| `LegacyKeyOf` ignoriert den Zeilen-Schlüssel | `A_line_moved_into_another_release_keeps_its_row` | rot |

Danach alles zurückgespielt und erneut grün geprüft.

## Was der Review ergab

Fünf Blickrichtungen (Rechte, Blazor-Zustand, Seeder, reine Helfer, Inhalt), jeder Befund von zwei
Skeptikern mit unterschiedlicher Linse gegengeprüft, Voreinstellung „widerlegt": **12 Rohbefunde,
11 überlebt, 1 widerlegt**. Die elf fallen auf sechs Sachen zusammen.

**1. Die umgezogenen Changelog-Zeilen behielten ihre Sortiernummer.** Von *fünf* der sechs
Blickrichtungen unabhängig gefunden, von vier Skeptikern bestätigt. `RenameLegacyEntryKeysAsync`
schreibt beim Umzug nur `SeedKey` und `ReleaseId`; die `SortOrder` setzt allein `SeedEntriesAsync`,
und dessen Aktualisierungszweig steht hinter `row.SeedRevision >= revision`. Mit `Revision = 1`
wurde er übersprungen. Folge auf **jeder Bestandsdatenbank**: die fünf Altzeilen behielten 650–690
aus der Fassung mit siebzig Einträgen, die neue Zeile bekam 50 — die Schnellerfassung hätte in der
Fassung 2.2.00 ganz **oben** gestanden statt unten, und auf einer frischen Datenbank wäre die
Reihenfolge eine andere gewesen als auf der Produktion. `Revision` steht jetzt auf 2, mit der
Begründung direkt an der Konstante; der Umzugstest prüft zusätzlich, dass die umgezogene Zeile
**vor** einer nie zuvor ausgelieferten steht.

**2. Der Aktivitäts-Wähler konnte strukturell nie mehr als zwei Fraktionen anbieten.** Die
Schnellsuche verteilt ihre Plätze reihum über **alle** Quick-Kategorien und schneidet erst danach auf
die angefragte Zahl ab. Wer zwei von vierzehn Kategorien annimmt, sieht zwei Zeilen — egal wie viele
er anfragt. Ein Agent, der einen Fraktionsnamen tippt, hätte „keine Akte gefunden" gelesen, während
sieben passen. `QuickCapture.FetchCount` fragt jetzt den Anteil mit ab, der weggeworfen wird
(gedeckelt bei 60, damit der schmalste Wähler nicht den Index leerräumt).

**3. *Ausführlich erfassen* warf Titel, Art und Text ohne Warnung weg.** Das ausführliche Formular
nimmt nur die Organisation mit. Jetzt fragt der Dialog nach, sobald etwas getippt ist.

**4. Der Artikel `art-bestenliste` trug dieselbe falsche Behauptung**, die diese Etappe zwei Artikel
weiter oben gestrichen hatte: „erledigte Aufgaben, eingetragene Aktivitäten" als Grundlage der
Bestenliste. `GamificationService.ComputeForAgentAsync` zählt **sechs** andere Dinge, und die
Bestenliste zeigt genau diese sechs als Spalten: Akten, Doks, Verknüpfungen, Einstufungen,
Observationen, gelöste Vorgänge. Der Artikel nennt jetzt die sechs und sagt ausdrücklich, dass
Aufgaben und Aktivitäten **nicht** zählen.

**5. „Danach stehst du auf der neuen Akte" stimmte für die Aufgabe nicht** — der Dialog sprang auf
die Aufgabenliste statt auf die neue Aufgabe. Statt den Satz zu entschärfen ist der Sprung
repariert: `JobService.CreateAsync` gibt die Aufgabe zurück, die Route `/aufgaben/{Id}` gibt es.
Damit stimmt der Satz für alle acht Typen.

**6. Der Hilfetext am Aktivitäts-Wähler versprach zu viel.** `FactionRecency.StampAsync` läuft nur
für **Fraktions**-Verknüpfungen; für eine Personengruppe gibt es keine Aktualitäts-Ampel. Der Text
sagt das jetzt.

**Widerlegt (1):** der Vorwurf, `CoerceText="false"` lasse sichtbaren Text und gebundenes Ziel
auseinanderlaufen. Ein Skeptiker hat die MudBlazor-Quelle gelesen: der Text wird über einen zweiten,
von `CoerceText` unberührten Pfad zurückgesetzt (`MudBaseInput.SetParametersAsync` →
`UpdateTextPropertyAsync`). Nachvollzogen und angenommen.

**Bewusst hingenommen:** die Login-Hinweiskarte meldet nach dem nächsten Start **sechs** Neuerungen,
obwohl fünf davon schon bekannt sind. Die Karte vergleicht das Anlegedatum der *Fassung*, und die
Fassung 2.2.00 ist neu. Das ist der Preis des neuen Abschnitts und nicht zu beheben, ohne die
Neuheit pro Zeile zu verfolgen — was das Datenmodell nicht tut.

**Zusätzliche Falsifikation nach den Korrekturen:**

| Sabotage | Erwartet rot | Ergebnis |
|---|---|---|
| `FetchCount` gibt wieder `max` zurück | `FetchCountAsksForMoreTheNarrowerThePickerIs` | rot |
| `SeedEntriesAsync` schreibt die `SortOrder` nicht mehr | `A_line_moved_into_another_release_keeps_its_row` | rot |

Ein neuer statischer Test (`Every_shipped_legacy_key_names_a_line_that_is_gone`) hält fest, dass ein
alter Schlüssel nie auf eine noch lebende Zeile zeigt — sonst benennt der Seeder eine lebende Zeile
auf eine andere um und der Unique-Index kippt den ganzen ersten Start.

