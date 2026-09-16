# Design: Aktenarchiv — dritter Zustand neben aktiv und Papierkorb

> Stand: 2026-09-16 · Sprache: Deutsch · Backlog-Punkt: „Archiv statt Dauerbestand" (`IdeenBacklog.md:1141`)

## 1. Ziel & Kontext

Eine Akte kennt heute genau zwei Zustände: **aktiv** oder **im Papierkorb** (`ISoftDelete`). Alles,
was je angelegt wurde, steht damit für immer in jeder Liste, jeder Auswahlliste und jeder Suche.
Aufgelöste Fraktionen, dauerhaft tote Personen und abgeschlossene Vorgänge verdrängen den aktiven
Bestand, halten die Aktualitäts-Ampel dauerhaft rot und verwässern die Statistik.

Das Archiv ist der **dritte Zustand**: die Akte bleibt vollständig erhalten und lesbar, verschwindet
aber aus dem, was den *aktiven Bestand* aufzählt. Ein Klick holt sie zurück.

**Archiv ist nicht Papierkorb.** Der Papierkorb ist ein Löschvorgang mit Reue-Fenster (Führung holt
zurück); das Archiv ist eine Aussonderung im laufenden Betrieb, die jeder schreibberechtigte Agent
selbst vornimmt und rückgängig macht. Die beiden Achsen sind unabhängig: eine archivierte Akte kann
zusätzlich gelöscht werden und liegt dann im Papierkorb.

## 2. Festgelegte Entscheidungen

| Thema | Entscheidung |
|---|---|
| Wirkung | **Sanft.** Raus aus Listen, Auswahllisten/Pickern und Standardsuche. Detailseite, Verknüpfungen, Erwähnungen, Zeitstrahl und Graph bleiben unverändert. |
| Umfang | **Sieben Aktentypen:** Person, Faction, PersonGroup, Party, Case, Operation, Taskforce. |
| Auslöser | **Nur von Hand.** Keine Frist, kein Worker, keine Automatik. |
| Recht | **Jeder schreibberechtigte interne Agent** (`Permission.RequireWriteAccess`) archiviert und holt zurück. Nur-Lese-Aufsicht, Partner und Demo-Besucher nicht. |
| Zugang | **Filter in der jeweiligen Liste + Facette in der Suche.** Keine Sammelseite `/archiv`. |
| Ausgeklammert | Aktualitäts-Ampel, Statistik/Dashboard/Lageberichte, Bedrohungs-Score-Lauf. |
| Graph & Zeitstrahl | **Nicht** ausgeklammert — Knoten bleibt, wird ausgegraut. |
| Bearbeiten | **Bleibt erlaubt.** Hinweisband statt Schreibsperre; Bearbeiten hebt das Archiv nicht auf. |
| Kaskade | **Keine.** Eine archivierte Fraktion nimmt Mitglieder, Vorgänge und Aufgaben nicht mit. |
| Öffentlicher Bereich | Unberührt. Eine laufende Ausschreibung bleibt laufend; der Dialog **warnt**, blockiert nicht. |

## 3. Warum kein globaler Query-Filter

Der Reflection-Filter für `ISoftDelete` (`Data/AppDbContext.cs:2182`) ist die mechanisch nächste
Vorlage — und für „sanft" die falsche. Ein zweiter globaler Filter würde die Akte auch aus der
Detailseite, dem Verknüpfungs-Panel, der Erwähnungs-Auflösung und dem Graph tilgen: Mitgliederlisten
liefen still leer, `@{Person:GUID}` zeigte nichts mehr an, Kanten zwischen zwei aktiven Akten
verschwänden. Dazu käme die in `CLAUDE.md` dokumentierte Falle, dass `IgnoreQueryFilters()` für die
ganze Kompilierung gilt — jede Archiv-Abfrage müsste den Soft-Delete-Gürtel von Hand nachbauen.

