# Etappe 7 — Seitenindex erweitern + Ctrl+K-Palette

> **Status: umgesetzt.** Build grün, **5030 Tests grün**. Drei Abweichungen vom ursprünglich geschriebenen Plan,
> jeweils begründet — siehe „Abweichungen" am Ende. **Nicht** verifiziert ist alles, was einen App-Start braucht:
> der manuelle Durchlauf unten steht noch aus (MariaDB/XAMPP lief hier nicht).

Letzte offene Etappe des Such-Umbaus (`~/.claude/plans/bitte-stelle-sicher-das-adaptive-deer.md`).
**Keine Lücke, sondern Verbesserung:** die Suche ist vollständig und getestet; hier geht es um Trefferquote bei
Tippfehlern und um die Bedienbarkeit der Befehlspalette.

---

## Kontext

Zwei Dinge, die der Umbau bewusst offen gelassen hat:

1. **Der phonetische Seitenindex deckt 8 von 58 Kategorien.** Er ist der Pfad, der `Maier`↔`Meyer` findet — was
   Levenshtein auf dem Rohstring verpasst (Editierdistanz 3, gleicher Kölner-Phonetik-Code). Personalakten, Gesetze,
   Asservate und Informanten sind seit Etappe 8 durchsuchbar, aber **nicht** phonetisch: wer „Maier" tippt, findet
   den Agenten „Meyer" nicht.
2. **Die Palette bietet 11 Kategorien und sagt nicht, was sie anbietet.** Ein Datensatz-Treffer und ein statischer
   Navigations-Befehl sehen in derselben Liste gleich aus, und es gibt keinen Weg von der Palette in die volle Suche.
   Mit 58 Kategorien ist eine 11er-Auswahl nur vertretbar, wenn ein Trichter daneben steht.

---

## Was aus dieser Etappe schon erledigt ist

Beim Orchestrator-Umbau (Etappe 5) mitgekommen — **nicht erneut anfassen**:

| Erledigt | Wo |
|---|---|
| `LoadSideHitsAsync` und `SideIndexLabels` gelöscht (die zweite, abweichende Kopie jeder Sichtbarkeitsregel) | `SearchService.cs` neu geschrieben |
| Seitenindex-Auflösung geht über `ISearchProvider.ResolveIdsAsync`, nicht über einen eigenen Switch | `Services/Search/SearchSideIndex.cs` |
| `ResolveIdsAsync` implementiert auf allen 8 aktuell indizierten Providern | `Providers/RecordSearchProviders.cs`, `OperationsSearchProviders.cs` |
| `QuickAsync` implementiert auf allen 11 `Quick`-Kategorien | dieselben + `KnowledgeSearchProviders.cs`, `PersonnelSearchProviders.cs` |
| Hartes Palette-Budget `QuickBudgetMs = 300`, Round-Robin über die Kategorien, stilles Degradieren auf statische Befehle | `SearchOptions`, `SearchService.QuickSearchAsync`, `SearchProviderKit.Shuffle` |
| `SearchIndexBackfillWorker.Version` als Marker-Mechanik **plus** der `Add`-statt-Update-Bug behoben | `Infrastructure/Search/SearchIndexBackfillWorker.cs` |
| Invariante `SideIndexed ⇒ !Heavy` als Test | `SearchCatalogTests` |

**Ist-Stand, gemessen:** `SideIndexed` auf 8 Kategorien (Person, Faction, PersonGroup, Party, Operation, Case,
Taskforce, Job) · `SearchIndexProjection.IndexedTypes` = dieselben 8 + `PersonAlias` (zahlt auf die Person ein) ·
Backfill listet dieselben 9 Sets · `Version = 1` · `Quick` auf 11 Kategorien, alle 11 mit `QuickAsync`.

---

## Teil A — Vier Typen in den Seitenindex

### Die Regel, die die Auswahl bestimmt

**Namen und Identifikatoren indizieren, nie Fließtext.** `SearchIndexInterceptor` läuft in *jedem*
`SaveChangesAsync` der App; jede indizierte Änderung ist Delete-by-`SourceId` + Neuanlage aller Phonetik- und
Stamm-Zeilen. `SearchTokenizer.Stems(ContentHtml)` würde tausende Zeilen in die Transaktion des Nutzers schreiben.
Deshalb schließt `SideIndexed` per Test `Heavy` aus — und deshalb kommen Dokumente, Besprechungen und Kommentare
hier **nicht** dazu.

