# Etappe 11 — Mehrfachauswahl in der Suche

Roadmap-Idee **#11** aus `IdeenBacklog.md:261` (Aufwand mittel, Jury-Schnitt 6). Geplant am 24.09.2026,
bevor eine Zeile entstand. Etappen #1–#10 sind gebaut und gepusht. Umfang von Tristan entschieden:
**nur Akten-Treffer, Verknüpfen mit jeder Akte (Vorgang vorgewählt), nur hinzufügen.**

## Kontext

Eine Recherche endet auf `/suche` in einer Trefferliste. Wer daraus acht Personen einem Vorgang zuordnen,
verschlagworten oder beobachten will, öffnet acht Akten und klickt achtmal. Die Dienste dafür gibt es alle
(`LinkService`, `TagService`, `WatchlistService`), nur der Weg vom Suchergebnis dorthin fehlt.

Ergebnis dieser Etappe: **Auswählen** schaltet die Trefferliste in einen Auswahlmodus. Akten-Treffer lassen
sich anhaken (einzeln oder je Gruppe), die Auswahl überlebt weitere Suchen — man kann über mehrere Suchbegriffe
sammeln —, und eine Leiste am Fuß bietet **Verknüpfen …**, **Stichwort …** und **Beobachten** für alle
markierten Akten auf einmal. Danach meldet eine Zeile, was passiert ist
(„5 verknüpft · 2 bestanden schon · 1 nicht erlaubt").

## Der Befund aus dem Code

- **Keiner der drei Schreibwege prüft selbst die Schreibberechtigung.** Nur der `ReadOnlyBarrierInterceptor`
  verweigert Nur-Lese-Aufsicht, Partner und Demo-Besucher beim Speichern. Dazu:
  - `LinkService.CreateAsync` prüft die Sichtbarkeit nur am **Ziel** und nur für eine feste Typliste, die
    Quelle nie; `KnownTypes` wird nicht erzwungen.
  - `TagService.SetAsync` prüft keine Sichtbarkeit, keinen Typ — und **ersetzt** die Stichworte einer Akte.
  - `WatchlistService.FollowAsync` fragt `Visibility.IsRecordVisibleAsync`. Das beantwortet unbekannte Typen
    (auch `PersonDoc`, `Observation`) mit „sichtbar" und prüft bei `Agent` nur den Rang, nicht ob es ihn gibt.
- **Ein `SaveChanges` je Akte wäre falsch.** `WatchlistChangeInterceptor` stößt je Speichern eine
  fire-and-forget-Benachrichtigung an; `WatchlistFanout` entdoppelt nur gegen schon **ungelesene** Meldungen.
  Acht schnelle Saves rennen dagegen und melden den Beobachtern des Vorgangs mehrfach. Ein Save sammelt die
  Akten in einem `HashSet` — eine Welle, jede Akte einmal.
- **Audit ist nur für Stichworte Handarbeit.** `Link` und `WatchlistEntry` sind `IAuditable`; über normales
  `SaveChanges` protokolliert sie der Interceptor. `TagMapping` ist es nicht — dort schreibt schon heute
  `SetAsync` eine `ManualAudit.Row` je Akte. Der Backlog-Hinweis („je Akte ein `ManualAudit.Row`") gilt also
  nur für Stichworte; für Verknüpfung und Beobachtung wäre er eine Doppelzeile.
- **Nicht jede Akte taugt als Verknüpfungsziel für viele.**
  - Operation und Taskforce erlauben auf ihrer Seite nur Person, Fraktion, Gruppe und Partei (`OrgTypes`,
    zweimal lokal kopiert in `OperationInvolvedPanel`/`TaskforceRelationsPanel`) und zeigen jede
    `Default`-Verknüpfung unter „Beteiligte".
  - Fraktion, Gruppe, Partei führen nur Konflikt und Bündnis (`OrgRelationsPanel`); Dokument, Personalakte und
    Ticket haben gar keine Verknüpfungsliste — dort angelegte Links sähe man nur von der Gegenseite.
  - Ein Link **Bürgerhinweis → Person** heißt für `TipTakeoverService.OldestLinkedPersonAsync` „schon in eine
    Personenakte übernommen" und blockierte die spätere Übernahme.
- **Stichwort-Chips und Stichwort-Suche decken sich nicht.** `TagChips` steht auf elf Aktenseiten, der
  Stichwort-Filter der Suche kennt nur die neun Kategorien mit `SearchTraits.Tagged` — Dokument und Termin
  fehlen (die Suchseite sagt das selbst im Hinweis am Filter).
- **Richtung einer Verknüpfung ist fast egal.** Alle Panels lesen über `GetForRecordAsync` beide Richtungen,
  der Zeitstrahl über `Counterpart`, der Graph zeichnet keine Pfeile. Nur die Chronik hängt einen Link an
  seine Quelle — und der Hinweis-Fall oben.
- **Die Bedrohungs-Neuberechnung** läuft heute je Link für beide Enden und kostet je Person 15–20 Abfragen.
  Mit einer Person als gewählter Akte und 30 Treffern liefe sie 30-mal für dieselbe Person.
- **Vorbild Oberfläche:** `EvidenceItemsPanel.razor` — *Auswählen*/*Auswahl beenden*, im Auswahlmodus
  toggelt der Kachelklick, klebende Leiste `.asservat-aktionsleiste`. Zwei Dinge daran nicht übernehmen: die
  `-1rem`-Marge gehört zum Rail-Pane (auf `/suche` gibt es keins), und eine `tabindex`-Kachel scrollt bei
  Leertaste die Seite — Blazor kann das nicht für eine Taste allein abfangen.
- **Vorbild Dienst:** `FactionService.MembersBulkApplyAsync` → `BulkMemberResult` (Zähler), die `MembersPanel`
  zu einer Meldung macht.

## Entscheidungen

**1. Nur Akten-Treffer sind auswählbar.** Trefferform `Record` und kein `TargetType`. Kommentar-, Quellen- und
Dok-Treffer zeigen auf ihre Elternakte und kommen je Akte mehrfach vor; ausgewählt ist immer genau die Zeile,
die man sieht. Protokoll-, Verwaltungs- und persönliche Treffer bleiben außen vor.

**2. Die Typregeln gehören dem Server.** `Services/RecordBatch.cs` (statisch, wie `QuickCapture`) hält, welcher
Typ was kann; die Dienste erzwingen es, die Suchseite fragt es nur ab:
- `Linkable` — `LinkService.KnownTypes` mit Trefferform `Record` und Route.
- `LinkAnchors` — was als *gewählte Akte* taugt, weil es selbst eine Verknüpfungsliste führt: Vorgang,
  Operation, Taskforce, Aufgabe, Person, Besprechung, Gesetz, Bewerbung. Damit ist „jede Akte" aus der
  Abstimmung eingelöst, soweit es die Seiten tragen; die Gründe stehen oben im Befund.
- `LinkTargetsFor(anchor)` — für Operation und Taskforce nur `LinkService.InvolvedTypes` (Person, Fraktion,
  Gruppe, Partei; die beiden `OrgTypes`-Kopien ziehen dorthin um), sonst `Linkable`.
- `Taggable` — `SearchCatalog.Clrs(SearchTraits.Tagged)`, also die neun, die der Stichwort-Filter findet.
- `Followable` — die acht Typen mit `FollowButton`.
- `Max = 50` Akten je Aktion; `ExistsAndVisibleAsync` = `Visibility.IsRecordVisibleAsync` plus Existenzprüfung
  für `Agent`. Jeder Typ in den Mengen muss eine echte Sichtbarkeitsregel haben — ein Test hält das.

**3. Drei Sammel-Schreibwege, je ein Kontext und ein `SaveChanges`.** Als Methoden an den Diensten, denen die
Tabellen gehören (Duplikatregel, Reaktivierung, Audit bleiben an einer Stelle). Jede beginnt mit
`Permission.RequireWriteAccess`. Getrackte Entitäten, kein `ExecuteUpdate` — Audit- und Beobachtungs-
Interceptor laufen mit.
- `ILinkService.CreateManyAsync(anchorType, anchorId, targets, label, actor, ct)`: gewählte Akte = Quelle
  (wie `CaseContentPanel`, `TicketConversionService`). Anker nicht erlaubt oder unsichtbar ⇒ Wurf ohne
  Schreiben. Je Ziel: außerhalb `LinkTargetsFor`, die Akte selbst oder unsichtbar ⇒ *verweigert*; schon
  verknüpft (eine Abfrage, beide Richtungen, gleiche Art, automatische Kollegen-Links zählen mit, gelöschte
  nicht) ⇒ *unverändert*. Bezeichnung über 200 Zeichen ⇒ Wurf (die Spalte fasst 200, `CreateAsync` prüft das
  bisher nicht). Danach **eine** Neuberechnung je beteiligter Fraktion/Person, `best effort` — die Links sind
  gespeichert, der tägliche Sweep holt einen Ausfall nach.
- `ITagService.AddManyAsync(records, tagIds, actor, ct)`: fügt nur hinzu, nimmt nie weg; unbekannte Tag-Ids
  fallen raus (keine übrig ⇒ Wurf); je **geänderter** Akte eine `ManualAudit.Row` „Tags hinzugefügt" mit genau
  den neuen Namen.
- `IWatchlistService.FollowManyAsync(records, actor, ct)`: schon gefolgt ⇒ *unverändert*, entfolgt ⇒
  reaktivieren, sonst anlegen.
- Rückgabe `BatchOutcome(Done, Unchanged, Refused)`.

**4. Auswahlmodus statt Dauer-Kästchen.** *Auswählen* im Ergebniskopf, nur mit `MayWrite()` — Nur-Lese-Aufsicht,
Partner und Demo-Besucher sehen den Knopf nicht, jeder Handler kehrt ohne das Recht früh zurück. Im Modus
navigiert keine Zeile; ein Klick toggelt, ein echtes `MudCheckBox` (in einem `@onclick:stopPropagation`-Span)
ist das Tastaturziel, nicht wählbare Zeilen sind gedimmt und `aria-disabled`. Ein kleines Symbol öffnet die Akte
in einem neuen Tab — wegnavigieren verlöre die Auswahl.

**5. Die Auswahl bleibt stehen.** Über Facettenwechsel, Modus-Schalter, neue Suchen und nach einer Aktion: die
Aktionen sind idempotent und werden verkettet (verknüpfen, dann verschlagworten, dann beobachten). Geleert wird
nur über *Keine* oder *Auswahl beenden*. Die Leiste sagt, wie viele davon nicht in der aktuellen Trefferliste
stehen, und listet die Auswahl in einem Menü zum Einzeln-Entfernen.

## Was bewusst **nicht** gebaut wird

- **Kein Entfernen** (Stichwort weg, Beobachtung beenden, Verknüpfung lösen) — das bleibt an der einzelnen Akte.
- **Keine Inhalts-Treffer** als Stellvertreter ihrer Akte.
- **Keine Mehrfachauswahl in den Listen** (Personen, Fraktionen, …) — die Suche ist die eine Trefferoberfläche.
- **Kein generisches Leisten-Bauteil.** Die Evidenz-Leiste bleibt, wie sie ist; die neue bekommt eine eigene
  CSS-Klasse ohne Rail-Marge.
- **Keine Nachrüstung der Einzelwege** (`CreateAsync`, `SetAsync`, `FollowAsync` ohne eigenen Schreib-Guard).
  Der Schreibschutz-Interceptor hält sie; nachgezogen wird das getrennt, nicht als Nebenwirkung.

## Aufbau

1. **Plan festschreiben** — diese Datei.
2. **Ergebnisform** — `Models/Common/BatchOutcome.cs`.
3. **Regeln** — `Services/RecordBatch.cs`; `LinkService.InvolvedTypes`, beide `OrgTypes` darauf umstellen.
4. **Dienste** — `CreateManyAsync`, `AddManyAsync`, `FollowManyAsync` (Interface zuerst, trailing
   `CancellationToken`, Primary Constructors bleiben unverändert).
5. **Treffer-Regeln** — `Services/Search/SearchSelection.cs`: `Key`, `IsSelectable`, `Supports`, `Refs`,
   `Summary` (deutsch, Singular/Plural, Nullteile weg).
6. **Dialoge** — `LinkDialog` bekommt `InitialType` (fällt auf den ersten angebotenen Typ zurück);
   `TagPickerDialog.ShowAsync(…, title = "Tags zuordnen")`.
7. **Oberfläche** — `SearchHitRow` (`SelectMode`, `Selectable`, `Picked`, `OnToggle`), `SearchPage`
   (`_mayAct`, `_selectMode`, `_picked`, *Alle dieser Gruppe*, Leiste am Seitenende außerhalb der
   Lade-/Ergebniszweige), neu `Components/Pages/Search/Shared/SearchSelectionBar.razor` (Anzahl, „n nicht in
   dieser Trefferliste", Menü, *Alle*/*Keine*, *Verknüpfen … (6)*, *Stichwort … (5)*, *Beobachten (8)*,
   Doppelklick-Sperre, Ergebnis als Snackbar), `app.css`: `.auswahl-aktionsleiste`,
   `.suche-treffer-gewaehlt`, `.suche-treffer-inaktiv`. Beim Verknüpfen: `LinkDialog` mit
   `allowedTypes: LinkAnchors`, `InitialType: Case`, `excluded` = Auswahl je Typ, Beschriftung
   „Bezeichnung für alle (optional)"; Treffer außerhalb `LinkTargetsFor` zählen als übersprungen.
8. **Changelog** `2.2.11-mehrfachauswahl` (Neu, „Bedienung"), **Handbuch** `art-suchen` um Absatz und Schritt
   ergänzt ⇒ `HandbookContent.Revision` 15 → 16, **IdeenBacklog** #11 als umgesetzt, **CLAUDE.md**-Gotcha.
9. **Bauen, gezielt testen, sabotieren, Browser, Review, Commit, Push.**

## Tests (kein bUnit)

`SqliteTestContext` hat keine Interceptors — die Schreib-Guards werden also im Dienst selbst getestet.

- `SearchSelectionTests` (rein): Person wählbar; Kommentar→Person, Protokoll, Handbuch-Artikel nicht; Gesetz nur
  Verknüpfen; Aktionsmatrix; `Summary` Singular/Plural, Nullteile weg.
- `RecordBatchTests` (rein + Quelltext-Scan): `Linkable` ⊆ `KnownTypes`, alle mit Trefferform `Record` und Route,
  ohne Dok/Observation/Tagesordnungspunkt; `LinkAnchors` ⊆ `Linkable`, ohne Hinweis, Ticket, Fraktion, Dokument,
  Personalakte; `LinkTargetsFor(Operation)` = `InvolvedTypes`; `Taggable` = Tagged-Kategorien und ⊆ Typen mit
  `<TagChips>`; `Followable` = Typen mit `<FollowButton>`; beide Org-Panels nutzen `InvolvedTypes`;
  `Normalize` entdoppelt, wirft über dem Deckel.
- `RecordBatchVisibilityTests`: Theory über jeden Typ der drei Mengen — eine nicht vorhandene Id wird auch für
  einen Director verweigert. Hält die offene „unbekannt ⇒ sichtbar"-Lücke und die Personalakten-Lücke zu.
- `LinkServiceTests` (+): Quelle = gewählte Akte, Art `Default`, nicht automatisch, Bezeichnung getrimmt;
  bestehend in beiden Richtungen und automatischer Kollegen-Link ⇒ unverändert; gelöschter ⇒ neu angelegt;
  die Akte selbst ⇒ verweigert, der Rest geht durch; Junior: VS-Person, fremde Taskforce ⇒ verweigert;
  Operation mit Vorgangs-Treffer ⇒ verweigert; gewählte Akte unsichtbar, ein Dok oder eine Fraktion ⇒ Wurf ohne
  Zeilen; Nur-Lese, Partner, Demo ⇒ Wurf ohne Zeilen; über Deckel, Bezeichnung über 200 ⇒ Wurf; genau ein Save;
  Neuberechnung je Person einmal; eine werfende Neuberechnung lässt die Links stehen.
- `TagServiceTests` (+): fügt hinzu, nimmt nie weg; eine Audit-Zeile je geänderter Akte nur mit den neuen Namen,
  keine für unveränderte; Dokument, Termin, Gesetz und VS für Junior ⇒ verweigert; unbekannte Tags raus, alle
  unbekannt ⇒ Wurf; Nur-Lese, Partner, Demo ⇒ Wurf; ein Save.
- `WatchlistServiceTests` (+): anlegen, reaktivieren, schon gefolgt ⇒ unverändert; Aufgabe, Dokument,
  VS-Vorgang für Junior, Personalakte für Nicht-Führung, fehlende Personalakte für Director ⇒ verweigert;
  Nur-Lese, Partner, Demo ⇒ Wurf; nur eigene Zeilen berührt.
- Jede neue Schranke einmal sabotieren (Schreib-Guard, Sichtbarkeit, Personalakten-Existenz, Duplikat-Abfrage,
  einzelner Save, `Taggable` auf die Chip-Menge) — Sicherung im Scratchpad, nie `git checkout`.
- Volle Suite unter `LANG=de_DE.UTF-8`; Bau 0 Fehler (Ausgangsstand 39 Warnungen).

## Abnahme im Browser (Klickstrecke)

App gegen MariaDB mit Demo-Daten. Der Demo-Besucher darf nicht schreiben ⇒ **kein** *Auswählen*-Knopf — das ist
selbst ein Prüfpunkt. Für die Schreibstrecke wie in Etappe 10 **nur lokal** vorübergehend lockern, danach
zurückbauen und per `git diff` prüfen.

1. Suche ⇒ *Auswählen* ⇒ drei Personen und ein Kommentar-Treffer: der Kommentar ist gedimmt und nicht wählbar;
   Leertaste auf dem Kästchen scrollt nicht.
2. Zweite Suche, eine weitere Person dazu ⇒ „4 ausgewählt · 3 nicht in dieser Trefferliste".
3. *Verknüpfen …* ⇒ Dialog öffnet mit *Vorgang* ⇒ „4 verknüpft", der Vorgang zeigt sie; noch einmal ⇒
   „4 bestanden schon".
4. *Stichwort …* ⇒ vorhandene Stichworte bleiben, das neue kommt dazu; der Stichwort-Filter findet alle vier.
5. *Beobachten* ⇒ `/beobachtet` listet sie. Beobachter des Vorgangs bekommen aus Schritt 3 **eine** Meldung.
6. Handybreite: die Leiste bricht um, nichts ist abgeschnitten. Im App-Log kein `fail:`.
