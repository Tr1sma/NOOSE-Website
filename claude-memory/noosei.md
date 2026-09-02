# NOOSEI (KI-Integration)

> **Lies das, bevor du `INooseiGateway`, `ILlmService`, ein Werkzeug unter `Services/Llm/Tools/`,
> `NooseiRecordTypes`, ein Kontingent oder die KI-Panels unter `/einstellungen` anfasst.**
> Kernregeln: **ein einziger Weg zum Modell** (`INooseiGateway.AskAsync`); ein Werkzeug filtert
> **selbst** über den `ViewerScope`; `NotFound()` ist für „existiert nicht" und „darfst du nicht sehen"
> **absichtlich identisch**; **echtes Geld sieht ausschließlich der KI-Eigner**, alle anderen rechnen
> in Kontingent-Token.
> Öffentliche Anbindung: [oeffentlich-suche.md](oeffentlich-suche.md)


- **Ein einziger Weg zum Modell:** `INooseiGateway.AskAsync`. Es prüft `Permission.RequireLlmUse`, dann das
  Wochenkontingent, führt aus (bei Werkzeugen mehrere Runden), bucht **genau einmal** ab und schreibt eine
  Zeile in `KiAnfragen`. `ILlmService` ist reiner Transport (eine Runde = ein HTTP-Call) und bleibt DB-frei.
  `NooseiGatewayCoverageTests` schlägt fehl, sobald ein vierter Produktionsdateiname `ILlmService` nennt.
- **Der Modellname ist aus `ILlmService` entfernt.** Keine Komponente *kann* ihn rendern; sichtbar ist er nur
  in `/einstellungen?tab=noosei`. Nach außen heißt alles NOOSEI.
- **Werkzeuge** (`Services/Llm/Tools/`) liefern deutschen Klartext und filtern **jeder für sich** über den
  `ViewerScope` des fragenden Agenten. `NooseiToolResult.NotFound()` ist für „existiert nicht" und „darfst du
  nicht sehen" **absichtlich identisch** — alles andere macht ein Werkzeug zum Existenz-Orakel für VS-Akten.
  Keine Schreibwerkzeuge: NOOSEI liest, Agenten schreiben.
- **Ein Werkzeug muss seine `Refs` liefern.** `NooseiToolResult.Refs` ist der einzige Weg, auf dem eine berührte
  Akte in die Quellen-Chips unter der Antwort (`KiNachrichten.Quellen`) und in die Aktenliste der Protokollzeile
  kommt — der `NooseiToolExecutor` gibt deshalb `NooseiToolOutcome(Text, Refs)` zurück, nicht nur Text. Refs ohne
  `Id` (die Werkzeug-Zähleinträge des Gateways) fallen bei den Chips bewusst raus.
- **Eine Anzahl ist eine Aussage über den Bestand.** `finde_akten` (Merkmalssuche und Zählung) fächert deshalb
  über die Listendienste aus und filtert im Speicher — die VS-Filterung kommt aus dem kanonischen Lesepfad, nicht
  aus einer zweiten Abfrage. Genauso `hole_kennzahlen`: `isLeadership` wird aus `Scope.MayClassifiedRead`
  **abgeleitet**, nie als `true` übergeben, sonst verrät ein Aggregat die Existenz eingestufter Akten, die kein
  Werkzeug nennen würde.
- **Welcher Aktentyp in welches Werkzeug darf, steht ausschließlich in `NooseiRecordTypes`** (`INooseiTool.cs`):
  eine `Uses`-Zeile je Typ mit den Flags `Read`/`List`/`Chronicle`, aus denen die Schema-Enums beim statischen
  Init berechnet werden. **Deutscher Name, Plural und die durchsuchbare Menge stehen dort NICHT** — sie kommen
  aus `SearchCatalog` (`NooseiUse.Search` ist der `SearchTraits.Assistant`-Trait, nicht eine Kopie davon).
  Zwei Label-Tabellen über dieselben Kategorien driften; abgeleitet können sie es baulich nicht.
  **Jedes Werkzeug mit `typ`-Parameter nimmt den geprüften Overload `Clr(german, NooseiUse.X)`** — nicht das
  nackte `Clr(german)`. Das Schema-Enum ist nur ein Hinweis, und ein durchgerutschter Typ landet in
  `Visibility.IsRecordVisibleAsync`. Drei Achsen statt einer Liste, weil sonst die schmalste Fähigkeit für alle
  entscheidet. `NooseiRecordTypesTests` prüft je Flag per Dateiscan, dass der Dienst dahinter es auch kann.
  `German(clr)` fällt nie auf den CLR-Namen zurück, sondern auf `"Eintrag"` — ein englischer Typname liest sich
  für das Modell wie eine Aktenart, die es öffnen darf.