### Die drei

| Typ | Phonetik-Feld | Stamm-Felder | Warum |
|---|---|---|---|
| `Agent` | `Codename` | `Codename`, `BadgeNumber` | Personalakte und Palette wollen beide `Maier`↔`Meyer` |
| `Law` | `Title` | `Paragraph`, `Title`, `LawBook` | Paragraphen werden falsch erinnert, nicht falsch geschrieben — aber der Titel schon |
| `EvidenceItem` | `Name` | `Name`, `Category` | Asservat-Namen sind Freitext von Hand |

**Klarname NICHT in die Stamm-Felder.** Bei `Agent` wandert der `RealName` weder in Phonetik noch in Stämme: die
Index-Tabelle trägt kein Sichtbarkeits-Gate, und der Klarname ist führungsexklusiv. Nur `Codename`.
Ein Test hält das fest (`The_projection_indexes_an_agents_codename_but_never_their_real_name`).

### Informanten: bewusst gar nicht indiziert

Der Index leakt **nicht** in die App — `SearchSideIndex` löst Kandidaten-Ids über
`InformantVisibility.VisibleIdsAsync` auf, und was nicht auflöst, erzeugt keine Ausgabe. Trotzdem: der einzige
Informanten-Wert, der einen phonetischen Pass verdient, ist der Klarname der V-Person, und der hat in einer Tabelle
ohne Zugriffsschicht nichts zu suchen. Das Aktenzeichen stattdessen zu indizieren bringt nichts — ein exaktes
Aktenzeichen findet die LIKE-Suche längst. Also **kein** Projektions-Arm, mit Begründung im Code und einem Test
(`The_projection_leaves_informants_out`), damit es niemand als Versehen „nachträgt".

### Nebenwirkung, die im Kommentar stehen muss

`Agent` ist die Identity-User-Tabelle. Sie wird bei **jedem Login** geschrieben (SecurityStamp-Rotation), bei jeder
Rang-/Flag-Änderung und bei jeder Namensänderung. Indizieren heißt: der Interceptor re-tokenisiert den Codename
innerhalb der Login-Transaktion. Bei einem Feld von ~20 Zeichen ist das ein paar Zeilen — aber es steht dann im
kritischen Pfad des Logins, und das gehört als Kommentar an die Projektion.

### Schritte

Je Typ **fünf** Stellen, sonst ist es halb gebaut:

1. **`Services/SearchIndexProjection.cs`** — Arm in `For(object entity)` + Eintrag in `IndexedTypes`.
   ```csharp
   Agent a => Build(nameof(Agent), a.Id, a.Id,
       // Codename only: the real name is leadership-exclusive and the index table has no gate
       new[] { a.Codename }, new[] { a.Codename, a.BadgeNumber }),
   ```
2. **`Services/Search/SearchCatalog.cs`** — `SearchTraits.SideIndexed` an die vier Zeilen.
3. **Provider** — `ResolveIdsAsync` überschreiben. Muss **dieselbe** `Visible(...)`-Query benutzen wie `SearchAsync`;
   eine zweite Filterkopie ist genau der Fehler, den `LoadSideHitsAsync` hatte.
   - `AgentSearchProvider` (`PersonnelSearchProviders.cs`): Klarname-Regel über `AgentNameDisplay.Pick` mitnehmen.
   - `LawSearchProvider` (`OperationsSearchProviders.cs`): Partner-Zweig (`OnlyPartnerVisible`) mitnehmen.
   - `EvidenceItemSearchProvider` (`LedgerSearchProviders.cs`).
   - `InformantSearchProvider` (`PersonnelSearchProviders.cs`): `InformantVisibility.VisibleIdsAsync` mitnehmen.
4. **`Infrastructure/Search/SearchIndexBackfillWorker.cs`** — je Typ eine `IndexAllAsync`-Zeile.
   `db.Users` für `Agent`, `db.Laws`, `db.EvidenceItems`, `db.Informants`.
