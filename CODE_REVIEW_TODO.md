# Code-Review NOOSE-Website – Findings (ToDo)

Stand: 2026-06-09 · Review über die gesamte Code-Base (Services, Components, Data, Models,
Authorization, Infrastructure). Es wurde NICHTS am Code geändert – reine Dokumentation.

## Zusammenfassung

| Kategorie         | Findings |
|-------------------|---------:|
| Bugs              | 7 |
| Logikfehler       | 6 |
| Inkonsistenzen    | 11 |
| Code-Qualität     | 11 |
| UX-Verbesserungen | 10 |
| Bedienbarkeit     | 10 |
| Sicherheit        | 11 |
| **Gesamt**        | **66** |

**Die wichtigsten 5:**

1. **[Sicherheit/hoch]** `FindeDuplikateAsync` filtert Verschlusssachen nicht → der Duplikat-Dialog
   zeigt jedem Agenten Name + Aktenzeichen klassifizierter Akten (real erreichbarer Leak über
   „Neue Person anlegen").
2. **[Sicherheit/hoch]** Schreibende Service-Methoden (Löschen, Wiederherstellen, Bearbeiten,
   Kommentar löschen …) erzwingen Rang/Verschlusssache/Eigentum nicht serverseitig – Schutz hängt
   allein an `AuthorizeView`/Seiten-Policies; `ClaimsPrincipal handelnder` wird nur fürs Audit genutzt.
3. **[Bug/hoch]** Beim Bearbeiten einer Person, deren gespeicherter Lebensstatus „Tot" ist (Fenster
   abgelaufen → effektiv lebend), startet `AktualisierenAsync` ungewollt ein NEUES 20-Minuten-Tot-Fenster –
   eine harmlose Beschreibungs-Änderung „tötet" die Person erneut.
4. **[Inkonsistenz/hoch]** Verschlusssachen-Prüfung der Querschnitts-Services: `VerknuepfungService`
   prüft Person+Fraktion+Gruppe, `QuelleService`/`KommentarService` nur Person → Quellen (inkl.
   Datei-Download) und Kommentare an klassifizierten Fraktionen/Gruppen sind ungeschützt.
5. **[Bedienbarkeit/hoch]** Enter-zum-Absenden und Autofocus fehlen praktisch überall (Formulare und
   alle Dialoge) – jede häufige Erfassung erfordert einen Mausklick auf den Speichern-Button.

---

## 1. Bugs