- **`NooseiUse.Read` verlangt zwingend einen Arm in `Visibility.IsRecordVisibleAsync`.** Dessen Schwanz beantwortet
  jeden **unbekannten** Typ mit „für alle sichtbar" — für einen lesbaren Typ ist das ein Leck, kein Default.
  `EveryReadableType_HasAnArmInTheVisibilityGate` hält das per Dateiscan fest. `Job` und `Appointment` waren dort
  lange reine Existenzprüfungen, obwohl ihre echten Regeln in `JobVisibility`/`AppointmentVisibility` stehen; das
  blieb nur folgenlos, solange beide nicht lesbar waren. Ein neuer Arm **benennt** einen vorhandenen Helfer und
  schreibt nie ein eigenes Prädikat.
- **Ein Dossier ist ein Budget, Akteninhalte sind ein eigenes.** `lies_akte` liefert Stammdaten plus einen Auszug;
  Kommentare, Quellen, Wiedervorlagen, Doks, Chat, Tagesordnung und Bewerbungs-Schriftwechsel holt
  `lies_akteninhalt` (`ReadRecordContentTool`) paginiert und mit `MaxContentResultChars`. Es **gatet die Elternakte
  selbst** — `TagService.GetForRecordAsync` und `CustomFieldValueService` haben für interne Agenten kein eigenes
  Gate, sie verlassen sich auf die Seite, die sie rendert. Hier ist das Werkzeug diese Seite.
- **`finde_akten` zählt Akten, `lies_bereich` berichtet einen Zustand.** Die Trennung steht in beiden
  Beschreibungen und in `NooseiPrompts.ToolChoice`; ohne sie rät das Modell. In `lies_bereich` (`ReadAreaTool`)
  liest ein Bereich ohne Recht **wortgleich wie ein leerer** — `UnauthorizedAccessException` wird abgefangen,
  sonst wäre das Werkzeug ein Rechte-Orakel über Bereiche, die das Schema ohnehin nennt.
- **`DossierContextBuilder` ist `partial` über zwei Dateien** (Aktenarten / Betrieb). Der Drift-Scan liest
  `DossierContextBuilder*.cs` — wer nur die Hauptdatei scannt, bekommt einen falsch-roten Wächter und entschärft ihn.
- **Modell pro Funktion** über `LlmOptions.ModelByFeature` (`ModelFor(feature)`, leer = Standardmodell).
  `LlmService` löst es aus `request.Context.Feature` auf — es gibt bewusst kein `Model` auf `LlmRequest`, damit
  Funktion und Modell nicht auseinanderlaufen können. Sichtbar bleibt es nur in `/einstellungen?tab=noosei`.
- **Der Akten-Anker der Unterhaltung wird jede Runde neu geprüft**, nicht einmal beim Anlegen: `?akte=Typ:Id`
  ist eine Nutzereingabe, und die Systemzeile nennt die Akte beim Namen. Ohne
  `Visibility.IsRecordVisibleAsync` gegen den *aktuellen* Scope wäre der Anker ein Existenz-Orakel.