Stattdessen das Muster, das im Projekt schon trägt (`Services/AgentSelection.cs`,
`Services/RecordVisibility.cs`): **ein Marker-Interface, ein zentraler statischer Helfer, explizit
angewandt an den Stellen, die einen Bestand aufzählen.** Die Menge dieser Stellen ist endlich,
benennbar und in §5 vollständig aufgeführt.

## 4. Datenmodell

### 4.1 Marker-Interface

`NOOSE-Website/Models/Abstractions/IArchivable.cs` — neben `IAuditable` und `ISoftDelete`:

```csharp
/// <summary>Marks a record as archivable; filtered out of stock listings, fully readable elsewhere.</summary>
public interface IArchivable
{
    bool IsArchived { get; set; }
    DateTime? ArchivedAt { get; set; }
    string? ArchivedById { get; set; }
    string? ArchiveReason { get; set; }
}
```

Implementiert von `Person`, `Faction`, `PersonGroup`, `Party`, `Case`, `Operation`, `Taskforce`.

Spalten (deutsch, wie im Rest des Modells): `IstArchiviert` (`bool`, nicht nullbar, Default `false`),
`ArchiviertAm` (`datetime?`, UTC), `ArchiviertVonId` (`string?`, FK auf `Agent`, `DeleteBehavior.Restrict`),
`Archivgrund` (`string?`, 300 Zeichen).

### 4.2 Zentraler Helfer

`NOOSE-Website/Services/RecordArchive.cs` — statisch, nicht DI-registriert, neben `RecordVisibility.cs`:

```csharp
public static class RecordArchive
{
    public static IQueryable<T> OnlyActive<T>(this IQueryable<T> query) where T : class, IArchivable
        => query.Where(x => !x.IsArchived);

    public static IQueryable<T> OnlyArchived<T>(this IQueryable<T> query) where T : class, IArchivable
        => query.Where(x => x.IsArchived);

    public static IQueryable<T> Apply<T>(this IQueryable<T> query, ArchiveFilter filter) where T : class, IArchivable
        => filter switch { ArchiveFilter.Active => query.OnlyActive(), ArchiveFilter.Only => query.OnlyArchived(), _ => query };

    public static bool IsActive(IArchivable record) => !record.IsArchived;
}
```

**Nie `.Where(x => !x.IsArchived)` von Hand** — dieselbe Regel wie bei `AgentSelection`. Wer eine
Ausnahme braucht, begründet sie dort, wo sie steht.

`NOOSE-Website/Models/Enums/ArchiveFilter.cs`: `Active = 0` (Standard), `Including = 1`, `Only = 2`.

### 4.3 Migration

`Phase81_Archiv` — vier Spalten plus Index auf `IstArchiviert` für `Personen`, `Fraktionen`,
`Personengruppen`, `Parteien`, `Vorgaenge`, `Operationen`, `Taskforces`.

### 4.4 Schreibweg

Je Dienst zwei Methoden, gleiche Form über alle sieben Typen:

```csharp
Task ArchiveAsync(string id, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
Task UnarchiveAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
```

Ablauf, in dieser Reihenfolge:

1. `Permission.RequireWriteAccess(actor)` als **erste** Anweisung.
2. `ExecuteUpdateAsync` auf die vier Spalten.
3. `ManualAudit.Row(nameof(T), id, …)` mit `ChangesJson` in der `{Feld:[alt,neu]}`-Form.

**Warum Bulk-Update statt Tracker:** der `AuditSaveChangesInterceptor` würde `GeaendertAm` stempeln.
Eine zurückgeholte Akte sähe dann frisch bearbeitet aus, obwohl niemand sie angefasst hat — und die
Aktualitäts-Ampel wäre für jede archivierte und wieder aktivierte Akte zurückgesetzt. Der Preis:
`ExecuteUpdateAsync` umgeht auch den `ReadOnlyBarrierInterceptor`, deshalb ist der Guard in Schritt 1
nicht optional, und der Audit-Eintrag in Schritt 3 muss von Hand geschrieben werden (beides genau so
in `CLAUDE.md` gefordert).