5. **`Version` von `1` auf `2`.** ⚠️ **Ohne das indiziert eine Bestandsinstallation null Zeilen für die neuen Typen**
   — der Guard kehrt früh zurück, sobald der Marker existiert, und die phonetische Suche wirkt monatelang „flaky".
   Die Mechanik ist da (Alt-Werte `"true"` parsen nicht und lösen einen Neulauf aus), nur die Zahl fehlt.

### Backfill-Verhalten nach dem Deploy

Der Lauf startet 45 s nach App-Start, löscht **beide** Index-Tabellen (`ExecuteDeleteAsync`) und baut neu auf. Bei
einem Bestand von einigen tausend Akten sind das Sekunden. In dem Fenster liefert die Smart-Suche nur
Levenshtein-Treffer, keine phonetischen — das ist akzeptabel und tritt genau einmal pro Version auf.

---

## Teil B — Ctrl+K-Palette

Alles in `Components/Common/Shared/CommandPalette.razor`.

### B1. Trichter-Eintrag

Zwischen den Datensatz-Treffern und dem NOOSEI-Eintrag:

```csharp
result.Add(new PaletteItem($"Alles durchsuchen: {v}", $"{SearchCatalog.Categories.Count} Kategorien",
    Icons.Material.Filled.ManageSearch, "/suche?q=" + Uri.EscapeDataString(v), PaletteKind.Search));
```

Diese eine Zeile ist, was eine 11-Kategorien-Palette gegenüber einer 58-Kategorien-Suche vertretbar macht.

### B2. Kennzeichnungs-Chips

`MudAutocomplete` hat keine Gruppen-Header, also muss die Unterscheidung in die Zeile:

```csharp
private enum PaletteKind { Favorite, Recent, Command, Record, Search, Noosei }
private record PaletteItem(string Label, string? Sub, string Icon, string Url, PaletteKind Kind);
```

Im `ItemTemplate` rechts ein `MudChip` mit `Badge(i)` → `"Befehl"` / `"Favorit"` / `"Zuletzt"` /
`SearchCatalog.German(category)` / `"Suche"` / `"NOOSEI"`. Ergebnis: `Meyer · NOOSE-P-42 · [Person]` gegen
`Personen-Akte · Neu anlegen · [Befehl]` — gleiche Zeilenform, unmissverständlicher rechter Rand.

Für den Typ-Chip muss die Kategorie bis ins Item durchgereicht werden; heute wird sie in `PaletteIcon(t.Category)`
verbraucht und weggeworfen.

### B3. `PaletteIcon` löschen

Der hand-geschriebene Switch (~8 Arme, mit Person-Fallback) wird zu `SearchCatalog.Icon(t.Category)`. Damit sind
Palette und Ergebnisliste garantiert gleich beschriftet, und eine neue Kategorie bringt ihr Icon selbst mit.

### B4. Was **nicht** angefasst wird

- **`app.js?v=`**: kein JS geändert, also kein Bump *nötig*. Beim Umsetzen kam heraus, dass
  `CommandPalette.razor` auf `v=2` stand, `FinancingCatalogPanel.razor` aber längst auf `v=3` — zwei `?v=` auf
  dasselbe Modul holen zwei Kopien. Das steht jetzt einheitlich auf `v=3`, und CLAUDE.md nennt die Regel
  „**alle** Importstellen mitziehen" ausdrücklich.
- **Das stille Degradieren auf statische Befehle** (`try`/`catch` + `LogDebug`) bleibt. Kein Snackbar: die Palette
  feuert beim Tippen, ein Fehler-Toast pro Tastendruck ist schlimmer als ein fehlender Vorschlag.
- **`Shuffle`** bleibt. Ohne das Round-Robin füllen Personen alle acht Plätze, sobald irgendeine Person passt.

---

## Teil C — Zwei fehlende Drift-Tests

Beide sind heute **nicht** vorhanden und decken je eine Halbfertig-Falle ab:

```csharp
[Fact]
public void Every_side_indexed_category_has_a_branch_in_the_projection()
{
    // ein indizierter Typ ohne Projektions-Arm schreibt nie Index-Zeilen: die Kategorie ist im Katalog
    // als phonetisch markiert und ist es nicht
    var projected = SearchIndexProjection.IndexedTypes.Select(t => t.Name)
        .Except([nameof(PersonAlias)], StringComparer.Ordinal)   // zahlt auf Person ein
        .ToHashSet(StringComparer.Ordinal);

    Assert.Equal(SearchCatalog.Clrs(SearchTraits.SideIndexed).ToHashSet(StringComparer.Ordinal), projected);
}

[Fact]
public void Every_palette_category_has_a_provider_that_implements_QuickAsync()
{
    // die Default-Interface-Methode gibt eine leere Liste zurück: eine Quick-Kategorie ohne Override
    // erscheint nie in der Palette und niemand merkt es
    using var ctx = new SqliteTestContext();
    var providers = SearchTestHost.Providers(ctx)
        .ToDictionary(p => p.Category, StringComparer.Ordinal);

    foreach (var category in SearchCatalog.Clrs(SearchTraits.Quick))
    {
        var declaring = providers[category].GetType()
            .GetMethod(nameof(ISearchProvider.QuickAsync))!.DeclaringType;
        Assert.NotEqual(typeof(ISearchProvider), declaring);
    }
}
```

Dazu eine Zeile in `SearchCoverageTests`: die `IndexAllAsync`-Liste des Backfills muss jeden `SideIndexed`-Typ
abdecken — sonst indiziert der Interceptor laufende Änderungen, aber der Bestand bleibt leer.

**Nicht testbar:** `CommandPalette.razor` selbst. Kein bUnit im Projekt, also keine Komponententests für Chips und
Trichter — die gehen über den manuellen Durchlauf unten.

---

## Reihenfolge

1. **Entscheidungspunkt Informanten-Klarname klären** (Option A oder B). Alles andere hängt nicht daran.
2. **Teil C zuerst**, mindestens den Projektions-Test — er schlägt dann rot aus und führt durch Teil A.
3. **Teil A** je Typ komplett (alle fünf Stellen), nicht typweise halb. Nach jedem Typ Suite grün.
4. **`Version = 2`** als letzter Schritt von Teil A, damit ein Abbruch vorher keinen halben Index als „fertig" markiert.
5. **Teil B** unabhängig, kann auch vorher laufen.

---

## Verifikation

```bash
dotnet build NOOSE-Website.slnx
dotnet test  NOOSE-Website.slnx --filter FullyQualifiedName~Search
dotnet run   --project NOOSE-Website/NOOSE-Website.csproj   # XAMPP muss laufen
```

Manuell, nach dem Start **45 s warten** (Backfill):

1. Agent mit Codename „Meyer" existiert → `/suche` mit **Smart-Suche an** nach `Maier` → Personalakte erscheint.
   Ohne Smart-Suche darf sie **nicht** erscheinen (der Seitenindex hängt an `Fuzzy`).
2. Asservat „Schalldämpfer" → Suche nach `Schaldämpfer` (Tippfehler) → Treffer.
3. Als einfacher Agent nach dem Codename eines Agenten suchen → **keine** Personalakte (Kategorie ist nicht seine).
4. Als Führungsagent, der **nicht** der führende Agent eines Informanten ist, dessen Klarname phonetisch suchen →
   **kein** Treffer. Das ist der Test, der beweist, dass der Index kein Umweg um `InformantVisibility` ist.
5. Ctrl+K: `Mey` tippen → Chips rechts zeigen `[Person]`, `[Befehl]`, `[Favorit]`; letzter Eintrag ist
   „Alles durchsuchen: Mey" und führt auf `/suche?q=Mey`.
6. Ctrl+K bei laufender Eingabe → keine spürbare Verzögerung (300-ms-Budget), keine Fehlermeldung.
7. `/einstellungen?tab=status` → App läuft; im Log steht `Search index backfill: N records indexed.` mit einem
   höheren N als vor dem Deploy.

Prod-Deploy (`.\deploy.ps1`) erst danach. Nach dem Deploy einmal beobachten, dass der Backfill wirklich neu läuft —
er ist der Teil, der still nichts tut, wenn `Version` vergessen wurde.

---

## Betroffene Dateien