- **„Verbindung" heißt drei Tabellen, nicht eine.** Mitgliedschaften (`FraktionMitglieder`/`PersonengruppeMitglieder`/
  `ParteiMitglieder`), typisierte `PersonBeziehungen` und die manuellen `Verknuepfungen` — `GraphEdgeLoader` ist die
  kanonische Aufzählung. `zeige_verbindungen` deckt alle drei ab, **in beide Richtungen** (Person → ihre Fraktionen,
  Fraktion → ihre Mitglieder); `finde_verbindungsweg` geht über `IGraphService.FindPathAsync` mehrere Schritte weit.
  Ein neuer Verbindungstyp gehört in `GraphEdgeLoader` **und** in `ListRelatedTool`, sonst ist er für NOOSEI unsichtbar.
  Ebenso: eine Akte, die eine Zuordnung nur von einer Seite rendert, macht sie im Kurzbrief der anderen Seite unsichtbar
  (war so bei `DossierContextBuilder`: Fraktionen listeten ihre Mitglieder, Personen nicht ihre Fraktionen).
- **`lies_kalender` nimmt bewusst nur `ICalendarService`, keinen `IDbContextFactory`.** Neun Quellen mit neun
  eigenen Sichtbarkeitsregeln (Agenda am Uhrzeit-Gate, Aufgaben am Kippschalter, Abmeldungen am Roster) laufen
  dort schon durch; ohne DB-Handle gibt es im Werkzeug baulich keinen Weg daran vorbei. `CalendarEntry.EntityType`/
  `EntityId` setzt **nur**, wer schon entschieden hat, dass der Betrachter die Akte kennen darf — eine
  Wiedervorlage auf eingestuftem Elternteil trägt weder Titel noch Link noch Referenz.
- **Der Kurzbrief-Cache wird auf minimalem Privileg erzeugt** (`DossierScope.ForRecord`), weil eine Zeile pro
  Akte von jedem gelesen wird, der die Akte sehen darf. `DossierContextBuilder.BuildAsync(..., scope: null, ...)`
  wählt genau das; der Werkzeug-Pfad übergibt den echten Scope.
- **Die Werkzeugaufrufe einer Runde laufen zusammen**, nicht nacheinander (`Task.WhenAll`); vier serialisierte
  Aktenlesungen sprengen sonst das Turn-Budget, das jede einzelne mühelos einhält. Zwei Regeln hängen daran:
  die **Wiederholungserkennung bleibt sequenziell** und in der Reihenfolge des Modells (sonst entscheiden zwei
  identische Aufrufe je nach Laufzeit unterschiedlich, und die Refs verlieren die Ordnung, auf die
  `MaxSources` beim Deduplizieren baut), und das **Turn-Budget wird genau einmal *nach* dem Bündel geprüft**
  (`turnCts.Token.ThrowIfCancellationRequested()`) — `RunToolAsync` wirft deshalb nie, auch nicht bei
  Abbruch, weil ein liegengelassener Werkzeug-Task später als unbeobachtete Ausnahme ohne zugehörige Anfrage
  auftaucht.
- **Ein abgelaufener Turn liefert aus, was da ist.** Läuft die Zeit ab, während ein Werkzeug noch liest, wird
  der Text der letzten Runde mit `Truncated` und einem deutschen Hinweis ausgeliefert statt einer Ausnahme —
  120 Sekunden Spinner, Kontingent belastet und kein Wort war die schlechteste aller Antworten. Der
  **Abbruch durch den Agenten fällt weiter durch** (Unterscheidung über `!cancellationToken.IsCancellationRequested`),
  und ohne bereits erzeugten Text wird nichts gerettet. Die Zeile ist `Erfolg = true` **mit**
  `Fehlerart = Timeout`: der Agent hat eine Antwort bekommen, und genau diese Kombination zählt die Auswertung.
- **`KiAnfragen` trägt acht nullable Betriebsspalten** (`Abschlussgrund`, `Versuche`, `ModellDauerMs`,
  `Werkzeugaufrufe`, `Werkzeugfehler`, `Eingeschraenkt`, `AbbruchGrund`, `Fehlerart`), befüllt über
  `LlmRequestTrace` am `LlmChargeInput`. **Nullable heißt „nicht gemessen"**, 0 heißt „gemessen und keins" —
  eine Zeile von vor der Migration darf nicht als Turn ohne Werkzeuge durchgehen. `DauerMs − ModellDauerMs`
  ist das Werkzeugbudget und die einzige Zahl, die ein langsames Modell von einer langsamen Datenbank trennt.
  Ausgewertet in `/einstellungen?tab=ki-betrieb` (`NooseiHealthPanel`) — **ausschließlich in Kontingent-Token**,
  damit `NooseiCostVisibilityTests` dort gar nicht erst greifen muss. Die Werkzeug-Rangliste ist eine
  **Stichprobe** der neuesten Zeilen (die Namen liegen in `Kontextrefs`, das kein Index erreicht) und sagt das
  auch dazu.