- [ ] [Schweregrad: hoch] Bearbeiten re-aktiviert abgelaufenes Tot-Fenster
  - Datei: Services/PersonService.cs (`AktualisierenAsync`, ca. Zeile 141–153) + Components/Pages/Personen/PersonBearbeiten.razor (Vorbefüllung, ca. Zeile 75)
  - Problem: Das Edit-Formular befüllt den GESPEICHERTEN Lebensstatus vor (bleibt „Tot", auch wenn das
    20-Min-Fenster abgelaufen ist und die Person effektiv „Lebend" angezeigt wird). Beim Speichern gilt
    `eingabe.Lebensstatus == Tot`, `IstTotFenster(alt…)` ist false (abgelaufen) → `person.TotBis = TotBisAb(DateTime.UtcNow)`
    setzt ein frisches 20-Minuten-Fenster.
  - Warum relevant: Jede beiläufige Bearbeitung (z. B. Tippfehler in der Beschreibung) lässt die Person
    in Liste/Detail wieder als „Tot · respawnt in 20 Min" erscheinen – falscher Aktenstand.
  - Vorschlag: Beim Vorbefüllen den EFFEKTIVEN Status verwenden (`LebensstatusLogic.Effektiv`), oder im
    Service nur dann ein neues Fenster setzen, wenn der Status im Formular tatsächlich von ≠Tot auf Tot
    geändert wurde (alten Eingabewert mitgeben bzw. `altStatus != Tot` prüfen).

- [ ] [Schweregrad: mittel] IdentityResult von UpdateAsync wird ignoriert – Fehler bleiben stumm
  - Datei: Services/AgentVerwaltungService.cs (`Speichern`, ca. Zeile 220–227)
  - Problem: `await userManager.UpdateAsync(agent)` und `UpdateSecurityStampAsync` liefern ein
    `IdentityResult`; `Succeeded` wird nie geprüft. Schlägt das Update fehl (z. B. Concurrency-Stamp,
    Validierungsfehler), passiert nichts – kein Throw, kein Log.
  - Warum relevant: UI zeigt „Freigegeben/Gespeichert/Gesperrt" als Erfolg an, obwohl nichts gespeichert
    wurde. Besonders kritisch beim Kill-Switch (`SperrenAsync`): Admin glaubt, der Account sei gesperrt.
  - Vorschlag: Ergebnis prüfen und bei `!Succeeded` eine `InvalidOperationException` mit den
    `result.Errors` werfen (die Seiten haben z. T. bereits try/catch+Snackbar).

- [ ] [Schweregrad: mittel] Dashboard-Feed verliert „Mitglied entfernt"-Ereignisse
  - Datei: Services/DashboardService.cs (`GetLetzteAenderungenAsync`, ca. Zeile 62–76)
  - Problem: Die Kind-Lookups für `FraktionMitglied`/`PersonengruppeMitglied` laufen OHNE
    `IgnoreQueryFilters`. Ein Austritt ist aber ein Soft-Delete der Mitgliedschafts-Zeile → der globale
    Filter blendet sie aus → das Audit-Ereignis (Aktion `Geloescht`) kann nie auf eine Akte aufgelöst
    werden und wird verworfen. Der Dok-Lookup (Zeile ~60) nutzt `IgnoreQueryFilters` korrekt.
  - Warum relevant: Austritte/Mitglieds-Entfernungen erscheinen nie im Aktivitäts-Feed – Feed lückenhaft.
  - Vorschlag: Bei beiden Mitglieder-Lookups (und `PersonengruppeAgenten`-Zeilen sind hart gelöscht – dort
    bleibt das Verhalten so dokumentieren) `IgnoreQueryFilters()` ergänzen, analog zum Dok-Lookup.

- [ ] [Schweregrad: mittel] Foto-/Quellen-Upload: Datei wird vor dem DB-Insert gespeichert → verwaiste Dateien
  - Datei: Services/PersonService.cs (`FotoHinzufuegenAsync`, ca. Zeile 391–413) und Services/QuelleService.cs (`ErstellenAsync`, Upload-Zweig ca. Zeile 70–80)
  - Problem: Die physische Datei wird zuerst geschrieben, danach der DB-Datensatz. Schlägt das
    `SaveChangesAsync` fehl (z. B. ungültige `personId` → FK-Fehler, DB weg), bleibt die Datei verwaist
    auf der Platte. `FotoEntfernenAsync` macht es explizit andersherum („DB zuerst") – das Anlegen
    widerspricht der eigenen Begründung.
  - Warum relevant: Datenmüll im geschützten Upload-Ordner, kein Aufräum-Mechanismus.
  - Vorschlag: Bei Fehlern nach `SpeichernAsync` die Datei in einem catch wieder löschen
    (`storage.Loeschen(dateiname)`), oder zuerst die Existenz der Person prüfen.

- [ ] [Schweregrad: niedrig] Datei-Endpoints werfen 500 statt 404, wenn die physische Datei fehlt
  - Datei: Infrastructure/Storage/FileStorageService.cs (`OeffnenLesen`, ca. Zeile 38) und QuellenStorageService.cs (analog); Aufrufer: Components/Personen/PersonenDateiEndpointRouteBuilderExtensions.cs (ca. Zeile 44) und Components/Querschnitt/QuellenDateiEndpointRouteBuilderExtensions.cs (ca. Zeile 38)
  - Problem: `File.OpenRead` wirft `FileNotFoundException`, wenn der DB-Datensatz existiert, die Datei
    aber fehlt (manuell gelöscht, Restore ohne Dateien). Der Minimal-API-Endpoint fängt das nicht.
  - Warum relevant: Unschöner 500er + Stacktrace im Log statt sauberem 404; kaputte Bilder in der Galerie.
  - Vorschlag: Im Endpoint `File.Exists`-Prüfung bzw. try/catch um `OeffnenLesen` → `Results.NotFound()`.

- [ ] [Schweregrad: niedrig] Sync-über-Async im Audit-Interceptor (Deadlock-Potenzial)
  - Datei: Infrastructure/Audit/AuditSaveChangesInterceptor.cs (`SavingChanges`/`SavedChanges`, ca. Zeile 48–57 und 66–70)
  - Problem: Die synchronen Interceptor-Pfade blockieren mit `GetAwaiter().GetResult()` auf
    `currentUserService.GetAsync()` bzw. `WritePendingAsync`.
  - Warum relevant: Sobald irgendwo synchron `SaveChanges()` gerufen wird (heute nicht der Fall, aber
    z. B. durch Identity-Interna oder künftigen Code), drohen Thread-Pool-Starvation/Deadlocks.
  - Vorschlag: Sync-Pfade entweder mit `NotSupportedException` hart verbieten oder eine synchrone
    `CurrentUserInfo`-Beschaffung (HttpContext-only) verwenden.

- [ ] [Schweregrad: niedrig, zu verifizieren] Zeitzonen-Behandlung hängt an der Server-Zeitzone
  - Datei: Components/Pages/Personen/Shared/DokDialog.razor (`Speichern`, ca. Zeile 158–162), DokAnlegenDialog.razor (analog ca. Zeile 160), alle `ToLocalTime()`-Anzeigen (Listen, Timelines, Karten)
  - Problem: Blazor Server rechnet `DateTime.Now`, `DateTimeKind.Local` und `ToLocalTime()` in der
    ZEITZONE DES SERVERS, nicht des Browsers. Läuft der VPS auf UTC (Linux-Standard), werden deutsche
    RP-Zeiten beim Erfassen und Anzeigen um 1–2 Stunden verschoben.
  - Warum relevant: Maßnahme-Zeitpunkte und das 20-Min-Tot-Fenster („Tod trat um X ein") wären falsch.
  - Vorschlag: Deployment prüfen (TZ=Europe/Berlin setzen) ODER sauber: Browser-Zeitzone per JS-Interop
    ermitteln/`TimeProvider` nutzen und zentral konvertieren.

---

## 2. Logikfehler

- [ ] [Schweregrad: mittel] Globale Suche findet Quellen/Kommentare nur an Personen-Akten
  - Datei: Services/SearchService.cs (Quellen-Query ca. Zeile 123, Kommentar-Query ca. Zeile 141: `quelle.EntitaetTyp == nameof(Person)` / `kommentar.EntitaetTyp == nameof(Person)`)
  - Problem: Die UI bietet Quellen/Kommentare auch an Fraktionen (FraktionDetail.razor, Tabs „Quellen"/
    „Kommentare") und Personengruppen an – die Suche durchsucht aber nur Person-Zuordnungen.
  - Warum relevant: Inhalte an Org-Akten sind über die globale Suche unauffindbar; Nutzer verlassen sich
    auf den Hinweistext „Durchsucht … Quellen und Kommentare".
  - Vorschlag: Queries auf `EntitaetTyp IN (Person, Fraktion, Personengruppe)` erweitern und das Ziel
    per Typ auflösen (Route via `SuchNavigation`), inkl. jeweiligem Verschlusssache-Filter.

- [ ] [Schweregrad: mittel] Mitgliederzahl in Listen ≠ Mitgliederliste im Detail
  - Datei: Components/Pages/Fraktionen/FraktionenListe.razor (ca. Zeile 78: `f.Mitglieder.Count`) und Components/Pages/Gruppen/GruppenListe.razor (ca. Zeile 75) vs. Services/FraktionService.cs (`GetMitgliederAsync`, ca. Zeile 184–198)
  - Problem: Die Listen zählen alle aktiven Mitgliedschafts-Zeilen; das Detail-Panel filtert zusätzlich
    Mitglieder heraus, deren Person im Papierkorb liegt oder (für Nicht-Führung) Verschlusssache ist.
  - Warum relevant: Liste zeigt „5 Mitglieder", Akte zeigt 3 – wirkt wie ein Datenfehler.
  - Vorschlag: Zählung in `GetListeAsync` über denselben Filter (Join auf Personen + VS-Bedingung)
    berechnen, z. B. als projizierte Zahl statt `Include(Mitglieder)`.

- [ ] [Schweregrad: niedrig] Fraktionssuche ignoriert Beschreibung/Ziele
  - Datei: Services/SearchService.cs (Fraktionen-Query, ca. Zeile 60–66)
  - Problem: Personen werden über Name/Aktenzeichen/Beschreibung/Aliase gesucht, Gruppen über
    Name/Aktenzeichen/Beschreibung – Fraktionen nur über Name/Aktenzeichen/Art. `Beschreibung` und
    `Ziele` fehlen.
  - Warum relevant: Suchtreffer-Erwartung („Volltext") wird je Aktentyp unterschiedlich erfüllt.
  - Vorschlag: `f.Beschreibung`/`f.Ziele` in das Where aufnehmen (wie bei Person/Gruppe).

- [ ] [Schweregrad: niedrig] Dashboard-Kachel „Offene Anträge" zählt Namensänderungen nicht
  - Datei: Services/DashboardService.cs (`GetKennzahlenAsync`, ca. Zeile 26) vs. Components/Pages/Admin/Freigaben.razor (zwei Posteingänge)
  - Problem: Gezählt wird nur `Status == Ausstehend`; der Freigabe-Posteingang enthält zusätzlich offene
    Namensänderungs-Anträge (`NamensaenderungBeantragtAm != null`).
  - Warum relevant: Kachel zeigt 0, obwohl der Posteingang Arbeit enthält → Anträge bleiben liegen.
  - Vorschlag: Beide Zähler addieren (oder getrennt ausweisen).

- [ ] [Schweregrad: niedrig] Doppelte Verknüpfungen/Beziehungen möglich
  - Datei: Services/VerknuepfungService.cs (`ErstellenAsync`, ca. Zeile 112–130) und Services/BeziehungService.cs (`ErstellenAsync`, ca. Zeile 40–60)
  - Problem: Kein Duplikat-Check: dieselbe Person↔Person-Verknüpfung bzw. Beziehung (auch in
    Gegenrichtung A↔B vs. B↔A) kann beliebig oft angelegt werden. Die Org-Beziehungs-Dialoge schließen
    bestehende Ziele clientseitig aus, der Personen-`VerknuepfungDialog`/`BeziehungDialog` nicht.
  - Warum relevant: Verwirrende Mehrfacheinträge im Panel; später Mehrfachkanten im Graph.
  - Vorschlag: Im Service vor dem Insert prüfen (beide Richtungen, `Art`/`Typ` beachten, soft-gelöschte
    ausgenommen) und mit verständlicher Meldung ablehnen – analog zur Mitgliedschafts-Duplikatprüfung.

- [ ] [Schweregrad: niedrig] Neues „Erschossen"-Dok überschreibt ein bereits laufendes Tot-Fenster
  - Datei: Services/PersonDokService.cs (`ErstelleDokAsync`, ca. Zeile 175–185)
  - Problem: Bei `Ausgang == Erschossen` wird `TotBis` bedingungslos auf `Zeitpunkt + 20 Min` gesetzt.
    Erfasst man nachträglich ein Dok mit älterem Zeitpunkt, während ein aktuelleres Fenster läuft, wird
    das Fenster verkürzt oder sofort beendet.
  - Warum relevant: Nachträgliche Dok-Erfassung (häufig im RP) kann den aktuell korrekten Status kippen.
  - Vorschlag: Nur setzen, wenn das neue `TotBis` GRÖSSER ist als das bestehende (`person.TotBis is null
    || neu > person.TotBis`), analog zur „besitzt Fenster"-Logik in `StatusNeuAuswerten`.

---

## 3. Inkonsistenzen

- [ ] [Schweregrad: hoch] Verschlusssache-Prüfung der Querschnitts-Services deckt Fraktion/Gruppe nicht ab
  - Datei: Services/QuelleService.cs (`AkteSichtbarAsync`, ca. Zeile 124–143: nur `nameof(Person)`) und Services/KommentarService.cs (`AkteSichtbarAsync`, ca. Zeile 60–73) — SO DORT: Services/VerknuepfungService.cs (`AkteSichtbarAsync`, ca. Zeile 141–172 prüft Person UND Fraktion UND Personengruppe)
  - Problem: Beide Services geben für alle Nicht-Person-Typen pauschal `true` zurück („nur Person in
    Phase 3"), obwohl Phase 4 Quellen/Kommentare an Fraktionen/Gruppen eingeführt hat. Betroffen sind
    `GetFuerAkteAsync` UND `GetFuerDownloadAsync` (Datei-Download-Endpoint!).
  - Warum relevant: Quellen-Anhänge und Kommentare einer Verschlusssache-Fraktion/-Gruppe sind für
    Nicht-Führung abrufbar (Download mit bekannter Quellen-Id; Service-Antworten ungefiltert).
  - Vorschlag: Die vollständige `AkteSichtbarAsync`-Variante aus `VerknuepfungService` in einen
    gemeinsamen Helfer ziehen und in Quelle-/Kommentar-Service verwenden.

- [ ] [Schweregrad: mittel] Fehlerbehandlung: try/catch+Snackbar mal vorhanden, mal nicht
  - Datei (mit Handling): Components/Pages/Personen/PersonNeu.razor (`SpeichernAsync`), QuellenPanel.razor, KommentarPanel.razor, ZugehoerigkeitenPanel.razor (`HinzufuegenAsync`), OrgBeziehungenPanel.razor — SO DORT (ohne Handling): Components/Pages/Personen/Shared/DokPanel.razor (`NeuAsync` ca. Zeile 95, `LoeschenAsync` ca. Zeile 117), DokDetailDialog.razor (`BearbeitenAsync` ca. Zeile 95), PersonenPapierkorb.razor (`WiederherstellenAsync`), FraktionenPapierkorb/GruppenPapierkorb (analog), Admin/Freigaben.razor (`Freigeben`/`Ablehnen` ca. Zeile 169–196), Admin/Agenten.razor (`RangAendern`/`Sperren`/… ca. Zeile 120–165), ZugehoerigkeitenPanel.`EntfernenAsync`, FotoGalerie.`EntfernenAsync`
  - Problem: Dieselbe Art von Service-Aufruf wird auf manchen Seiten abgefangen (Snackbar), auf anderen
    nicht – dort führt eine Exception (z. B. „nicht gefunden", DB-Fehler) zum gelben
    Blazor-Fehlerbanner und der Circuit ist faktisch tot.
  - Warum relevant: Unvorhersehbares Fehlerverhalten; ein abgelaufener Datensatz reißt die ganze Seite ab.
  - Vorschlag: Einheitliches Muster (try/catch + `Snackbar.Add(ex.Message, Severity.Error)`) für ALLE
    schreibenden UI-Aktionen; ggf. kleiner Helper `await Ausfuehren(() => …, "Erfolgstext")`.

- [ ] [Schweregrad: mittel] Duplikat-Warnung nur beim regulären „Neue Person anlegen"
  - Datei (mit Check): Components/Pages/Personen/PersonNeu.razor (`SpeichernAsync` ca. Zeile 86–95, via `FindeDuplikateAsync`) — SO DORT (ohne Check): Fraktionen/Shared/MitgliedDialog.razor („Neue Person anlegen", ca. Zeile 100), Gruppen/Shared/GruppeMitgliedDialog.razor (analog), Personen/Shared/DokAnlegenDialog.razor (`IstNeueAkte`), Services/PersonDokService.cs (`ErstellenFuerNeuePersonAsync`), FraktionService/PersonengruppeService (`PersonIdErmittelnAsync`)
  - Problem: Über die Schnellanlage-Pfade (Mitglied hinzufügen, Dok für neue Person) entstehen Akten ohne
    jede Dubletten-Prüfung; tippt man im Dok-Dialog einen existierenden Namen, ohne den Treffer
    anzuklicken, entsteht still eine zweite Akte.
  - Warum relevant: Genau die Dubletten, die Phase 2 verhindern wollte, entstehen über die Nebenwege.
  - Vorschlag: `FindeDuplikateAsync` auch in den Schnellanlage-Pfaden aufrufen (Warn-Dialog oder
    mindestens Hinweis-Snackbar „Achtung: gleichnamige Akte existiert").

- [ ] [Schweregrad: mittel] Anlege-Flows: Gruppe mit Mitglieder-Erfassung, Fraktion ohne
  - Datei: Components/Pages/Gruppen/Shared/GruppeForm.razor (`ZeigeMitglieder`, ca. Zeile 45–90) + Models/Gruppen/PersonengruppeEingabe.cs (`Mitglieder`) — SO DORT: Components/Pages/Fraktionen/Shared/FraktionForm.razor + Models/Fraktionen/FraktionEingabe.cs (KEINE Mitglieder beim Anlegen; Kommentar „Mitglieder … über eigene Endpunkte")
  - Problem: Beim Anlegen einer Personengruppe kann man Mitglieder direkt miterfassen, bei einer
    Fraktion nicht – gleicher Anwendungsfall, zwei Bedienmuster.
  - Warum relevant: Bricht das Muscle-Memory; Fraktionsanlage erfordert Nachklicken in der Akte.
  - Vorschlag: Mitglieder-Sektion (wie `GruppeForm`) auch in `FraktionForm`/`FraktionService.ErstellenAsync`
    anbieten – oder bewusst beide ohne (dann Gruppe vereinfachen).

- [ ] [Schweregrad: niedrig] Leere Listen: DataGrid ohne, MudTable mit Leerzustand
  - Datei: Components/Pages/Personen/PersonenListe.razor, Fraktionen/FraktionenListe.razor, Gruppen/GruppenListe.razor (MudDataGrid OHNE `NoRecordsContent`) — SO DORT: Personen/DoksUebersicht.razor (ca. Zeile 84) und Admin/Tags.razor (MudTable MIT `NoRecordsContent`)
  - Problem: Die drei Hauptlisten zeigen bei leerer Datenbank ein leeres Grid ohne Hinweis.
  - Warum relevant: Neue Instanz/neuer Nutzer sieht „nichts" statt „Noch keine Akten – lege die erste an".
  - Vorschlag: `<NoRecordsContent>` mit Hinweis + ggf. Anlegen-Button ergänzen.

- [ ] [Schweregrad: niedrig] CancellationToken-Weitergabe nur in Autocomplete-Suchen
  - Datei: alle `SearchFunc`-Aufrufe (z. B. Personen/Shared/DokAnlegenDialog.razor `SuchePersonen`) geben das Token weiter — SO DORT: sämtliche `OnInitializedAsync`/Button-Handler (z. B. PersonDetail.razor `LadenAsync`, DokPanel.razor `LadenAsync`) rufen Services OHNE Token auf, obwohl jede Service-Methode eines akzeptiert
  - Problem: Halbherzige CT-Durchreichung; lange DB-Queries laufen nach Navigations-/Circuit-Abbruch weiter.
  - Warum relevant: Unnötige DB-Last; Pattern wirkt beliebig.
  - Vorschlag: Einheitlich entscheiden – mindestens in teuren Lade-Methoden ein komponentengebundenes
    CTS (`IAsyncDisposable`) verwenden oder CT-Parameter aus den UI-Pfaden konsequent nutzen.

- [ ] [Schweregrad: niedrig] `istFuehrung`-Parameter mit unterschiedlicher Bedeutung
  - Datei: Services/PersonDokService.cs (`GetFuerPersonAsync`, ca. Zeile 15: filtert NUR die Org-Anzeige, nicht die Person) — SO DORT: `GetAlleAsync` (ca. Zeile 25: filtert Verschlusssache-PERSONEN weg)
  - Problem: Gleicher Parametername, einmal Sichtbarkeitsfilter der Eltern-Akte, einmal nur Anzeige-Detail.
    `GetFuerPersonAsync` verlässt sich stillschweigend darauf, dass der Aufrufer die Person bereits geprüft hat.
  - Warum relevant: Spätere Aufrufer übersehen die Vorbedingung leicht (Sicherheits-Falle).
  - Vorschlag: In `GetFuerPersonAsync` die Eltern-Person ebenfalls prüfen (eine Mini-Query) oder die
    Vorbedingung im XML-Doc-Kommentar deutlich machen.

- [ ] [Schweregrad: niedrig] Upload-Konfiguration halb in appsettings, halb im Code
  - Datei: appsettings.json (`FileUpload`: nur PersonenPfad/MaxBytes/ContentTypes) — SO DORT: Infrastructure/Storage/FileUploadOptions.cs (Quellen-Defaults `QuellenPfad`/`QuellenMaxBytes`/`ErlaubteQuellenContentTypes` NUR als Code-Default)
  - Problem: Wer Limits in appsettings sucht, findet die Quellen-Werte nicht; Konfig-Drift.
  - Warum relevant: Betrieb (Phase 10) ändert das Personen-Limit und wundert sich über Quellen-Limit.
  - Vorschlag: Quellen-Schlüssel mit Default-Werten in appsettings.json aufnehmen.

- [ ] [Schweregrad: niedrig] Veraltete Kommentare behaupten geteilten Circuit-DbContext
  - Datei: Components/Querschnitt/Shared/CommandPalette.razor (catch-Kommentar „geteilter Circuit-Context", ca. Zeile 95) und Components/Pages/Suche/SucheSeite.razor (Kommentar ca. Zeile 199–201) — SO DORT: Program.cs (Zeile 43–58: ausführliche Begründung der DbContext-FACTORY, jede Arbeitseinheit eigener Context)
  - Problem: Die Kommentare erklären Schutzmechanismen mit einem Problem, das die Factory-Architektur
    bereits beseitigt; das Catch-all in der Palette schluckt zudem ALLE Fehler.
  - Warum relevant: Irreführend für Wartung – man behält Workarounds bei oder kopiert sie weiter.
  - Vorschlag: Kommentare korrigieren; im Palette-catch zumindest loggen.

- [ ] [Schweregrad: niedrig] Kill-Switch-Intervalle: Kommentar 30 s vs. Revalidierung 1 min
  - Datei: Program.cs (Zeile 78–80: `SecurityStampValidatorOptions.ValidationInterval = 30 s`, Kommentar „greift praktisch sofort") — SO DORT: Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs (`RevalidationInterval = TimeSpan.FromMinutes(1)`, Kommentar „Kurzes Intervall")
  - Problem: Zwei Stellschrauben für dieselbe Garantie mit verschiedenen Werten/Aussagen.
  - Warum relevant: Wer die Sperr-Latenz beurteilen will, liest widersprüchliche Angaben (eff. bis zu 1 min).
  - Vorschlag: Beide auf denselben Wert setzen und im Kommentar die effektive Worst-Case-Latenz nennen.

- [ ] [Schweregrad: niedrig] Tooltip „Fraktions-Mitgliedschaft" auch für Gruppenkollegen-Links
  - Datei: Components/Querschnitt/Shared/VerknuepfungPanel.razor (Tooltip, ca. Zeile 95: „Automatisch durch Fraktions-Mitgliedschaft …") — SO DORT: Services/KollegenSync.cs (Label „Gruppenkollege" für Personengruppen)
  - Problem: Automatische Gruppenkollegen-Verknüpfungen zeigen denselben Fraktions-Tooltip.
  - Warum relevant: Verwirrt bei der Ursachensuche („die Person ist in keiner Fraktion!").
  - Vorschlag: Tooltip aus dem Label ableiten („Automatisch durch Fraktions-/Gruppen-Mitgliedschaft").

---

## 4. Code-Qualität

- [ ] [Schweregrad: mittel] Zwei nahezu identische Dok-Dialoge (~150 Zeilen Duplikat)
  - Datei: Components/Pages/Personen/Shared/DokDialog.razor und DokAnlegenDialog.razor
  - Problem: Datum/Zeit, Ausgang inkl. Warnhinweise, Org-Picker (RadioGroup + Autocomplete + AlsMitglied),
    Freitext-Felder sind doppelt implementiert; DokAnlegenDialog ergänzt nur die Personenwahl.
  - Warum relevant: Jede Feld-Änderung muss zweimal nachgezogen werden (Drift bereits sichtbar:
    `_orgVerborgen`-Schutz existiert nur im DokDialog).
  - Vorschlag: Gemeinsame Felder in eine `DokFelderForm`-Komponente extrahieren; DokAnlegenDialog =
    Personenwahl + `DokFelderForm`.

- [ ] [Schweregrad: mittel] Dreifache Kopien: Papierkorb-Seiten, Historie-Timelines, Listen-Seiten
  - Datei: PersonenPapierkorb/FraktionenPapierkorb/GruppenPapierkorb.razor; HistorieTimeline/FraktionHistorieTimeline/GruppeHistorieTimeline.razor; PersonenListe/FraktionenListe/GruppenListe.razor
  - Problem: Struktur, Spalten und Code-Blöcke sind je dreimal fast identisch (nur Service/Routen/Labels
    unterschiedlich).
  - Warum relevant: Der vierte Aktentyp (Partei, Phase 5) verdreifacht den Pflegeaufwand erneut; Fixes
    (z. B. Leerzustand, try/catch) müssen 3× erfolgen.
  - Vorschlag: Generische `PapierkorbSeite`/`AuditTimeline`-Komponenten mit Parametern (Titel, Lade-/
    Restore-Delegate, Routen-Prefix, Typ-Label-Map).

- [ ] [Schweregrad: mittel] `GetAbgeleiteteBeziehungenAsync` ist ein 125-Zeilen-Monolith
  - Datei: Services/PersonService.cs (ca. Zeile 264–389)
  - Problem: Sechs nummerierte Schritte (Orgs sammeln, Verknüpfungen, Partner auflösen, Mitglieder,
    Kandidaten, Personen) in einer Methode mit mehreren Dictionaries und Tupel-Typen.
  - Warum relevant: Schwer testbar/lesbar; künftige Erweiterung (z. B. Parteien) wird riskant.
  - Vorschlag: In private Helfer je Schritt zerlegen (oder eigene Klasse `AbgeleiteteBeziehungenRechner`),
    Zwischenmodelle benennen.

- [ ] [Schweregrad: niedrig] `Leer(string?)`-Helfer sechsfach kopiert
  - Datei: PersonService.cs (Zeile 496), PersonDokService.cs (Zeile 231), FraktionService.cs (Zeile 375), PersonengruppeService.cs (Zeile 418), QuelleService.cs (Zeile 144), TagService.cs (Zeile 117)
  - Problem: Identische private Methode in sechs Services.
  - Warum relevant: Trivial, aber symptomatisch; Normalisierungsregeln könnten auseinanderlaufen.
  - Vorschlag: Zentrale Extension (z. B. `StringExtensions.TrimToNull()`), überall verwenden.

- [ ] [Schweregrad: niedrig] `VerknuepfungService.GetFuerAkteAsync`: drei Copy-Paste-Typ-Branches
  - Datei: Services/VerknuepfungService.cs (ca. Zeile 40–110)
  - Problem: Für Person/Fraktion/Personengruppe je ein identischer Block (Ids sammeln → Map laden →
    VS-Filter → Anzeige bauen); der vierte Aktentyp verlängert die Methode erneut.
  - Warum relevant: Wartung + Gefahr, den VS-Filter in einem neuen Branch zu vergessen.
  - Vorschlag: Typ-Registry (Dictionary `Typ → (LadeFunc, RouteFunc)`), die auch `AkteSichtbarAsync`,
    `SuchNavigation` und die Panels speist.

- [ ] [Schweregrad: niedrig] Storage-Services doppelt implementiert
  - Datei: Infrastructure/Storage/FileStorageService.cs und QuellenStorageService.cs
  - Problem: Bis auf Pfad/Limit/Endungslogik identische Klassen (`SichererPfad` 1:1 kopiert).
  - Warum relevant: Path-Traversal-Schutz lebt an zwei Stellen – Fix an einer Stelle vergisst die andere.
  - Vorschlag: Gemeinsame Basisklasse/Kern (`DateiSpeicher` mit Optionen-Parameter), zwei dünne Ableitungen.

- [ ] [Schweregrad: niedrig] `PersonIdErmittelnAsync` doppelt
  - Datei: Services/FraktionService.cs (ca. Zeile 234–246) und Services/PersonengruppeService.cs (ca. Zeile 258–270)
  - Problem: Identische Logik (bestehende Person prüfen oder neue anlegen) zweimal.
  - Warum relevant: Duplikat-Check (siehe Inkonsistenzen) müsste später an zwei Stellen rein.
  - Vorschlag: In `IPersonService` (z. B. `FindeOderErstelleAsync`) oder einen gemeinsamen Helfer ziehen.

- [ ] [Schweregrad: niedrig] Tote Authorization-Policy „Verschlusssache" + Stub-Handler
  - Datei: Authorization/Policies.cs (Zeile 25), AuthorizationRegistration.cs (Zeile 33–35), VerschlusssacheAuthorizationHandler.cs
  - Problem: Policy ist registriert, wird aber nirgends benutzt (kein `[Authorize]`, kein `AuthorizeView`);
    die reale VS-Prüfung läuft komplett über `istFuehrung`-Parameter in den Services.
  - Warum relevant: Täuscht eine ressourcenbasierte Absicherung vor, die nicht existiert; toter Code.
  - Vorschlag: Entweder die ressourcenbasierte Prüfung tatsächlich einführen (Handler mit Akte als
    Resource, von den Services aufgerufen) oder Policy+Handler bis dahin entfernen/als TODO markieren.

- [ ] [Schweregrad: niedrig] Farb-Array für Tags doppelt definiert
  - Datei: Components/Querschnitt/Shared/TagDialog.razor (ca. Zeile 60) und TagPickerDialog.razor (ca. Zeile 95)
  - Problem: Identisches `Color[] Farben`-Array zweimal.
  - Warum relevant: Farbpalette ändern = zwei Stellen.
  - Vorschlag: In eine statische Klasse (z. B. `TagFarben.Alle`) auslagern.

- [ ] [Schweregrad: niedrig] Error.razor ist ungestyltes Template mit Bootstrap-Resten
  - Datei: Components/Pages/Error.razor (Zeile 7–8: `class="text-danger"`)
  - Problem: Bootstrap wurde in Phase 0 entfernt – die Klassen wirken nicht; Seite ist roh/unthematisiert
    und passt optisch nicht zur App (vgl. NotFound.razor, das sauber im MudBlazor-Look ist).
  - Warum relevant: Gerade die Fehlerseite sehen Nutzer im schlechtesten Moment.
  - Vorschlag: Im Stil von NotFound.razor neu aufbauen (MudPaper, Icon, „Zum Dashboard"-Button).

- [ ] [Schweregrad: niedrig] AktenzeichenService verlässt sich stillschweigend auf Aufrufer-Transaktion
  - Datei: Services/AktenzeichenService.cs (`NaechstesAsync`, ca. Zeile 9–20)
  - Problem: Das Muster „INSERT … ON DUPLICATE KEY UPDATE + anschließendes SELECT" ist nur race-sicher,
    weil ALLE drei Aufrufer (Person/Fraktion/Gruppe-Erstellen) eine Transaktion öffnen (Row-Lock bis
    Commit). Ohne Transaktion (Autocommit) könnten zwei parallele Anlagen dieselbe Nummer lesen →
    Unique-Index-Crash. Diese Vorbedingung steht nur indirekt im Kommentar.
  - Warum relevant: Ein künftiger Aufrufer ohne Transaktion reaktiviert die Race unbemerkt.
  - Vorschlag: Im Service `db.Database.CurrentTransaction is null` → Exception werfen (Fail-fast), oder
    auf `LAST_INSERT_ID(LetzteNummer + 1)`-Muster umstellen (transaktionsunabhängig atomar).

---

## 5. UX-Verbesserungen (Feedback & Information)

- [ ] [Schweregrad: mittel] Historie zeigt nie, WAS sich geändert hat (Alt→Neu fehlt)
  - Datei: Components/Pages/Personen/Shared/HistorieTimeline.razor (ca. Zeile 28–39, nur „Akte geändert") sowie FraktionHistorieTimeline/GruppeHistorieTimeline; Daten vorhanden in Infrastructure/Audit/AuditLog.cs (`AenderungenJson`)
  - Problem: Der Interceptor erfasst die geänderten Felder als Alt→Neu-JSON, die Timeline rendert aber nur
    „Akte geändert · Datum · Agent". Plan §7 verlangt ausdrücklich „Wer/Wann/Alt→Neu".
  - Warum relevant: Der Hauptzweck der Historie („was wurde geändert?") wird nicht erfüllt – man sieht
    nur DASS etwas passierte.
  - Vorschlag: `AenderungenJson` deserialisieren und als aufklappbare Feldliste („Name: Alt → Neu")
    anzeigen; technische Felder (GeaendertAm/-VonId) ausblenden.

- [ ] [Schweregrad: mittel] Nicht-Führung kann sich durch „Verschlusssache" selbst aussperren – ohne Warnung
  - Datei: Components/Pages/Personen/PersonBearbeiten.razor (Switch, ca. Zeile 44), PersonNeu.razor (Zeile 43), FraktionForm.razor (Zeile 36), GruppeForm.razor (Zeile 29)
  - Problem: Jeder aktive Agent kann den VS-Schalter setzen. Speichert ein Junior Agent das, verliert er
    sofort selbst den Zugriff auf die Akte (GetDetail → null) – ohne jeden Hinweis vorher.
  - Warum relevant: Versehentlicher Klick = Akte „verschwindet" für den Bearbeiter und alle Nicht-Führung;
    nur Führung kann es rückgängig machen. Wirkt wie Datenverlust.
  - Vorschlag: Beim Aktivieren durch Nicht-Führung Warn-Dialog („Du verlierst selbst den Zugriff –
    fortfahren?") oder das Setzen auf Führung beschränken (fachlich klären).

- [ ] [Schweregrad: mittel] Dok löschen lässt gesetzten Tot-Status kommentarlos stehen
  - Datei: Components/Pages/Personen/Shared/DokPanel.razor (`LoeschenAsync`, Bestätigungstext ca. Zeile 117) + Services/PersonDokService.cs (`LoeschenAsync`, Kommentar „kein Revert", ca. Zeile 211)
  - Problem: Löscht man ein versehentlich falsches „Erschossen"-Dok, bleibt die Person für den Rest des
    Fensters „Tot" – der Dialog erwähnt das nicht.
  - Warum relevant: Genau der Korrektur-Anwendungsfall (falscher Ausgang erfasst) hinterlässt falschen Status.
  - Vorschlag: Mindestens Hinweis im Bestätigungsdialog; besser: beim Löschen dieselbe
    „besitzt Fenster"-Rücknahme wie in `StatusNeuAuswerten` ausführen.

- [ ] [Schweregrad: niedrig] Gespeicherte Suche: Löschen ohne Bestätigung/Feedback
  - Datei: Components/Pages/Suche/SucheSeite.razor (`SucheLoeschenAsync`, ca. Zeile 277–285; Chip-`OnClose` Zeile 60)
  - Problem: Das kleine X am Chip löscht sofort und still – keine Bestätigung, keine Snackbar, kein Undo.
  - Warum relevant: X liegt direkt neben dem Klickziel „Suche anwenden" – Fehlklick zerstört die Suche.
  - Vorschlag: Snackbar mit „Rückgängig"-Aktion oder kurze Bestätigung.

- [ ] [Schweregrad: niedrig] Dialog-Speichern ohne Auswahl tut still nichts
  - Datei: Components/Pages/Personen/Shared/ZugehoerigkeitDialog.razor (`Speichern`, ca. Zeile 70), Querschnitt/Shared/BeziehungDialog.razor, VerknuepfungDialog.razor, OrgBeziehungDialog.razor, Gruppen/Shared/AgentZuteilenDialog.razor, Fraktionen/Shared/MitgliedDialog.razor (`Speichern` mit frühem `return`)
  - Problem: Klick auf den Primär-Button ohne gewähltes Ziel macht einfach nichts – kein Fehlertext, der
    Dialog bleibt kommentarlos offen. DokAnlegenDialog zeigt dagegen einen `_personFehler`-Alert (Vorbild).
  - Warum relevant: Nutzer hält den Dialog für kaputt.
  - Vorschlag: Validierungsmeldung anzeigen (Alert wie im DokAnlegenDialog) oder Button bis zur Auswahl
    disablen.

- [ ] [Schweregrad: niedrig] Dashboard zeigt Nicht-Führung „Verschlusssachen: 0"
  - Datei: Components/Pages/Home.razor (Kachel, ca. Zeile 47–56) + Services/DashboardService.cs (`verschlusssachen = 0` für Nicht-Führung, ca. Zeile 30)
  - Problem: Für Nicht-Führung ist die Zahl bewusst 0 – die Kachel wird aber trotzdem gerendert und
    suggeriert „es gibt keine Verschlusssachen".
  - Warum relevant: Falsche Information; zudem verschenkter Platz für eine relevante Kachel.
  - Vorschlag: Kachel per `AuthorizeView Policy=Fuehrung` ausblenden (oder durch z. B. „Meine zuletzt
    bearbeiteten Akten" ersetzen).

- [ ] [Schweregrad: niedrig] Ablehnen/Sperren ohne Begründungs-Eingabe trotz vorhandenem Feld
  - Datei: Components/Pages/Admin/Freigaben.razor (`Ablehnen`, ca. Zeile 177: hartkodiert „Registrierung abgelehnt") und Admin/Agenten.razor (`Sperren`, ca. Zeile 150: hartkodiert „Notfall-Sperre durch Admin"); Service-Signaturen nehmen `grund` entgegen
  - Problem: `GesperrtGrund` wird Nutzern auf der Gesperrt-Seite indirekt relevant und landet im Audit –
    die UI bietet aber keine Eingabemöglichkeit.
  - Warum relevant: Plan §9 Phase 5 sieht „Genehmigen/Ablehnen mit Begründung" vor; Audit-Trail ist so wenig
    aussagekräftig.
  - Vorschlag: Kleiner Begründungs-Dialog (Textfeld, optional mit Pflicht ab Ablehnung).

- [ ] [Schweregrad: niedrig] Profil-Speichern (Führung) wirft Nutzer nach ~1 Minute kommentarlos raus
  - Datei: Components/Pages/Konto/MeinProfil.razor (`SpeichernAsync`, ca. Zeile 120) + Services/AgentVerwaltungService.cs (`StammdatenAendernAsync` → `neuerStamp: true`)
  - Problem: Der erneuerte SecurityStamp invalidiert die laufende Sitzung; die Revalidierung trennt den
    Circuit nach bis zu 1 Minute. Die Snackbar sagt nur „Bitte melde dich neu an", der Rauswurf kommt
    dann aber überraschend mitten in der Nutzung.
  - Warum relevant: Wirkt wie ein Absturz/Session-Bug.
  - Vorschlag: Nach dem Speichern aktiv auf die Login-Seite leiten (`Nav.NavigateTo("/Account/Login…", forceLoad:true)`)
    oder den Nutzer mit `RefreshSignInAsync`-Äquivalent direkt neu einloggen.

- [ ] [Schweregrad: niedrig] Duplikat-Dialog: Klick auf Treffer verwirft das ausgefüllte Formular
  - Datei: Components/Pages/Personen/Shared/DuplikatDialog.razor (MudListItem mit `Href`, ca. Zeile 11)
  - Problem: Die Treffer sind direkte Links – Klick navigiert zur bestehenden Akte und alle bereits
    erfassten Steckbrief-Daten der neuen Person sind weg, ohne Warnung.
  - Warum relevant: Gerade beim sorgfältigen Prüfen verliert man die meiste Arbeit.
  - Vorschlag: Links mit `target="_blank"` öffnen oder im Dialog eine Mini-Vorschau zeigen statt zu
    navigieren.

- [ ] [Schweregrad: niedrig] Keine Rück-Ansicht „Doks dieser Organisation" auf Fraktion/Gruppe
  - Datei: Components/Pages/Fraktionen/FraktionDetail.razor und Gruppen/GruppeDetail.razor (kein Tab); Daten vorhanden: Data/Entities/Personen/PersonDok.cs (`OrgTyp`/`OrgId` + Index in AppDbContext.cs Zeile 180)
  - Problem: Doks verlinken auf die Organisation (klickbarer Link Dok→Fraktion), aber die Fraktions-/
    Gruppen-Akte zeigt nirgends „alle Doks mit Bezug zu dieser Org" – die Gegenrichtung fehlt.
  - Warum relevant: Plan-Grundsatz „jede Verknüpfung in BEIDE Richtungen klickbar"; der DB-Index
    (OrgTyp, OrgId) existiert bereits genau dafür.
  - Vorschlag: Tab „Doks" auf FraktionDetail/GruppeDetail mit Query über `OrgTyp`/`OrgId` (Anzeige wie
    DoksUebersicht, personenbezogen verlinkt).

---

## 6. Bedienbarkeit

- [ ] [Schweregrad: hoch] Enter zum Absenden fehlt in praktisch allen Formularen und Dialogen
  - Datei: Components/Pages/Personen/PersonNeu.razor, PersonBearbeiten.razor, FraktionNeu/FraktionBearbeiten, GruppeNeu/GruppeBearbeiten, Konto/MeinProfil.razor sowie ALLE Dialoge (DokDialog, QuelleDialog, TagDialog, MitgliedDialog, BeziehungDialog, …) – nirgends `OnKeyDown`/Submit-Verdrahtung; Gegenbeispiele: Layout/MainLayout.razor (`SucheKeyDown`, Zeile 77) und Suche/SucheSeite.razor (Zeile 165) haben Enter
  - Problem: MudForm/MudDialog submitten nicht automatisch auf Enter; der Nutzer MUSS jedes Mal zur Maus
    greifen. Bei den häufigsten Aufgaben (Person anlegen, Dok erfassen, Kommentar schreiben) bremst das
    jeden Workflow; KommentarPanel: Enter im Textfeld macht nur Zeilenumbruch, Absenden nur per Button.
  - Warum relevant: Höchster Reibungsfaktor im Alltag; zerstört Tastatur-Workflows komplett.
  - Vorschlag: Einheitliches Muster: in Dialogen `DefaultFocus`/`OnKeyDown(Enter) → Speichern()` bzw.
    MudForm in ein `<form @onsubmit>`/`MudButton ButtonType=Submit` einbetten; bei mehrzeiligen Feldern
    Strg+Enter.

- [ ] [Schweregrad: mittel] Kein Autofocus auf dem ersten sinnvollen Feld
  - Datei: PersonNeu.razor (Name-Feld), alle Erfassungs-Dialoge (z. B. DokAnlegenDialog: Person-Feld, TagDialog: Name); einzig Components/Querschnitt/Shared/CommandPalette.razor setzt `AutoFocus="true"` (Zeile 17)
  - Problem: Nach dem Öffnen einer Anlege-Seite/eines Dialogs muss erst ins Feld geklickt werden.
  - Warum relevant: Summiert sich bei jeder Erfassung; Sofort-Lostippen ist Standarderwartung.
  - Vorschlag: `AutoFocus="true"` auf dem jeweils ersten Eingabefeld (MudTextField/MudAutocomplete
    unterstützen es bereits, siehe CommandPalette).

- [ ] [Schweregrad: mittel] Listen-Zeilen nur per Maus-RowClick bedienbar (keine Links, keine Tastatur)
  - Datei: PersonenListe.razor (`RowClick`, ca. Zeile 28), FraktionenListe.razor, GruppenListe.razor, DoksUebersicht.razor (`OnRowClick`)
  - Problem: Navigation erfolgt über JS-RowClick statt echter Links: kein Öffnen im neuen Tab
    (Mittelklick/Strg+Klick), keine Tastaturfokussierung der Zeile, Screenreader erkennen kein
    interaktives Element.
  - Warum relevant: Akten parallel in Tabs öffnen ist ein Kern-Workflow für Ermittler; Barrierefreiheit.
  - Vorschlag: Mindestens die Namens-/Aktenzeichen-Spalte als `MudLink href` rendern (RowClick kann
    zusätzlich bleiben).

- [ ] [Schweregrad: mittel] Command-Palette bietet „Papierkorb" allen Nutzern an → ACCESS DENIED; Org-Papierkörbe fehlen
  - Datei: Components/Querschnitt/Shared/CommandPalette.razor (`_befehle`, ca. Zeile 55–66)
  - Problem: Der Befehl „Papierkorb" (`/personen/papierkorb`) ist nicht Führungs-gated – Nicht-Führung
    landet auf der vollflächigen Sperrseite. Außerdem gibt es nur den Personen-Papierkorb als Befehl,
    Fraktionen-/Gruppen-Papierkörbe fehlen, während „Tags verwalten" korrekt Führungs-gated ist.
  - Warum relevant: Schnellzugriff führt in eine Sackgasse; inkonsistente Befehlsliste bricht Vertrauen
    in die Palette.
  - Vorschlag: Papierkorb-Befehle in den `if (_istFuehrung)`-Block verschieben und alle drei aufnehmen.

- [ ] [Schweregrad: mittel] Zugehörigkeit von der Personen-Akte aus: Rang/Rolle/Leitung nicht setzbar
  - Datei: Components/Pages/Personen/Shared/ZugehoerigkeitDialog.razor (nur Typ + Akte wählbar) vs. Fraktionen/Shared/MitgliedDialog.razor (Rang + Leitung) und Gruppen/Shared/GruppeMitgliedDialog.razor (Rolle + Leitung)
  - Problem: Von der Personenseite aus wird das Mitglied immer ohne Rang/Rolle/Leitung angelegt; zum
    Vervollständigen muss man zur Org-Akte navigieren und dort „Mitgliedschaft bearbeiten" öffnen.
  - Warum relevant: Zwei zusätzliche Navigationsschritte für einen Standardfall; dieselbe Aktion
    verhält sich je nach Einstieg unterschiedlich (Muscle Memory).
  - Vorschlag: Rang-/Rolle-/Leitung-Felder in den ZugehoerigkeitDialog aufnehmen (Ränge der gewählten
    Fraktion lassen sich nach Auswahl nachladen).

- [ ] [Schweregrad: mittel] Kill-Switch „Sperren" ohne Bestätigungsdialog
  - Datei: Components/Pages/Admin/Agenten.razor (`Sperren`, ca. Zeile 150; Button Zeile 80–84)
  - Problem: Ein einzelner Klick sperrt den Account und beendet sofort alle Sitzungen – ohne Rückfrage
    (alle Löschaktionen der App haben dagegen BestaetigenDialog).
  - Warum relevant: Destruktivste Aktion der Adminseite ist die am leichtesten auslösbare; Fehlklick
    in der Zeile genügt.
  - Vorschlag: BestaetigenDialog (inkl. Begründungsfeld, siehe UX-Finding) vorschalten.

- [ ] [Schweregrad: niedrig] Kein Offene-Anträge-Badge an „Freigaben" in der Navigation
  - Datei: Components/Layout/NavMenu.razor (ca. Zeile 16–18)
  - Problem: Führung sieht erst nach Klick auf „Freigaben", ob etwas ansteht (Dashboard-Kachel zählt
    zudem die Namensänderungen nicht mit, s. Logikfehler).
  - Warum relevant: Freigaben bleiben liegen; neue Agenten warten unnötig.
  - Vorschlag: `MudBadge` mit Anzahl (Ausstehende + Namensänderungen) am NavLink; Zahl z. B. über einen
    leichten scoped Service cachen.

- [ ] [Schweregrad: niedrig] Mobile: Topbar-Suche ausgeblendet, Strg+K nicht verfügbar
  - Datei: Components/Layout/MainLayout.razor (Suchfeld `d-none d-md-flex`, Zeile 22–25)
  - Problem: Auf kleinen Screens gibt es weder Suchfeld noch Hotkey; Suche nur über den Nav-Punkt
    „Globale Suche" erreichbar (Drawer erst öffnen).
  - Warum relevant: Such-Einstieg ist DIE Kernfunktion; Desktop-Fokus ist geplant, aber „responsiv" steht
    im Plan.
  - Vorschlag: Auf <md ein Such-Icon in der Topbar zeigen, das direkt `/suche` öffnet (oder die
    CommandPalette per Button öffnet).

- [ ] [Schweregrad: niedrig] „Einstufung speichern" ist wortlos deaktiviert
  - Datei: Components/Pages/Personen/Shared/EinstufungPanel.razor (Button `Disabled="@(_neu == Aktuell)"`, ca. Zeile 34)
  - Problem: Solange die Auswahl der aktuellen Einstufung entspricht, ist der Button deaktiviert, ohne
    dass erkennbar ist warum (kein Tooltip/Hinweis).
  - Warum relevant: Nutzer, die nur eine Begründung nachtragen wollen, verstehen die Sperre nicht.
  - Vorschlag: Tooltip („Bitte zuerst eine andere Einstufung wählen") oder Hinweistext unter dem Button.

- [ ] [Schweregrad: niedrig] Uneinheitliche URL-/Nav-Struktur der Verwaltungsseiten
  - Datei: Components/Pages/Admin/Tags.razor (`@page "/tags"`) vs. Admin/Freigaben.razor (`/admin/freigaben`) und Admin/Agenten.razor (`/admin/agenten`); NavMenu.razor Abschnitt „VERWALTUNG"
  - Problem: Tags liegt im Admin-Ordner und Nav-Abschnitt „Verwaltung", aber unter der Top-Level-Route
    `/tags`.
  - Warum relevant: Erwartbarkeit von URLs (Bookmarks, Auffindbarkeit); Routen-Konvention bricht.
  - Vorschlag: Nach `/admin/tags` verschieben (alte Route ggf. als Redirect behalten) – oder bewusst
    dokumentieren, dass Tags „halb-öffentlich" ist.

---

## 7. Sicherheit

- [ ] [Schweregrad: hoch] Duplikat-Suche leakt Verschlusssachen an alle Agenten
  - Datei: Services/PersonService.cs (`FindeDuplikateAsync`, ca. Zeile 75–89) + Components/Pages/Personen/Shared/DuplikatDialog.razor (zeigt Name + Aktenzeichen + Link)
  - Problem: Die Query filtert NICHT nach `IstVerschlusssache`/`istFuehrung`. Legt ein Nicht-Führungs-Agent
    eine Person mit gleichem Namen/Telefonnummer an, zeigt der Warn-Dialog ihm Existenz, Namen und
    Aktenzeichen der klassifizierten Akte – über einen ganz normalen UI-Weg.
  - Warum relevant: Direkter, real ausnutzbarer Bruch der Verschlusssachen-Regel (gerade Namens-Raten ist
    trivial).
  - Vorschlag: `istFuehrung`-Parameter ergänzen und VS-Akten für Nicht-Führung ausfiltern; alternativ als
    neutralen Hinweis „Es existiert bereits eine (nicht einsehbare) Akte" ohne Details anzeigen.

- [ ] [Schweregrad: hoch] Löschen/Wiederherstellen ohne serverseitige Führungs-Prüfung
  - Datei: Services/PersonService.cs (`LoeschenAsync` Zeile 167, `WiederherstellenAsync` Zeile 177), Services/FraktionService.cs (Zeile 139/149), Services/PersonengruppeService.cs (Zeile 166/175)
  - Problem: Die Rechte-Matrix verlangt „Akten löschen … Führung/Admin", erzwungen wird das aber nur durch
    `AuthorizeView Policy=Fuehrung` um den Button (PersonDetail.razor ca. Zeile 27) bzw. die Seiten-Policy
    der Papierkorb-Seiten. Die Service-Methoden nehmen `ClaimsPrincipal handelnder` entgegen und nutzen ihn
    NUR fürs Audit.
  - Warum relevant: Plan §7 fordert „dienstgradbasierte Policies serverseitig erzwungen". Blazor-Server-
    Event-Dispatch mildert die akute Ausnutzbarkeit, aber jeder neue Aufrufer (Command-Palette-Befehl,
    künftige API, versehentlicher Button ohne Gate) erbt die Lücke unbemerkt.
  - Vorschlag: Zu Beginn der Methoden `if (!handelnder.IstFuehrung()) throw new UnauthorizedAccessException(...)`
    (bzw. gemeinsamen Guard-Helfer), analog zum vorhandenen `EinstufungHelfer.PruefeRangGate`.

- [ ] [Schweregrad: hoch] Verschlusssache wird bei schreibenden/lesenden Detail-Operationen nicht geprüft
  - Datei: Services/PersonService.cs (`AktualisierenAsync` Zeile 122, `EinstufungSetzenAsync` Zeile 191, `GetEinstufungVerlaufAsync` Zeile 204, `GetHistorieAsync` Zeile 436, `FotoHinzufuegenAsync`/`FotoEntfernenAsync` Zeile 391/415, `GetZugehoerigkeitenAsync` Zeile 213), Services/PersonDokService.cs (`ErstellenAsync`/`AktualisierenAsync`/`LoeschenAsync`), analog FraktionService/PersonengruppeService (`AktualisierenAsync`, Mitglieder-Methoden)
  - Problem: Nur `GetDetailAsync`/`GetListeAsync`/`SucheAsync` filtern VS. Alle anderen Methoden operieren
    bei bekannter Id ungeprüft auf klassifizierten Akten (lesen Verlauf/Historie, ändern Steckbrief,
    löschen Fotos, legen Doks an).
  - Warum relevant: „Verschlusssache greift in Suche, Listen und Graph" (Plan §7) – die Durchsetzung ist
    aber Komponenten-Disziplin statt Service-Garantie; eine vergessene UI-Prüfung genügt für einen Leak.
  - Vorschlag: Gemeinsamen Guard (`PruefeSichtbarkeitAsync(db, typ, id, handelnder)`) einführen und am
    Anfang jeder betroffenen Methode aufrufen; `istFuehrung` aus dem `handelnder` ableiten statt als
    separates bool von der UI durchzureichen (eine Quelle der Wahrheit).

- [ ] [Schweregrad: mittel] Quellen-Download/-Liste an VS-Fraktionen/-Gruppen ungeschützt
  - Datei: Services/QuelleService.cs (`GetFuerDownloadAsync` Zeile 112 + `AkteSichtbarAsync` Zeile 124: nur Person) → Endpoint Components/Querschnitt/QuellenDateiEndpointRouteBuilderExtensions.cs
  - Problem: Folge des Inkonsistenz-Findings oben, hier als konkreter Angriffsweg: Der anonyme-Id-basierte
    Download-Endpoint `/dateien/quellen/{id}` liefert Anhänge einer Verschlusssache-Fraktion an JEDEN
    aktiven Agenten aus (Quellen-Id reicht, z. B. aus alter Verlinkung/geteiltem Link).
  - Warum relevant: Datei-Anhänge sind oft das sensibelste Material der Akte.
  - Vorschlag: `AkteSichtbarAsync` auf Fraktion/Personengruppe erweitern (siehe Inkonsistenz-Finding).

- [ ] [Schweregrad: mittel] Kommentar-Löschen: Autor-/Führungs-Regel nur in der UI
  - Datei: Services/KommentarService.cs (`LoeschenAsync`, ca. Zeile 46–58: keinerlei Prüfung) vs. Components/Querschnitt/Shared/KommentarPanel.razor (`DarfLoeschen`, ca. Zeile 105: Autor oder Führung)
  - Problem: Die fachliche Regel („eigene oder Führung") existiert nur als Button-Sichtbarkeit; der
    Service löscht jeden Kommentar für jeden Aufrufer.
  - Warum relevant: Gleiche Service-Vertrauensproblematik wie oben; die Regel ist dokumentiert und sollte
    durchgesetzt werden.
  - Vorschlag: Im Service: `if (!handelnder.IstFuehrung() && kommentar.ErstelltVonId != handelnder.GetAgentId()) throw …`.

- [ ] [Schweregrad: mittel] Upload-Größenlimit serverseitig nicht erzwungen
  - Datei: Services/PersonService.cs (`FotoHinzufuegenAsync`, Zeile 391: prüft nur ContentType) und Services/QuelleService.cs (`ErstellenAsync`, Upload-Zweig: prüft nur Typ); Client-Prüfungen in FotoGalerie.razor (Zeile 55–62) und QuelleDialog.razor (`DateiGewaehlt`)
  - Problem: `MaxBytes` wird nur in den Komponenten geprüft (`datei.Size > FileStorage.MaxBytes`); die
    Services schreiben jeden Stream/Byte-Array ungeprüft auf Platte und vertrauen dem
    `groesse`-Parameter der UI. (`OpenReadStream(MaxBytes)` begrenzt zwar den heutigen UI-Pfad, aber kein
    anderer Aufrufer ist geschützt.)
  - Warum relevant: Disk-Filling über künftige Aufrufpfade; Validierung gehört laut Plan §7 zur Härtung.
  - Vorschlag: Im Service Stream-Länge prüfen (`inhalt.Length`/Zählung beim Kopieren) und bei
    Überschreitung ablehnen.

- [ ] [Schweregrad: niedrig] Link-Quellen ohne URL-Schema-Prüfung (javascript:-Links möglich)
  - Datei: Services/QuelleService.cs (`ErstellenAsync`, Link-Zweig ca. Zeile 50–56) + Anzeige Components/Querschnitt/Shared/QuellenPanel.razor (`MudLink Href="@q.Url"`, ca. Zeile 41)
  - Problem: Die URL wird ungeprüft gespeichert und als klickbarer Link gerendert; `javascript:`-URLs
    wären ein stored-XSS-Vektor (moderne Browser blocken vieles, aber nicht verlässlich alle Fälle).
  - Warum relevant: Defense in depth bei nutzergenerierten Links.
  - Vorschlag: Beim Speichern nur `http(s)://` (ggf. `discord://`) zulassen; sonst ablehnen oder
    automatisch `https://` voranstellen.

- [ ] [Schweregrad: niedrig] Agent-Selbstschutz (letzter Admin / Selbst-Sperre) nur clientseitig
  - Datei: Components/Pages/Admin/Agenten.razor (Disabled bei `context.Id == _meineId`, Zeile 64/82) vs. Services/AgentVerwaltungService.cs (`AdminSetzenAsync` Zeile 162, `SperrenAsync` Zeile 171: keine Prüfung)
  - Problem: „Sich selbst sperren/Admin entziehen" verhindert nur das UI-Disabled; der Service kennt den
    Unterschied nicht. Einen Schutz „mindestens ein Admin bleibt übrig" gibt es gar nicht.
  - Warum relevant: Fehlbedienung/künftige Aufrufer können das System administratorlos machen
    (nur noch per DB-Eingriff behebbar).
  - Vorschlag: Im Service prüfen: handelnder ≠ Ziel bei Sperre/Admin-Entzug, und vor Admin-Entzug
    `Users.Count(IstAdmin) > 1` sicherstellen.

- [ ] [Schweregrad: niedrig] Tag-Verwaltung (Umbenennen/Löschen) nur durch Seiten-Policy geschützt
  - Datei: Services/TagService.cs (`AktualisierenAsync` Zeile 50, `LoeschenAsync` Zeile 70: kein Check) vs. Components/Pages/Admin/Tags.razor (`[Authorize(Policy = Fuehrung)]`)
  - Problem: Anlegen ist bewusst für alle erlaubt (TagPickerDialog); Umbenennen/Hart-Löschen soll laut
    Seite Führungs-Sache sein, der Service erzwingt es nicht.
  - Warum relevant: Hartes Tag-Löschen entfernt das Label von ALLEN Akten – destruktive globale Aktion.
  - Vorschlag: Führungs-Guard in `AktualisierenAsync`/`LoeschenAsync`.

- [ ] [Schweregrad: niedrig] Rate-Limit nur auf Login-Start, nicht auf OAuth-Callback
  - Datei: Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs (`/PerformExternalLogin` mit `RequireRateLimiting`, Zeile 27–45; `/ExternalLogin` GET ohne, ab Zeile 47)
  - Problem: Der Callback führt DB-Arbeit aus (FindByLogin, ggf. CreateAsync) und ist unlimitiert.
  - Warum relevant: Geringe, aber vorhandene Spam-/Enumeration-Fläche.
  - Vorschlag: Dieselbe (oder eine eigene, etwas großzügigere) Rate-Limit-Policy auf den Callback legen.

- [ ] [Schweregrad: niedrig] Ressourcenbasierte Verschlusssachen-Policy bleibt Stub (Phase-2-Versprechen)
  - Datei: Authorization/VerschlusssacheAuthorizationHandler.cs (gesamt) + Plan.md Phase 1/2 („volle ressourcenbasierte Prüfung ab Akten-Phase")
  - Problem: Akten existieren seit Phase 2, der Handler prüft weiterhin nur `IstFuehrung()`; die im Plan
    erwähnte Variante „ausdrücklich zugewiesene Agenten dürfen VS sehen" ist nirgends umgesetzt (auch
    kein Datenmodell dafür).
  - Warum relevant: Geplante Funktionalität (zugewiesene Agenten) fehlt still; siehe auch Code-Qualität
    (tote Policy).
  - Vorschlag: Entscheiden: Feature jetzt bauen (Zuweisungs-Tabelle + Resource-Handler, von den
    Service-Guards genutzt) oder Plan/Doku auf „nur Führung" korrigieren.