| Datei | Änderung |
|---|---|
| `NOOSE-Website/Services/SearchIndexProjection.cs` | 3 Arme; `IndexedTypes` **gelöscht** |
| `NOOSE-Website/Services/Search/SearchCatalog.cs` | `SideIndexed` an Agent, Law, EvidenceItem |
| `NOOSE-Website/Services/Search/Providers/PersonnelSearchProviders.cs` | `ResolveIdsAsync` für Agent |
| `NOOSE-Website/Services/Search/Providers/OperationsSearchProviders.cs` | `ResolveIdsAsync` + `Visible()` für Law |
| `NOOSE-Website/Services/Search/Providers/LedgerSearchProviders.cs` | `ResolveIdsAsync` für EvidenceItem |
| `NOOSE-Website/Infrastructure/Search/SearchIndexBackfillWorker.cs` | 3 Zeilen + `Version = 2` |
| `NOOSE-Website/Components/Common/Shared/CommandPalette.razor` | Trichter, `PaletteKind`, Chips, `PaletteIcon` weg, `?v=3` |
| `NOOSE-Website.Tests/Services/SearchIndexCoverageTests.cs` | **neu** — 6 Drift-Wächter |
| `NOOSE-Website.Tests/Services/Integration/SearchServiceFuzzyTests.cs` | 6 funktionale Tests der neuen Typen |
| `CLAUDE.md` | `?v=`-Regel: alle Importstellen mitziehen |

---

## Abweichungen vom geschriebenen Plan

**1. Informanten sind gar nicht im Index — nicht „nur über das Aktenzeichen".**
Option A war halbgar: Phonetik auf `NOOSE-VP-2026-0001` ist Rauschen, und ein exaktes Aktenzeichen findet die
LIKE-Suche längst. Der einzige Wert läge im Klarnamen, und der gehört nicht in eine Tabelle ohne Zugriffsschicht.
Also drei Typen statt vier, mit einem Kommentar an der Projektion, der das begründet, damit niemand es „nachträgt".

**2. `SearchIndexProjection.IndexedTypes` ist gelöscht statt erweitert.**
Beim Nachsehen hatte das Set **keinen einzigen Konsumenten** — der Interceptor liest ausschließlich `For()`.
Eine Liste, die mit einem Switch synchron gehalten werden muss und von niemandem gelesen wird, ist dieselbe Falle,
die den 12-vs-18-Checkbox-Bug erzeugt hat. Der Drift-Test liest stattdessen die `Build(nameof(X))`-Aufrufe aus dem
Quelltext, also das tatsächliche Verhalten.

**3. Sechs Wächter statt zwei, in eigener Datei.**
`SearchIndexCoverageTests` prüft beide Richtungen (Katalog↔Projektion), die Backfill-Abdeckung, die Versionszahl
und — für Seitenindex *und* Palette — dass der Provider die jeweilige Default-Interface-Methode wirklich
überschreibt. Letzteres fehlte komplett: eine `Quick`-Kategorie ohne `QuickAsync`-Override erscheint nie in der
Palette, und niemand merkt es, weil die Default-Implementierung eine leere Liste zurückgibt.

**Nicht gemacht:** die Palette selbst bleibt ungetestet (kein bUnit im Projekt). Trichter und Chips hängen am
manuellen Durchlauf.

---

## Beim Nachprüfen gefunden und behoben

Zwei Fehler, die Build und Tests nicht gezeigt haben:

**1. Der Personalakten-Provider umging `AgentSelection` (Leak).**
`AgentSearchProvider` und `AgentNoteSearchProvider` griffen auf rohes `db.Users` zu. `/personal` filtert dagegen
`Status != Applicant && Status != Blocked && !IsTeamLead` — die Suche hätte also **TeamLead-Konten** (laut CLAUDE.md
RP-weit unsichtbar), **gesperrte Konten** und **Bewerber** ausgeliefert, inklusive eines Vermerks, dessen Titel den
TeamLead benennt. Genau die Divergenz „Suche weiter als Seite", gegen die der ganze Umbau gebaut ist, und der in
CLAUDE.md als wiederkehrend markierte `AgentSelection`-Fehler.
Behoben über eine **dritte** benannte Regel `AgentSelection.OnlyWithPersonnelFile()` (+ In-Memory-Zwilling
`HasPersonnelFile`), die jetzt **sowohl** `PersonnelList.razor` **als auch** die zwei Provider benutzen — nicht als
Kopie im Provider. Sechs neue Tests nageln alle fünf Konto-Zustände fest.

