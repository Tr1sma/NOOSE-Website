# Listen-Ansichten speichern

Roadmap-Idee **#8** aus `IdeenBacklog.md:200` (Aufwand mittel, Jury-Schnitt 6). Geplant am 22.09.2026,
gebaut am 23.09.2026. Umfang von Tristan entschieden: **die vier Listen, die ihre Filter schon in der
URL halten, plus das Aufgaben-Board.**

## Kontext

Listenfilter landen über `QueryState` zwar in der Adresszeile, aber jeder Einstieg über das Menü setzt
sie zurück. Wer täglich dieselbe Auswahl braucht — „Personen, Verdachtsfall, Aktualität rot" — stellt sie
jeden Tag neu ein. Gespeicherte Suchen gibt es nur auf `/suche`, nicht auf den Listen, auf denen
gearbeitet wird. Das Aufgaben-Board schrieb seine Filter nicht einmal in die URL: dort überlebte die
Auswahl nicht einmal einen Reload.

Ergebnis: ein Menü *Ansichten* mit *Diese Ansicht merken* in der Kopfzeile von fünf Listen. Die
gemerkte Ansicht steht im Drawer unter den Favoriten (Gruppe *Meine Ansichten*), in der Strg+K-Palette
(Chip „Ansicht") und im Menü ihrer Liste; gelöscht wird sie dort oder im Anpassen-Dialog.

## Befunde, an denen der Entwurf hängt

1. **`NavigationManager.Uri` hinkt dem Filter hinterher.** `QueryState.WriteAsync` schreibt über
   `history.replaceState` (`App.razor:34`), und das erreicht Blazor nie (vermerkt in `RadioPlan.razor`
   und `SearchPage.razor`). Eine Ansicht wird deshalb **aus den Feldern der Seite** gebaut
   (`QueryState.BuildRoute`), nie aus der Adresszeile.
2. **Ein Favorit ist der falsche Behälter.** `INavPreferencesService.FavoriteId` gäbe jeder Ansicht
   dieselbe Id `record::`, `ReorderFavoritesAsync` baut daraus ein `ToDictionary` und würfe beim ersten
   Ziehen. `NavMenu.Favorite` rendert unbekannte Arten stillschweigend nicht, und
   `Onboarding.MenuTouched` hakte „Menü angepasst" allein durchs Speichern ab.
3. **Blazor recycelt die Seite, wenn sich nur die Query ändert.** Die Listen lesen ihre Filter einmal in
   `OnInitializedAsync`; eine aus Strg+K geöffnete Ansicht der Liste, auf der man schon steht, hätte die
   Adresse geändert und sonst nichts.

## Entscheidung: eigene Liste im Präferenzen-Blob

`NavPreferences.SavedViews` neben `Favorites`, keine Tabelle. Lesezeichen wohnen schon dort
(`Favorites`, `Recents`, `StartRoute`). Der Blob-Schreibweg geht bewusst an der Schreibsperre vorbei
(`ExecuteUpdateAsync`) — eine Tabelle liefe in den `ReadOnlyBarrierInterceptor`, und ausgerechnet die
Nur-Lese-Aufsicht, die den ganzen Tag Listen liest, bekäme keine Ansicht. Ohne Tabelle entfallen
Migration und Pflichtregister. Preis: keine Änderungsprotokoll-Zeile, kein Treffer in der globalen Suche —
beides wie bei Favoriten.

## Abweichungen vom genehmigten Plan

- **Anwenden über `LocationChanged` statt `OnParametersSetAsync`.** Der Plan wollte den Lesevorgang in
  `OnParametersSetAsync` mit einem `_appliedQuery`-Wächter verlegen. Beim Nachlesen des Vorbilds stand in
  `RadioPlan.razor` ausdrücklich, warum das dort **nicht** gemacht wird: `OnParametersSet` läuft auch bei
  Renders, die keine Navigation sind, und liest dann die veraltete `Nav.Uri` — es überschriebe, was der
  Agent gerade getippt hat. Stattdessen horcht der `SavedViewButton` auf `NavigationManager.LocationChanged`
  (feuert nur bei echten Navigationen, nie bei `replaceState`) und reicht die neue Query per `OnApply` an die
  Seite. Das Abo entsteht erst nach dem ersten interaktiven Render, wie in `HandbookHelpButton` — beim
  Prerendern leckte sonst ein Handler auf einen Circuit, der nie lebt. Nebeneffekt, gewollt: ein Klick auf
  den Menüeintrag der Liste, auf der man steht, setzt die Filter jetzt auch sichtbar zurück; vorher sprang
  nur die Adresse zurück und die Filter blieben stehen.
- **Der Helfer heißt `SavedViewRules`**, nicht `SavedViews` — sonst trügen Klasse und Eigenschaft
  `NavPreferences.SavedViews` denselben Namen.
- **Im Menü wird eine Ansicht per Navigation angewandt**, nicht per Rückruf: Adresszeile, Zurück-Knopf und
  Liste sehen denselben Schritt, und es gibt genau einen Weg.

## Was gebaut wurde

| Datei | Was |
|---|---|
| `Models/Navigation/NavPreferences.cs` | `SavedViews` + `record SavedView(Id, Label, Route, Icon)` |
| `Services/SavedViewRules.cs` (neu) | Deckel 20 über alle Listen, Upsert über den Namen **je Liste** (Groß-/Kleinschreibung egal, Id und Platz bleiben), `ForRoute` mit **exaktem** Pfad, `IsLocalRoute`, `Normalise` |
| `Services/INavPreferencesService.cs` + Impl. | `SaveViewAsync` → `SavedViewOutcome`, `RemoveViewAsync`; beide über `MutateAsync` (Schloss), beide mit `notify` |
| `Components/Common/Shared/QueryState.cs` | Query-String-Überladungen, `BuildRoute`, `AnySet`; `ReadFlag` nimmt `"1"`; `ReadEnum` nur noch definierte Werte |
| `Components/Common/Shared/SavedViewButton.razor` (neu) | Menü *Ansichten*; horcht auf `LocationChanged` und `NavPrefs.Changed`; nicht für den Demo-Besucher |
| `Components/Common/Shared/SavedViewDialog.razor` (neu) | Name; warnt vor dem Überschreiben, sperrt bei vollem Deckel nur neue Namen |
| `People/PeopleList`, `Factions/FactionsList`, `Cases/CasesList`, `Operations/OperationsList` | `FilterState`, `ReadFilters(query)`, `ApplyViewAsync`, Ladegeneration, Knopf im Kopf |
| `Jobs/JobsList.razor` | handgebaute Kopfzeile → `PageHeader`; `q`/`meine`/`prio` in der URL; Knopf |
| `Components/Layout/NavMenu.razor` | Gruppe *Meine Ansichten* im Favoriten-Panel, gegatet wie die Liste; Partner über ihre freigegebenen Typen |
| `Components/Common/Shared/CommandPalette.razor` | `PaletteKind.View`, Chip „Ansicht", Listenname als Untertitel, `MaxItems` aufgehoben |
| `Components/Common/Navigation/NavCustomizeDialog.razor` | Reiter *Ansichten* zum Löschen |
| Changelog `2.2.07-ansichten`, Handbuch `art-listen-ansichten` | neu, also ohne Revisions-Erhöhung |

**Mitgenommen:** `LlmRequestLogPanel` schrieb `auf=1` und las mit `bool.TryParse` zurück — der Haken
„nur Auffällige" überlebte nie einen Reload. Behoben über das tolerante `ReadFlag`, ohne das Panel
anzufassen. `ReadEnum` nahm jede Zahl (`?einstufung=99`); jetzt nur noch definierte Werte (kein
`[Flags]`-Enum unter den acht Aufrufern, geprüft).

## Was bewusst nicht gebaut wurde

- Ansichten auf den übrigen Listen (Tristans Entscheidung). Jede weitere Liste: `FilterState`,
  `ReadFilters`, `ApplyViewAsync`, eine Zeile im Kopf.
- Geteilte Ansichten, Umbenennen, Sortieren.
- `/nachweis`: drei Panels und der Rail teilen **eine** Adresse; eine Ansicht trüge alle Filter vermischt.
- **Keine Aktiv-Markierung** für Ansichten im Drawer (`ActiveClass=""`): `NavLink` ignoriert die Query, also
  leuchteten alle Ansichten einer Liste zugleich, und keine sagte, welche gerade offen ist.

## Tests

| Klasse | Fälle | hält |
|---|---|---|
| `Navigation/QueryStateTests` (neu) | 23 | Rundlauf `BuildRoute` → `Read` mit Umlaut, `&`, `+`, `%`, `#`; String- und `NavigationManager`-Form lesen gleich; Flag `"1"`; Enum ohne Mitglied |
| `Services/SavedViewRulesTests` (neu) | 33 | Upsert je Liste, Deckel (auch: voll, aber Ersetzen geht), abgelehnte Routen, Präfix-Falle `/personengruppen`, `PathOf` |
| `NavPreferencesServiceTests` (+) | 13 | Speichern/Ersetzen/Voll/Löschen, getrennt von `Favorites`, `Changed` feuert, parallel zum Recents-Schub, Alt-Blob ohne Feld, Demo-Konto abgewiesen |
| `OnboardingTests` (+) | 1 | eine Ansicht allein hakt „Menü angepasst" nicht ab |
| `Services/SavedViewPageScanTests` (neu) | 5 | Quelltext-Scan der fünf Listen: eigene Route als `BaseRoute`, dieselben Paare für Adresse und Ansicht, Wiederlesen über `ReadFilters`, Ladegeneration vor dem ersten und nach dem letzten `await` |

Bau: 3 Projekte, **0 Fehler, 41 Warnungen** (Ausgangsstand). Gezielt: **227 Tests, 0 rot.**

### Falsifikation

Jede Schranke einmal sabotiert (Sicherung im Scratchpad, Rückspielung per Kopie, `cmp` danach identisch):

| Sabotage | Rot wurde |
|---|---|
| `ForRoute` per Präfix statt exakt | `A_list_sees_its_own_views_and_not_the_ones_of_a_longer_route` |
| `//` in `IsLocalRoute` durchgelassen | `A_route_that_would_leave_the_site_is_refused`, `SaveViewAsync_refuses_without_writing` |
| `Enum.IsDefined` entfernt | `An_enum_value_no_member_carries_reads_as_absent` (99, -1) |
| `MenuTouched` zählt Ansichten mit | `A_saved_list_view_alone_leaves_the_menu_step_open` |
| Deckel entfernt | `The_cap_stops_a_new_name…`, `SaveViewAsync_reports_a_full_list…` |
| `ReadFlag` wieder strikt | `A_flag_reads_one_and_true_as_set("1")`, `The_request_log_anomaly_box_survives_a_reload` |
| Namen mit Groß-/Kleinschreibung verglichen | `The_same_name_overwrites…` (rot, ROT), `A_taken_name_is_recognised…`, Dienst-Ersetzen |

Nach dem Review, dieselbe Probe für die Korrekturen:

| Sabotage | Rot wurde |
|---|---|
| Name wieder listenübergreifend | `The_same_name_on_another_list_is_a_view_of_its_own` |
| Demo-Sperre im Dienst entfernt | `SaveViewAsync_refuses_the_shared_demo_account` |
| Generationsprüfung in `CasesList` entfernt | `Every_list_lets_only_its_newest_load_write_the_rows` |
| `BaseRoute="/aufgabe"` auf dem Board | `Every_list_files_its_views_under_its_own_route` |

Nicht falsifizierbar im Testharnisch: das Schloss selbst (`SqliteTestContext` serialisiert von allein, der
Test fängt nur eine Verklemmung) und das Verhalten der `.razor`-Teile — der Scan hält nur ihre Form, kein bUnit.

## Abnahme im Browser (Klickstrecke)

1. `/personen` → Einstufung *Verdachtsfall* + Aktualität *rot* → *Ansichten* → *Diese Ansicht merken* → Name.
2. Übers Menü auf `/dashboard` und zurück auf `/personen` → Filter sind weg (so soll es sein); Strg+K → die
   Ansicht steht mit Chip „Ansicht" da → anwenden → Filter **und** Adresszeile stimmen.
3. Dasselbe, **während man schon auf `/personen` steht** — der Fall aus Befund 3: die Liste muss sich
   sichtbar umstellen, auch der Archiv-Filter (der lädt neu).
4. `/aufgaben` → „Nur meine" + Priorität → F5 → Auswahl steht noch; Ansicht merken und anwenden.
5. Drawer → Favoriten → *Meine Ansichten*; Zahnrad → Reiter *Ansichten* → löschen.
6. Ohne Filter ist *Diese Ansicht merken* abgeblendet; mit 20 Ansichten sperrt der Dialog nur neue Namen.
7. Im Menü der Liste auf den Papierkorb einer Ansicht klicken → sie verschwindet, die Liste **wechselt nicht**.

## Was der Review ergab

Zwölf Agents auf Opus 5.5: vier Blickrichtungen (Lebenszyklus, Regeln, Oberflächen, Behauptungen), je
Richtung die zwei schwersten Befunde von einem auf „widerlegt" voreingestellten Prüfer gegengelesen, der
Rest von mir. Alle zwölf fertig, keiner mit Fehler. Acht Befunde gegengeprüft (sieben bestätigt, einer
widerlegt), drei weitere nur von mir gelesen. Die elf laufen auf sieben verschiedene Mängel hinaus, weil
mehrere Richtungen dasselbe fanden — und alle sieben führten zu einer Änderung:

1. **Lade-Wettlauf** (zwei Richtungen unabhängig): der Knopf sitzt im Kopf, **außerhalb** von
   `@if (_load)`, und bleibt deshalb während eines Ladevorgangs bedienbar. Eine zweite Ansicht (oder der
   Zurück-Knopf) startete einen zweiten `LoadAsync`, und wer zuletzt fertig wurde, schrieb die Zeilen — die
   Liste zeigte archivierte Akten unter dem Filter „Aktiv". Vorher unerreichbar, weil die Filterleiste
   selbst während des Ladens verschwindet. Jetzt schreibt nur die neueste Ladegeneration.
2. **Dauer-Spinner**: schlug das Nachladen fehl, blieb `_load` auf `true`, und der Kommentar im Knopf
   behauptete, die Liste behalte ihre alten Filter. Jetzt setzt die Seite `_load` zurück, der Knopf sagt es
   dem Agenten, und der Kommentar sagt, was wirklich stehen bleibt.
3. **Demo-Zugang**: alle anonymen Besucher sind dort **ein** Konto (`demo-agent`). Favoriten waren schon
   geteilt, aber ihre Beschriftung kommt aus dem Katalog — ein Ansichtsname ist der erste freie Text, den
   ein Besucher allen folgenden zeigen könnte. Der Knopf und der Reiter fehlen dort jetzt, der Dienst weist
   das Konto zusätzlich ab. Damit stimmt auch der Handbuchsatz „gehören dir allein" wieder.
4. **Namen waren global eindeutig**: „Rot" auf Fraktionen überschrieb „Rot" auf Personen und trug die
   Ansicht zur anderen Liste. Der Prüfer hat das **widerlegt** — der Dialog warnte ja vorher — und hat
   damit recht, dass es nicht still geschah. Geändert habe ich es trotzdem: die Warnung sprach von einer
   Ansicht, die auf dieser Liste gar nicht zu sehen war, und der Kommentar in der Palette ging schon vom
   Gegenteil aus. Namen gelten jetzt je Liste, der Deckel weiter über alle.
5. **Leere Gruppe im Drawer**: der Stern und die Überschrift „Meine Ansichten" hingen an der Rohzahl;
   wurde jede Ansicht vom Gate verschluckt (Partner-Freigabe entzogen, Konto zum Partner gemacht), stand
   eine Überschrift über nichts. Jetzt zählt, was durch den Policy-Schnappschuss kommt.
6. **Palette kappte bei zehn**: `MudAutocomplete.MaxItems` steht per Vorgabe auf 10 — Ansichten nach
   vielen Favoriten fielen still weg, und mit ihnen der Trichter „Alles durchsuchen", den die Palette
   ausdrücklich nie verstecken will. Aufgehoben.
7. **„Zahnrad"**: mein Handbuchsatz schickte den Leser zu einem Zahnrad, das es nicht gibt — der Knopf ist
   `Tune`, Tooltip „Navigation anpassen". Korrigiert. Dieselbe Formulierung steht an vier älteren Stellen
   (Onboarding-Schritt, zwei Handbuchsätze, eine Changelog-Zeile); die gehören nicht zu dieser Etappe und
   brauchen je eine Revisions-Erhöhung — als eigene Aufgabe vorgemerkt.