**Zwei neue Werte in `AuditAction`:** `Archived = 4`, `Unarchived = 5`. Mit `Modified` hieße der
Eintrag auf Zeitstrahl und Chronik „Akte geändert" und wäre auf `/nachweis` nicht filterbar. Drei
Stellen ziehen mit — alle drei haben einen `_`-Zweig, der Fehler wäre also ein roher englischer
Enum-Name statt eines Absturzes:

- `AuditActionDisplay.All` (Filterleiste), `.Name` („Archiviert" / „Aus dem Archiv geholt"),
  `.Colour` (beide `Color.Default`).
- `TimelineDisplay.MapAudit` — die `Verb`-Funktion um beide Fälle erweitern.
- Die acht `*HistoryTimeline`-Komponenten prüfen, ob sie über `AuditAction` verzweigen.

## 5. Durchsetzungsstellen

### 5.1 Gefiltert wird

| Stelle | Änderung |
|---|---|
| `GetListAsync` der sieben Dienste | neuer Parameter `ArchiveFilter filter = ArchiveFilter.Active` |
| `SearchAsync` der sieben Dienste | immer `OnlyActive()` — speist `LinkDialog.razor:538-558`, `QuickAddDialog`, alle Picker |
| `SearchQuery` | neues Feld `IncludeArchived` (Form wie `Fuzzy`/`Deep`, `Models/Common/SearchQuery.cs`) |
| Suchanbieter der sieben Typen | `RecordSearchProviders.cs`, `OperationsSearchProviders.cs`: `OnlyActive()` außer bei `query.IncludeArchived` — **in `SearchAsync` und `ResolveIdsAsync`**, sonst holt die phonetische Zweitwelle über `SearchSideIndex` genau die Akten zurück, die die erste Welle ausgelassen hat |
| `SearchService.QuickSearchAsync` | baut die `SearchQuery` selbst ⇒ Kommandopalette, `MentionService.CandidatesAsync:221` und die Kompromittierungs-Felder sind mit dem Standardwert erledigt |
| `DashboardService` | Kacheln, Gefährdungsliste und „veraltet"-Zähler auf `OnlyActive()` |
| `Services/Statistics/*` | `StatisticsService`, `ThreatStatisticsService`, `ThroughputStatisticsService`, `NetworkStatisticsService` und der Lagebericht zählen nur aktive Akten |
| `ThreatScoreService.CalculateAll*` | archivierte Personen und Fraktionen überspringen; der zuletzt berechnete Wert bleibt an der Akte |
| `RecencyService` / `FactionRecency` | archiviert wirkt wie `VeralterungDeaktiviert` — keine Ampel, keine „veraltet"-Liste |

### 5.2 Bewusst unberührt

Detailseite, Editor, Druckansicht · `LinkPanel`/`LinkService`/`RecordsReference` (bestehende
Verknüpfungen bleiben sichtbar) · Erwähnungs-**Auflösung** (`@{Typ:GUID}` zeigt weiter den Namen;
nur der @-Picker bietet archivierte nicht mehr an) · Graph und Chronik-Feed (ausgegraut statt
entfernt) · Papierkorb und `TrashService` (zweite Achse) · der gesamte öffentliche Bereich ·
NOOSEI-Werkzeug `lies_akte` (liest weiter; `suche_akten` findet archivierte nicht mehr, weil es über
`SearchQuery` läuft).

Der Suchindex selbst bleibt ebenfalls unberührt: archivierte Akten werden weiter projiziert und
indiziert, damit die Facette sie finden kann. `SearchIndexProjection` bekommt keinen neuen Typ,
also bleibt `SearchIndexBackfillWorker.Version` stehen.

### 5.3 Vorhandener Vorgangs-Status

`CaseStatus.Archived` (`Models/Enums/CaseStatus.cs:15`) existiert heute als reine Beschriftung und
nimmt den Vorgang aus keiner Liste. Er wird **nicht** zur zweiten Achse: setzt jemand einen Vorgang
auf `Archiviert`, schlägt der Editor das Archivieren vor (Hinweis mit Knopf), führt es aber nicht
selbsttätig aus. Umgekehrt setzt das Archivieren den Status nicht. Zwei Felder, eine Richtung
Vorschlag — kein stiller Gleichlauf, der beim Zurückholen wieder auseinanderfiele.