**2. Die Palette hatte eine reihenfolgeabhängige Verzerrung, die ich selbst eingebaut hatte.**
Der Fuzzy-Pass lädt `FuzzyCandidates` (2000) Zeilen **pro Kategorie**. Bei 11 Palette-Kategorien sind das 22.000
Zeilen pro Tastendruck, und das neue 300-ms-Budget bricht die sequenzielle Schleife dann mitten drin ab — immer an
derselben Stelle. Person/Fraktion hätten ihre Tippfehler-Toleranz immer behalten, Personalakte/Dokument/Besprechung
(später im Katalog) immer verloren. Die alte Implementierung lud dieselben 2000, hatte aber kein Budget und damit
keine Verzerrung — die habe ich also erst erzeugt.
Behoben über `SearchOptions.QuickFuzzyCandidates = 400`: der Pool schrumpft auf 4.400 Zeilen, das Budget beißt
gar nicht mehr, und damit ist die Verzerrung weg statt nur seltener.

**3. Die Suchseite hätte weder Facettenleiste noch Trefferzeilen gerendert.**
`SearchFacetBar` und `SearchHitRow` liegen unter `Components/Pages/Search/Shared/`, also in einem Unter-Namespace,
den `_Imports.razor` nicht mitbringt. Razor meldete das als **`warning RZ10012`** und behandelte beide als unbekannte
HTML-Elemente — die Komponenten wären gar nicht instanziiert worden. Build grün, 5030 Tests grün, Seite kaputt.
Behoben mit `@using NOOSE_Website.Components.Pages.Search.Shared` in `SearchPage.razor`.

**Warum das so lange unentdeckt blieb — und die eigentliche Lehre:** ich habe die Build-Ausgabe die ganze Zeit auf
`: error` gefiltert. Razor meldet eine nicht auflösbare Komponente aber nur als *Warnung*. Ein Clean-Build **ohne**
Warnungsfilter hat zusätzlich vier Nullable-Warnungen in den neuen Providern gezeigt (nullable Spalte in einen
`string`-Parameter, und drei EF-Prädikate, die nicht dem `(x != null && x.Contains(s))`-Muster der Codebase folgten).
Alle behoben; die Warnungsbasis der neuen Dateien ist jetzt leer, damit die nächste echte Warnung auffällt.

---

## `SearchPageParityTests` — der Test, der die ersten zwei Fehler gefunden hätte

Neu: `NOOSE-Website.Tests/Services/Integration/SearchPageParityTests.cs`, **26 Assertions** über 13 Kategorien
× 2 Betrachter. Für jede Kategorie mit kanonischem Listendienst wird die Id-Menge des Suchproviders gegen die des
Dienstes verglichen — mit **echten** Diensten, nicht mit Stubs, weil die Sichtbarkeit in ihnen wohnt.

Abgedeckt: Person, Fraktion, Personengruppe, Partei, Operation, Vorgang, Taskforce, Dokument, Gesetz,
Bibliotheks-Datei, Informant, Asservat, Personalakte.

Der Wert liegt darin, dass der Test **keine Regel kennen muss**: er vergleicht zwei Implementierungen derselben
Frage und schlägt fehl, wenn sie sich uneinig sind — egal welche Seite falsch ist. Eine zu weite Suche leakt, eine
zu enge versteckt die Akte an der einen Stelle, an der ein Agent sie sucht.

**Verifiziert, dass er fehlschlagen kann:** mit wieder eingebautem `db.Users`-Bug meldet er
`Expected: ["a-active", "a-gone", "me"]` gegen `Actual: [..., "a-applicant", "a-blocked", ..., "a-tl"]`.
Ein Paritätstest, der nicht rot werden kann, wäre wertlos.

**Nicht abgedeckt:** Inhalts-Kinder (Kommentare, Quellen, Wiedervorlagen, Verknüpfungen, Zusatzfelder,
Taskforce-Chat, Vermerke …) — sie laufen über den `SearchParentResolver` und haben kein Roster zum Vergleichen.
Für die bleibt `SearchVisibilityTests` zuständig. Ebenso ohne Parität: Termine, Abmeldungen, Feedback, Anträge,
Ankündigungen, Lageberichte, Bewerbungen, Vorlagen, Protokolle und das Persönliche.
