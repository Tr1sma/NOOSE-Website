# Drogenrouten gehören genau einer Fraktion

Tristans Vorgabe vom 24.09.: Eine Route soll nur einer Fraktion zugeordnet werden können. Zwei Fraktionen
halten nie dieselbe Route gleichzeitig.

## Entscheidungen (Tristan, 24.09.)

- **Konflikt:** Wer eine Route einträgt, die eine andere Fraktion hält, wird **gefragt**. Bestätigt er, geht die
  Route beim Speichern über: Sie verschwindet bei der bisherigen Fraktion, und beide Akten bekommen eine Zeile im
  Änderungsprotokoll. Ohne Bestätigung wird nicht gespeichert.
- **Papierkorb und Archiv geben frei.** Eine gelöschte oder archivierte Fraktion hält keine Route. Wird sie
  wiederhergestellt oder aus dem Archiv geholt und hält inzwischen eine andere Fraktion die Route, verliert die
  zurückgeholte sie. Das steht als Protokollzeile auf ihrer Akte.
- **Altbestand wird beim nächsten Speichern geklärt.** Es gibt keine Bereinigung beim Start und keine eigene
  Liste. Wer eine Fraktion speichert, die eine schon doppelt gehaltene Route trägt, bekommt dieselbe Rückfrage.

## Ausgangslage

- `FactionDrugRoute` (Tabelle `FraktionDrogenrouten`) ist eine freie Textzeile (`Bezeichnung`, `Notiz`) je
  Fraktion, nicht auditiert und ohne eigenes Datum.
- `FactionService.CreateAsync`/`RefreshAsync` ersetzen die ganze Liste (`ChildrenMap`). Andere Schreibwege gibt es
  zwei:
  - `ProfileSuggestionService.RenameAsync`, per Massen-Update über **alle** Fraktionen
  - `DeleteAsync` im Vorschlagskatalog, das nur löscht und deshalb unkritisch ist
- Der Vorschlagskatalog kennt nicht jede Route, weil Verschlusssachen dort nicht landen. Seine eigene
  Doppelungsprüfung reicht deshalb nicht.

## Entwurf

- **Was „dieselbe Route“ heißt:** Der Vergleich ignoriert Groß- und Kleinschreibung und Leerzeichen am Rand, und
  mehrere Leerzeichen zählen wie eines (`Services/DrugRouteRules.Key`). Ein Tippfehler bleibt eine andere Route,
  dafür ist der Katalog da.
- **Wer eine Route hält:** jede Fraktion, die weder im Papierkorb (globaler Filter) noch im Archiv
  (`OnlyActive`) liegt.
- **Die Prüfung:** `FactionService.GetDrugRouteConflictsAsync(factionId, designations, actor)` liefert je
  Konflikt die Route, die haltende Fraktion und ob der Handelnde sie übernehmen darf. Das darf er nur, wenn er die
  andere Akte sehen und bearbeiten darf (`Visibility` und `DocumentViewerScope`). Sonst bleiben Name und Id leer,
  und die Meldung lautet „gehört einer Fraktion, die du nicht einsehen kannst“. Eine Verschlusssache verrät damit
  weder ihren Namen, noch kann ihr jemand blind eine Route wegnehmen.
- **Durchsetzung im Dienst**, nicht nur in der Oberfläche:
  - `CreateAsync` und `RefreshAsync` prüfen dieselben Konflikte noch einmal. Jeder muss in
    `FactionInput.ConfirmedRouteTakeovers` stehen und übernehmbar sein, sonst kommt `DrugRouteConflictException`.
  - Bestätigte Routen werden bei der bisherigen Fraktion im **selben** `SaveChanges` entfernt, mit je einer
    `ManualAudit`-Zeile an beiden Akten.
  - Danach bekommt die abgebende Fraktion ihren Bestands-Stempel (`FactionRecency`) und eine neu berechnete
    Gefährdung.
  - Eine Route, die doppelt in derselben Liste steht, wird beim Speichern zu einer.
- **Gleichzeitige Saves:** Ein prozessweites Schloss (`SemaphoreSlim`) umschließt Prüfen und Speichern, beim
  Anlegen bis zum Commit. Die App läuft als eine Instanz. Ein eindeutiger Index ginge nicht, denn die Zeilen einer
  Fraktion im Papierkorb bleiben stehen, und der Altbestand darf Doppelungen enthalten.
- **Zurückholen:** `RestoreAsync` und `UnarchiveAsync` streichen unter demselben Schloss jede Route, die
  inzwischen eine andere haltende Fraktion trägt. Die Protokollzeile nennt die andere Fraktion nicht, denn sie
  könnte eine Verschlusssache sein.
- **Katalog:** Das Umbenennen einer Drogenroute wird abgelehnt, wenn der neue Name schon als Route irgendeiner
  Fraktion existiert. Es würde sonst zwei Routen zusammenlegen, auch bei Verschlusssachen.
- **Oberfläche (`FactionEditor`):** Vor dem Speichern wird geprüft.
  - Sind alle Konflikte übernehmbar, fragt eine Rückfrage „Diese Routen gehören derzeit … – übernehmen und
    speichern?“.
  - Ist einer nicht übernehmbar, kommt nur ein Hinweis, und es wird nicht gespeichert.

## Nicht gebaut

- Keine Liste der doppelten Routen und keine Bereinigung beim Start (Tristans Wahl).
- Kein Hinweis „gehört Fraktion X“ schon in der Vorschlagsliste des Eingabefelds. Die Rückfrage deckt das ab.

## Tests

- `DrugRouteRulesTests`: der Schlüssel (Groß- und Kleinschreibung, Leerzeichen) und das Entdoppeln einer Liste.
- `FactionDrugRouteTests` (Integration):
  - Eine gehaltene Route ohne Bestätigung ergibt einen Konflikt.
  - Mit Bestätigung wechselt die Route, und beide Akten bekommen eine Protokollzeile.
  - Papierkorb und Archiv halten nicht.
  - Eine Verschlusssache als Halter lässt sich ohne Recht nicht übernehmen und bleibt ungenannt.
  - Wiederherstellen und Entarchivieren streichen die Route.
  - Das Anlegen wird ebenso geprüft.
  - Das Umbenennen im Katalog auf eine vorhandene Route wird abgelehnt.