- **Kontingente:** ISO-Woche, Reset Montag 00:00 lokal, träge beim Lesen (kein Worker), Vorbild ist das
  Finanzierungsbudget. **1.000 Kontingent-Token = 1 Cent** echter API-Kosten (`usage.cost` von OpenRouter).
  Der Übertrag ist auf `Basis · %/100` **gedeckelt** (`LlmQuotaMath.CarryOut`) und wird auch **beim Lesen**
  geklemmt — anders als beim Finanzierungsbudget, das compoundieren kann.
- **Tagesgrenze zusätzlich zur Woche:** `LlmRankQuota.DailyPercent` (Standard 40 %), durchgesetzt in
  `EnsureAvailableAsync`. Gemessen an der **Basis, nicht an Basis + Übertrag** — eine Woche mit Übertrag soll
  länger reichen, nicht schneller ausgebbar sein; ein individuelles `LlmQuotaOverride` verschiebt sie mit.
  **Die Wochensperre schlägt die Tagessperre** (`IsDayBlocked` enthält `!IsBlocked`), sonst verspräche die
  Meldung „ab morgen früh" etwas, das erst am Montag stimmt. Die Burn-Rate-Regel R2 *meldet* weiterhin nur;
  gestoppt wird hier.
- **Der Kontingent-Lesepfad fragt vier Mal, egal für wie viele.** `QuotaSnapshot.LoadAsync` holt geschlossene
  Perioden, Verbrauch je Woche, Korrekturen und den heutigen Verbrauch gruppiert; `CloseElapsedAsync` liest
  danach gar nichts mehr, auch nicht im `ReadPredecessor`-Sonderfall. Vorher waren es sieben Abfragen **je
  Agent** — bei dreißig Agenten 210 Round-Trips in einem synchronen Render, und derselbe Pfad hängt hinter
  jeder Antwort, weil die Abrechnung mit einem Status-Lesen abschließt.
- **Nur der KI-Eigner ändert Kontingente** (`Ki:OwnerDiscordId(s)` → Claim `noose:kiowner` → `Policies.AiOwner` /
  `Permission.RequireAiOwner`). Führung und andere Admins lesen nur. Bewusst eine eigene Achse neben den
  Bootstrap-Admins, von denen es mehrere gibt.
- **`RequireLlmUse` sperrt Partner, Demo und die Nur-Lese-Aufsicht.** Unterhaltungen (`KiUnterhaltungen`)
  sind besitzer-privat, liegen **nicht** im globalen Papierkorb, und ihr `RechteStempel` verwirft beim
  Replay alle Werkzeug-Antworten, sobald sich der Scope des Besitzers geändert hat. Weil es keinen Papierkorb
  gibt, ist `DeleteAsync` ein **Hard-Delete** — der Dialog im Chat sagt deshalb „endgültig" und „nicht
  wiederherstellbar".
- **Die Werkzeug-Spur unter einer Antwort kommt aus den gespeicherten `tool`-Zeilen**, nicht aus einer zweiten
  Kopie auf der Assistant-Zeile — sonst zeigen frische und wiedergeöffnete Unterhaltung Unterschiedliches.
  Folge: ein Aufruf ohne Ergebnis fehlt in der Spur, weil genau diese Zeilen bewusst nicht gespeichert werden.
  Deutsche Bezeichnung und Fortschrittstext eines Werkzeugs stehen zusammen in `NooseiToolLabels` — getrennt
  driften sie, und ein neues Werkzeug erscheint dann in der einen Liste als roher Bezeichner.