## 6. Oberfläche

- **Detailseite** (7×): Knopf *Archivieren* neben *Löschen* (`PersonDetail.razor:61` als Vorlage),
  Dialog mit optionalem Grund. Ist die Akte archiviert, steht oben ein `MudAlert` in `Severity.Info`:
  „Archiviert am … von … — diese Akte erscheint nicht in Listen, Auswahllisten und der
  Standardsuche.", mit Knopf *Zurückholen* und dem Grund als Untertext.
- **Liste** (7×): Filter *Archiv* (`Aktiv` / `Mit Archiv` / `Nur Archiv`), über `QueryState` in der
  URL gehalten wie `einstufung` und `aktualitaet` heute (`PeopleList.razor:153-155`). Archivierte
  Zeilen tragen einen grauen Chip *Archiviert*.
- **Suche**: Facette *Archiv einschließen* neben den vorhandenen Schaltern; setzt
  `SearchQuery.IncludeArchived`.
- **Graph**: archivierte Knoten ausgegraut (`graph.js` — `?v=` hochzählen, alle Importstellen).

## 7. Tests

xunit auf In-Memory-SQLite, keine Datenbank nötig. Je Aktentyp:

1. Archivierte Akte fällt aus `GetListAsync` und aus `SearchAsync`.
2. `ArchiveFilter.Including` bringt sie zurück, `ArchiveFilter.Only` liefert nur sie.
3. Sie bleibt über `GetByIdAsync` und `RecordsReference` erreichbar (die „sanft"-Zusage).
4. Suchanbieter: ohne `IncludeArchived` kein Treffer, mit Flag ein Treffer — **beide Wellen**, also
   auch der Weg über `SearchSideIndex` → `ResolveIdsAsync`. Suchtests mit `MaxConcurrency = 1`
   konstruieren (`SearchTestHost`), weil `SqliteTestContext` allen Kontexten dieselbe offene
   Verbindung gibt.

Dazu quer über alle Typen:

5. **`GeaendertAm` bleibt beim Archivieren und Zurückholen unverändert** — daran hängt der
   Bulk-Update-Weg aus §4.4.
6. Ein Audit-Eintrag entsteht, gegen die Akte geloggt, mit lesbarem `ChangesJson`.
7. Nur-Lese-Aufsicht, Partner und Demo-Principal werden mit `UnauthorizedAccessException` abgewiesen.
8. Der Score-Sweep überspringt archivierte Fraktionen und Personen.
9. Archiviert und gelöscht stören sich nicht: eine archivierte Akte lässt sich löschen, liegt dann im
   Papierkorb und kommt beim Wiederherstellen archiviert zurück.

## 8. Handbuch & Changelog

- Handbuch-Artikel **„Akten archivieren"** im Kapitel zur Aktenführung, mit dem Rollenspiel-Kasten
  (Aussonderung als Verwaltungsvorgang). Neuer Artikel ⇒ kein `HandbookContent.Revision`-Bump nötig.
- Glossarbegriff **„Archiv"** — mit ausdrücklicher Abgrenzung zum Papierkorb; das ist die
  Verwechslung, die sonst kommt.
- Eine Changelog-Zeile in `ChangelogContent.cs`, Alltagssprache, ohne Technik.

## 9. Nicht Teil dieser Arbeit

Keine Sammelseite `/archiv`, keine automatische Frist und kein Worker, keine Vorschlagsliste für
Archiv-Kandidaten, keine Massenaktion, keine Schreibsperre auf archivierten Akten, keine Kaskade auf
abhängige Akten, keine Archiv-Wirkung im öffentlichen Bereich. Die Mechanik aus §4 trägt jedes davon
als spätere Erweiterung; heute wäre es Aufwand ohne Nutzer.