- **Vorschlags-Chips kosten nichts.** Startfragen sind fest, Folgefragen werden aus den Quellen der letzten
  Antwort abgeleitet — kein Modellaufruf. Ein Chip **füllt nur das Eingabefeld**, dieselbe Regel wie bei einer
  aus der Befehlspalette übernommenen Frage: Senden bleibt eine bewusste Handlung, weil es Kontingent kostet.
- **Der Bild-Platzhalter des Editors ist ein Attribut, kein `src`.** `richtext.js` ersetzt jedes base64-Bild
  durch `data-noosei-bild="n"` und tauscht es beim Übernehmen zurück. Ein Platzhalter *im* `src` funktioniert
  nicht: `src` ist ein URI-Attribut, und `HtmlCleanup` wirft jedes unbekannte Schema weg — das löschte das Bild
  still aus dem korrigierten Dokument. Deshalb geht der Editor-Pfad über `HtmlCleanup.CleanAiPayload`
  (Allowlist + Marker), alle anderen Pfade über `Clean`, das den Marker bewusst verwirft.
- **Echtes Geld sieht ausschließlich der KI-Eigner.** Alle anderen — Agenten, Führung, andere Admins, die
  Nur-Lese-Aufsicht — rechnen in Kontingent-Token. Zwei Schichten sichern das: Beträge stehen nur unter
  `Components/Pages/Admin/` (Seite hängt an `Policies.LeadershipPage`, Anfragen-Protokoll zusätzlich an
  `Policies.CounterIntel`), **und** dort nochmals hinter `IsAiOwner()` — als `_maySeeCost` in den Panels, als
  Parameter `MaySeeCost` im `LlmRequestDetailDialog` (Standard `false`, damit ein neuer Aufrufer nichts leakt).
  Auch der Umrechnungssatz „1.000 Token = 1 Cent" ist ein Preis und fällt darunter. `NooseiCostVisibilityTests`
  prüft beide Schichten per Dateiscan. `LlmQuotaMath.ToCents`/`ToCost` bleiben — nur die Anzeige ist begrenzt.
- **Wochenkosten:** `LlmCostForecast.MaxTokens` summiert `Available` (Basis **+** Übertrag, nicht die Rang-Basis)
  über den `OnlySelectable()`-Bestand — das Maximum, wenn alle restlos verbrauchen. `Expected` mittelt die
  **abgeschlossenen** Wochen aus `ILlmRequestLogService.GetWeeklySpendAsync`; die laufende Woche ist als
  `Running` markiert und fliegt raus, sonst fiele die Prognose jeden Montag ab und stiege bis Sonntag wieder.
- **Verbrauch wird beim Erzeugen gebucht, nicht beim Übernehmen.** Der Editor-Dialog meldet die Kosten über
  `NooseiDialog.OnGenerated`, sobald die Antwort da ist. Verwerfen, Escape und ein zweiter Anlauf kosten
  genauso; über das Dialog-*Ergebnis* zu buchen zeigte nach einem „Verwerfen" die Zahlen des letzten
  *angenommenen* Durchgangs an. `_lastDiscarded` wird erst nach dem erfolgreichen `applyAiResult` gelöscht.
- **Kontingent-Lesepfade nehmen den `ClaimsPrincipal`** (`Permission.RequireQuotaRead`: eigenes immer, fremdes
  bzw. der ganze Bestand nur mit `MayClassifiedRead`). Die Nur-Lese-Aufsicht sieht die Zahlen, die
  Anomalie-Auswertung darüber bleibt hinter `RequireLeadershipNoReader`.
- **Editor-Korrektur sieht nie Markup:** `TextBlocks` zerlegt das HTML in nummerierte Klartext-Blöcke und
  schreibt die Korrektur in die Textknoten zurück. Formatierung, Gliederung und base64-Bilder sind damit
  mechanisch geschützt, nicht per Prompt. Danach laufen harte Prüfungen (Zahlen, Aktenzeichen, `{{…}}`,
  Erwähnungen, Bewerbungs-Tokens) — ein Verstoß verwirft die Antwort.
