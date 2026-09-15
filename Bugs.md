# Bugs.md — Befunde aus dem Review seit `8893f17`

**Geprüfter Stand:** `7e2a19e` („Add rich text editor paste cleanup and structure blocks"), Arbeitsbaum sauber.
**Nachkontrolliert gegen `2a0f921`:** seither sind nur drei Doku-/Template-Commits gelandet
(`b64d566`, `3aed09c`, `d47fb7d` — DeepSeek-Schlüssel in Vorlage und Anleitung, Löschen von
`Richtexteditor.md`). **Kein Codebefund dieser Liste ist davon berührt**, und die Doku-Befunde 36 und 37
stehen auch auf dem aktuellen Stand noch.
**Umfang:** 25 Commits, ~40.000 neue Zeilen — Handbuch, Changelog, Onboarding, Rechte, LinkPreview,
LinkDialog-Zusammenlegung, Dienstnummer, NOOSEI-Anbieterumschaltung und der gesamte Texteditor-Ausbau.

**Verfahren:** 23 Finder über den Diff, danach jeder Befund gegen den Code nachgeprüft — die schwersten
von mir selbst, der Rest von unabhängigen Prüfern mit dem Auftrag zu widerlegen. 45 Befunde erhoben,
**44 bestätigt, 1 widerlegt** (steht unten unter „Sieht aus wie ein Fehler, ist keiner").

Reihenfolge: **P0** (Datenverlust, Geld, Sicherheit) → **P1** (tut nachweislich das Falsche) →
**P2** (Randfall, Last, stiller Teilausfall) → **P3** (Inkonsistenz, Doku).

---

## Stand der Behebung

Build grün, **738 Tests der betroffenen Bereiche grün**. Keine Migration nötig.

**Behoben:** 1, 2, 3, 4, 5, 6, 7, 10, 12, 13, 14, 15, 16, 17, 26, 35, 36, 37, 43.

| # | Was geändert wurde |
|---|---|
| 1 | `LlmQuotaStatus` trägt jetzt `RawBaseWeekly` + `BoostPercent`; der Dialog füllt sich aus dem **rohen** Wert und zeigt die Wirkung des Aufschlags getrennt aus. Regressionstest hält den Rundlauf fest. |
| 2 | Suchen/Ersetzen baut den Heuhaufen aus `getContents()` mit einem Platzhalter je Embed — String-Offset und Quill-Index sind wieder dieselbe Zahl. |
| 3 | `id` ist aus der globalen Sanitizer-Freigabe raus und wird über `RemovingAttribute` **nur auf `h1`–`h6`** behalten; die beiden Anker-Tests auf die tatsächliche `ue`-Transliteration korrigiert. |
| 4 | Der Kandidatenfilter des Interceptors sieht jetzt auch Überschriften, nicht nur Bilder. |
| 5 | Neue `RichTextAnchorFields` (Obermenge von `RichTextImageFields`): Handbuch-Artikel und Glossarbegriffe bekommen Anker, ihre Bilder bleiben wie gehabt inline. Zwei Stolperdrähte halten die Liste gegen das Modell. |
| 6 | `AnnouncementForm` reicht `MarkSavedAsync` durch, `PublicFaqPanel` ruft es nach Erfolg. Für die drei **Dialoge** gibt es `RichTextEditor.DiscardDraftAsync(js, agentId, key)` — der Aufrufer verwirft den Entwurf erst, wenn der Schreibvorgang wirklich durch ist. |
| 7 | `?v=16`. |
| 10 | `RichTextFigure.ToStored` faltet nur noch eine Zeile mit **genau einem** Bild-Element; Mehrbild- und verlinkte Bilder bleiben unangetastet (zwei neue Tests). |
| 12, 13, 15, 16, 17, 26, 35, 36, 37, 43 | Von einem zweiten Agenten umgesetzt und von einem unabhängigen Prüfer gegengelesen — alle sieben Punkte korrekt, nichts außerhalb des Auftrags angefasst. |

**Zweite Runde, ebenfalls behoben:** 8, 9, 18, 20, 21, 22, 24, 25, 27, 28, 30, 31, 34, 42, 44.

| # | Was geändert wurde |
|---|---|
| 8 + 31 | Der Slug ist auf **64** Zeichen begrenzt — Spalte, Bereinigung und Eingabefeld gemeinsam, mit der Kopplung an die Suchindex-Breite als Kommentar an beiden Enden. Deckt auch den Kapitel-Fall (Spalte 80) ab. Der Schnitt trimmt jetzt einen Trennstrich am Ende weg. |
| 9 | Beide Kompromittierungs-Picker filtern auf `LinkService.KnownTypes` wie der @-Picker, und `AddCompromiseAsync` prüft den Typ **serverseitig** — der SignalR-Pfad bleibt sonst offen. |
| 11 | **Halb.** Der echte Sackgassen-Fall ist weg: neues `GetAllTermsAsync`, die Redaktion sieht ausgeblendete Begriffe markiert und kann sie zurückholen. Kapitel und Artikel bleiben beim Ausblenden weiterhin nur über die Datenbank erreichbar. |
| 18 | `_allChapters`/`_allArticles` — die beiden Listen, die ganze Artikel samt `longtext` laden — werden nur noch für Redakteure geholt. |
| 20 | Ein Klassen-Attributor `noose-toc` hält die Ebenen des Inhaltsverzeichnisses über den Einfüge-Vorgang; die CSS hängt jetzt am Listeneintrag statt an einem Wrapper, den Quill ohnehin neu baut. |
| 21 + 22 | Markdown-Kürzel und Schrägstrich-Menü steigen im Codeblock aus; „Text" räumt alle Blockformate ab statt nur der Überschrift. |
| 24 | Abgelaufene Wochen werden mit dem **rohen** Grundkontingent geschlossen. Bewusst konservativ: kann nur zu wenig, nie zu viel gewähren. Exakt wäre eine Boost-Spalte auf der Periodenzeile — die braucht eine Migration. |
| 25 | Artikel- und Glossar-Quellenchips werden getrennt gedeckelt; die Artikel behalten ihre Plätze. |
| 27 | Der Schritt „Menü angepasst" trägt keinen Link mehr, sondern sagt, wo das Zahnrad sitzt. Der Test verbietet jetzt ausdrücklich eine Route auf `/dashboard`. |
| 28 | `GlossaryTermView` trägt `ArticleId` und `IsVisible`; das Preset nimmt beide aus der Zeile statt sie aus der Leseansicht zurückzurechnen. |
| 30 | Beide Stempelstellen prüfen `IsInternalAgent` wie die anderen drei. |
| 34 | `Designation` ist entfernt. |
| 42 | Beide Rechtstexte haben eine Vorschau — und ein **neuer** Scan-Test prüft die Regel über `PublicRoutes` statt über den Ordnernamen, damit die nächste öffentliche Seite außerhalb von `Pages/Public` nicht wieder durchrutscht. |
| 44 | Die drei Seiten tragen `Policies.InternalAgent`, statt sich allein auf das Layout-Gate zu verlassen. |

**Offen:** 19, 23, 29, 32, 33, 38, 39, 40, 41 — und die Kapitel/Artikel-Hälfte von 11.
Keiner davon kostet Daten oder Geld; 38 braucht eine Migration, 23 einen Eingriff in den Abmelde-Pfad.

---

## Dritte Runde: Nachprüfung durch drei unabhängige Prüfer

Zwei Bereiche waren in der ersten Runde nie geprüft worden — die zuständigen Agenten wurden abgebrochen,
bevor sie etwas lieferten. Beide wurden nachgeholt; dazu eine adversariale Prüfung der Fixes selbst.

**NOOSEI-Anbieterumschaltung (`3daa507`) — sauber.** Kein P0/P1/P2. Insbesondere geprüft und in Ordnung:
`ILlmService` ist DB-frei geblieben; Adresse und Schlüssel werden je Anfrage auf der `HttpRequestMessage`
gesetzt, nicht auf dem gepoolten Client — ein Schlüssel kann nicht an den fremden Endpunkt geraten, und ein
Test hält genau das fest; ein Anbieter ohne Schlüssel lässt sich gar nicht erst auswählen; DeepSeek meldet
keine Kosten, aber der Token-Preis-Boden verhindert eine Nullbuchung; der Schreibpfad trägt
`Permission.RequireAiOwner` als erste Anweisung; keines der beiden Panels zeigt echtes Geld.
Ein P3 bleibt offen: `DossierSummaryService` liest den Anbieter für das Modell-Label ein zweites Mal statt
ihn aus der Antwort zu nehmen — unter der 10-Sekunden-Cache-Race steht dort der falsche Name. Reines
Anzeigefeld, die Abrechnung läuft korrekt über das Anfrageprotokoll.

**Handbuch-Seeder und Schema — drei Befunde, alle behoben:**

| Befund | Behebung |
|---|---|
| **P1** Modell sagte `HasMaxLength(64)`, Migration und Datenbank stehen auf `varchar(120)` — die Grenze galt nur im C#-Modell, und der nächste Scaffold hätte ein überraschendes `ALTER` vorgeschlagen. **Das war ein Fehler in meinem eigenen Fix.** | Spalte zurück auf 120. Die echte 64er-Regel lebt in `HandbookService.Slug`, wo jeder Schreibpfad durchläuft — ohne Migration, ohne Drift. |
| **P2** Kein Test auf die Slug-Länge, und der Seeder schreibt den ausgelieferten Slug roh durch. Ein künftiger Inhalt über 64 Zeichen hätte den **App-Start** abgebrochen. | Neuer Stolperdraht `Every_shipped_slug_fits_the_search_index`. |
| **P2** Zwei Artikel beanspruchten denselben `NavKey` „handbuch" — der Hilfe-Knopf hätte nur den mit der kleineren Sortierung gefunden. | Der Zweitanspruch ist entfernt, `HandbookContent.Revision` hochgezählt, und `Every_shipped_nav_key_is_claimed_only_once` fängt den nächsten Fall ab. |

**Adversariale Prüfung der Fixes — zwei P1, beide behoben:**

| Befund | Behebung |
|---|---|
| **P1** `RichTextFigure.ToStored` zählte ein Quill-`<br>` als Inhalt, wodurch die Faltung im Normalfall nicht mehr griff — die Beschriftung wäre ganz ausgefallen. | `<br>` wird vor der Zählung übersprungen, mit eigenem Test. |
| **P1** Der zweite Commit änderte `richtext.js` erneut, ohne `?v=` zu erhöhen — genau der Fehler, den Befund 7 beschreibt. | Beide Importstellen auf `?v=17`. |

Dazu drei P3 aus derselben Prüfung: `ArticleIdOf` war toter Code (entfernt); `RichTextAnchors.ToStored`
behielt eine vorhandene `id` unbesehen, sodass eine eingefügte Überschrift ihren eigenen Namen mitbrachte
(nur noch Werte, die diese Regel selbst erzeugt hätte, überleben — mit zwei Tests); und der Übertrag einer
Woche, die stabil unter Aufschlag lief, fällt jetzt grundsätzlich niedriger aus als früher. Letzteres ist
die dokumentierte, bewusst konservative Seite von Befund 24.

Ausdrücklich geprüft und **in Ordnung**: der Indexraum von Suchen/Ersetzen (Embed = genau ein Zeichen,
kein rohes NUL-Byte im Quelltext), der `id`-Handler des Sanitizers, `RichTextAnchorFields` als echte
Obermenge der Bild-Träger, die Redaktions-Bedingung im Handbuch (spiegelt
`Permission.RequireHrbOrLeadershipWrite` exakt), die drei neuen `InternalAgent`-Gates (sperren niemanden
aus, der vorher hereindurfte) und beide neuen `IsInternalAgent`-Filter.

---

## P0 — Kritisch

### 1. Kontingent-Dialog schlägt den Aufschlag ein zweites Mal auf — Ratsche auf echtes Geld
`NOOSE-Website/Components/Pages/Admin/Shared/LlmQuotaOverrideDialog.razor:47`

`LlmQuotaStatus.BaseWeekly` ist seit `3daa507` **nicht mehr** das gespeicherte Grundkontingent, sondern
`LlmQuotaMath.Boosted(override ?? rules.BaseWeekly, boostPercent)` (`LlmQuotaService.cs:347`). Der Dialog
füllt sein Feld „Grundkontingent" mit genau diesem aufgeschlagenen Wert und gibt ihn bei „Übernehmen"
unverändert an `SetOverrideAsync` zurück.

**Ablauf:** DeepSeek aktiv mit +400 %. Agent mit `BaseWeekly` 50.000 hat kein Override, die Zeile zeigt
250.000. Der KI-Eigner öffnet „Kontingent anpassen", schaltet „Individuelles Kontingent" ein und klickt
**ohne die Zahl anzufassen** auf Übernehmen. Gespeichert wird 250.000, wirksam ist danach
`Boosted(250.000, 400)` = 1.250.000 — das 25-fache der Rangregel. Jedes weitere Öffnen+Übernehmen
multipliziert erneut mit 5.

**Warum kritisch:** Das Wochenkontingent ist der einzige Deckel auf echten API-Ausgaben, und
`MaxBoostPercent = 400` existiert laut Kommentar genau deshalb („beyond that a wrong digit costs real
money"). Der Fehler ist unsichtbar: das Feld beschriftet den aufgeschlagenen Wert als „Grundkontingent",
und die Vorschau im Dialog rechnet ebenfalls auf der falschen Basis. Kein Test greift — `.razor` ist ohne
bUnit nicht testbar, und `TheBoost_AlsoLiftsAnIndividualOverride` prüft nur den Lesepfad.

**Richtung:** `LlmQuotaStatus` um den rohen Wert erweitern (`agent.LlmQuotaOverride ?? rules.BaseWeekly`,
gesetzt **vor** dem `Boosted`-Aufruf) und den Dialog daraus speisen. Dazu ein Rundreise-Test: Status lesen →
`SetOverrideAsync` mit dem angezeigten Wert → erneut lesen, `BaseWeekly` muss gleich bleiben.

---

### 2. „Alle ersetzen" rechnet im falschen Indexraum und zerstört gespeicherten Text
`NOOSE-Website/wwwroot/js/richtext.js:1086`

`neuSuchen` baut die Trefferliste aus `editor().getText()` und legt die reinen **String**-Positionen ab.
Dieselben Zahlen gehen anschließend als Quill-**Dokument**indizes in `setSelection`, `getContents`,
`deleteText` und `insertText`. Quill 1.3.7 filtert Embeds in `getText` restlos heraus, während jedes Embed
im Indexraum genau eine Position belegt — nach dem ersten Bild oder Erwähnungs-Chip laufen beide Räume
auseinander.

**Ablauf:** Dokument beginnt mit einem eingefügten Bild, danach steht „Meier war vor Ort. Meier sagte aus."
Strg+F, „Meier" → die Markierung sitzt schon eine Stelle zu weit links. Klick auf „Alle ersetzen" mit
„Müller" löscht an jeder Fundstelle die um die Zahl der vorangehenden Embeds verschobenen Zeichen. Liegt
ein Erwähnungs-Chip im Löschbereich, verschwindet er mitsamt dem gespeicherten `@{Typ:GUID}`-Token.

**Warum kritisch:** Nach dem Speichern ist der beschädigte Text und die verlorene Referenz persistiert. Der
Schutz, der genau das verhindern soll — `beruehrtEmbed` (`richtext.js:1062`) — prüft denselben falschen
Index und kann den Schaden nicht sehen; der Kommentar darüber behauptet das Gegenteil.

**Richtung:** Den Heuhaufen im Dokument-Indexraum bauen statt aus `getText()`: über `getContents()` laufen
und jedes nicht-String-`insert` durch genau ein Platzhalterzeichen ersetzen. Dann stimmen String-Offset und
Quill-Index überein und ein Treffer kann ein Embed nie überlappen.

---

## P1 — Hoch

### 3. Drei Tests sind auf `master` rot
`NOOSE-Website.Tests/Services/HtmlCleanupTests.cs:367`, `NOOSE-Website.Tests/Services/RichTextAnchorsTests.cs:48`

Nachgemessen gegen `7e2a19e`: `Fehler: 3, erfolgreich: 1062`.

- **`Clean_DisallowedIdAttribute_IsRemoved`** verlangt, dass `id` aus sanitisiertem HTML entfernt wird.
  Commit `7e2a19e` hat `"id"` in die **globale** Attribut-Freigabe aufgenommen
  (`HtmlCleanup.cs:92`). Im selben Commit kam `Clean_HeadingIdAndDivider_AreKept` dazu, das das
  Gegenteil verlangt — **zwei Tests derselben Datei widersprechen sich jetzt.**
- **`Slug_TransliteratesGermanAndDropsPunctuation`** und **`BuildToc_UsesTheSameSlugsAsTheAnchors`**
  erwarten `über` → `uber`. Der Code macht `ü` → `ue` (`RichTextAnchors.cs:83`), identisch zu
  `HandbookService.Slug`. **Hier ist der Test falsch, nicht der Code.**

**Warum:** Ein rotes `master` nimmt der Suite ihre Aussagekraft — der nächste echte Regressionstreffer geht
im Rauschen unter.

**Richtung:** Bei den Anker-Tests die Erwartung auf `ueber` ziehen. Beim `id`-Attribut eine Entscheidung
treffen: entweder `id` global erlauben (dann den alten Test löschen **und** begründen, dass beliebige
Nutzer-`id`s in jedem Kommentar, Ticket und auf öffentlichen Seiten landen dürfen) oder `id` aus der
globalen Liste nehmen und nur auf `h1`–`h6` durchlassen (`PostProcessNode`). Die zweite Variante ist die
engere: gebraucht wird `id` ausschließlich für Überschriften-Anker.

---

### 4. Überschriften-Anker werden nie vergeben — das Inhaltsverzeichnis ist im Normalfall tot
`NOOSE-Website/Infrastructure/RichTextHtmlInterceptor.cs:71`

`RichTextAnchors.ToStored` läuft nur für Spalten, die `Candidates()` vorher eingesammelt hat. Der Filter
lässt eine Spalte aber nur durch, wenn ihr HTML `"data:image"` **oder** `noose-bildtext` enthält. Ein Text
mit Überschriften und Inhaltsverzeichnis, aber ohne Bild, erfüllt keine der beiden Bedingungen.

**Ablauf:** Agent öffnet `/dokumente/{id}/bearbeiten`, tippt eine `h2`-Überschrift, klickt
„Inhaltsverzeichnis einfügen", speichert. Gespeichert wird `<a href="#lagebild">`, aber
`<h2>Lagebild</h2>` **ohne** `id`. In der Leseansicht passiert beim Klick nichts. Fügt derselbe Agent
zusätzlich einen Screenshot ein, funktioniert derselbe Link plötzlich.

**Warum:** Das Inhaltsverzeichnis ist genau dann kaputt, wenn es gebraucht wird — langer Text ohne Bilder.
Der Ausfall ist stumm. `RichTextAnchorsTests` prüft nur die reine Funktion, nie den Weg durch den
Interceptor; `RichTextHtmlInterceptorTests` enthält kein Dokument mit Überschriften.
Der ausgelieferte Changelog-Eintrag `2.1.12-editor-struktur` verspricht die Funktion bereits.

**Richtung:** Den Anker-Durchlauf vom Bild-Interceptor lösen und für jede Rich-Text-Spalte eigenständig
laufen lassen — oder als Sofortmaßnahme den Filter um `<h1`/`<h2`/`<h3` erweitern.

---

### 5. Der Inhaltsverzeichnis-Knopf steht überall, Anker bekommen aber nur sechs Trägertypen
`NOOSE-Website/wwwroot/js/richtext.js:1968`

Die Werkzeugleiste zeigt `noose-toc` in **jedem** nicht-kompakten Editor. Die `id`-Attribute vergibt
ausschließlich `RichTextHtmlInterceptor`, und der arbeitet nur die sechs in `RichTextImageFields`
registrierten Träger ab (`Document`, `AgentActivity`, `Meeting`, `MeetingAgendaItem`, `Announcement`,
`AgentNote`).

**Ablauf:** HRB bearbeitet einen Handbuch-Artikel, gliedert ihn mit `h2`, klickt „Inhaltsverzeichnis
einfügen", speichert. Unter `/handbuch/{slug}` zeigt jeder Eintrag ins Leere. Dasselbe für alle Vorlagen,
Presse, öffentliche Seiten, Warnungen, Lageberichte, Fraktionsprofile und den Beförderungsantrag.

**Warum:** Die Beschränkung auf sechs Träger ist für **Bilder** begründet und dokumentiert (interner
Auslieferungs-Endpunkt). Für **Anker** gibt es keinen Grund — sie ist nur ein Nebeneffekt davon, dass der
Anker-Durchlauf im Bild-Interceptor mitläuft. Das inzwischen gelöschte Designdokument `Richtexteditor.md`
nannte für die Gliederung ausdrücklich keine Trägerbeschränkung.

**Richtung:** Zusammen mit Befund 4 lösen. Solange das offen ist, den Knopf nur dort einblenden, wo Anker
tatsächlich vergeben werden.

---

### 6. Fünf Editorstellen mit `DraftKey` rufen nie `MarkSavedAsync`
`NOOSE-Website/Components/Pages/Board/Shared/AnnouncementForm.razor`,
`Handbook/Shared/HandbookArticleDialog.razor:34` und `:42`,
`Handbook/Shared/GlossaryTermDialog.razor:20`,
`Personnel/Shared/PromotionDialog.razor:27`,
`Admin/Shared/PublicFaqPanel.razor:195`

Nachgezählt: **20 Dateien setzen einen `DraftKey`, 15 rufen `MarkSavedAsync`, 5 nicht.** Nur dieser Aufruf
löscht die IndexedDB-Zeile und zieht `zustand.basis` auf den gespeicherten Stand nach.

**Ablauf:** Agent öffnet `/brett/neu`, schreibt eine lange Ankündigung, klickt „Veröffentlichen". Der
Entwurf unter `<AgentId>:ankuendigung:neu` bleibt liegen. Beim nächsten „Neue Ankündigung" ist der Editor
leer, der Entwurf ungleich leer → die Karte „Nicht gespeicherter Entwurf vom … gefunden" bietet den Text
der **bereits veröffentlichten** Ankündigung an. Gleiches Muster bei „Neuer Artikel", „Neuer Begriff" und
beim zweiten Beförderungsantrag für denselben Agenten.

**Warum:** Der Agent hält einen erledigten Text für ungespeichert — oder veröffentlicht ihn ein zweites
Mal. Zusätzlich bleibt der Klartext 30 Tage liegen, obwohl der Datensatz längst in der DB steht.
`PublicFaqPanel` fällt dabei als einziges seiner vier Schwesterpanels heraus.

**Richtung:** `AnnouncementForm` eine `MarkSavedAsync()` durchreichen lassen und in
`AnnouncementEditor.SaveAsync` vor dem `NavigateTo` rufen. In `PublicFaqPanel.EntrySaveAsync` analog zu
`PressPanel:244` ergänzen. Für die drei Dialoge je ein `@ref` halten und im Save-Pfad vor
`MudDialog.Close` rufen — beim Handbuch-Dialog für **beide** Editoren.

---

### 7. `richtext.js` wurde um ~490 Zeilen erweitert, der `?v=`-Cache-Buster steht weiter auf 15
`NOOSE-Website/Components/Common/Shared/RichTextEditor.razor:171`

`?v=15` kam mit `ff87213`. `7e2a19e` hat `richtext.js` um 492 Zeilen erweitert, ohne die Zahl zu erhöhen.
Dynamische ES-Importe umgehen Blazors Asset-Fingerprinting — `?v=` ist der einzige Cache-Schlüssel.

**Verschärfend:** Der Aufruf übergibt jetzt **zehn** Argumente; das zehnte (`HtmlCleanup.Profile`) ist neu.
Eine gecachte Datei mit der alten neunstelligen Signatur verwirft es stillschweigend.

**Ablauf:** Jeder Agent, der die Seite seit dem letzten Deploy einmal geöffnet hat, lädt nach dem nächsten
Deploy weiterhin die alte Datei: keine Kästen-, Trenner- und Inhaltsverzeichnis-Knöpfe, kein Strg+S, keine
Auswahl-Blase — und **keine Einfüge-Säuberung**, weil der 10. Parameter nie ankommt. Keine Fehlermeldung.

**Richtung:** Auf `?v=16` erhöhen. `richtext.js` hat nur diese eine Importstelle.

---

### 8. Handbuch-Slug darf 120 Zeichen, die Suchindex-Spalte fasst 64
`NOOSE-Website/Services/SearchIndexProjection.cs:68`

Der Handbuch-Arm schreibt den **Slug** als `EntityId` in `SearchPhoneticKey` und `SearchStemToken`. Beide
Spalten sind `HasMaxLength(64)` (`AppDbContext.cs:685` und `:695`), der Slug darf 120
(`AppDbContext.cs:1831`), und `HandbookArticleDialog.razor:22` gibt dem Feld ausdrücklich `MaxLength="120"`.
Bisher war jede `EntityId` eine GUID (36 Zeichen) — der Handbuch-Artikel ist der erste Typ mit freiem
Schlüssel.

**Ablauf:** HRB legt einen Artikel mit einer Adresse über 64 Zeichen an, etwa
`wie-du-eine-fahndung-ausschreibst-und-sie-danach-wieder-zuruecknimmst` (69). Der
`SearchIndexInterceptor` hängt die Indexzeilen in dasselbe `SaveChanges`; MySQL im Strict-Mode wirft
„Data too long" → **der Artikel wird gar nicht angelegt**, der Redakteur sieht eine rohe Datenbankmeldung.

**Nachgemessen:** Der längste **ausgelieferte** Slug ist 28 Zeichen — die Falle ist heute nicht scharf, sie
steht nur offen. Zusätzlich reißt derselbe Überlauf den `SearchIndexBackfillWorker` mit: er leert beide
Indextabellen und indiziert dann Typ für Typ; bricht er beim Handbuch ab, bleibt die Fertig-Marke
ungeschrieben und der ganze Lauf wiederholt sich bei **jedem** Start. (Entgegen der ursprünglichen Meldung
sind die zwölf vorher indizierten Typen nicht betroffen — jeder Typ committet einzeln.)

**Richtung:** Eine der beiden Zahlen muss die andere kennen. Entweder `EntityId` per Migration auf 120
heben, oder Slug, Spalte und Dialog gemeinsam auf 64 begrenzen. Dazu ein Kommentar an der
`HandbookArticle`-Zeile in `SearchIndexProjection`, der die Kopplung benennt.

---

### 9. Handbuch-Artikel und Glossar-Begriffe landen in den Kompromittierungs-Pickern der Entführung
`NOOSE-Website/Components/Pages/Abductions/Shared/AbductionCompromiseField.razor:52`,
`CompromiseManagerPanel.razor:122`

`QuickSearchAsync` liefert seit den zwei neuen `Quick`-Kategorien auch `HandbookArticle` (TargetId =
**Slug**) und `GlossaryTerm`. `MentionService.CandidatesAsync` wurde dafür in `b8a7a74` mit
`LinkService.KnownTypes` gefiltert — die beiden anderen Verbraucher derselben Methode nicht.

**Ablauf:** Agent öffnet eine Entführung, tippt im Feld „Kompromittierte Akte suchen & hinzufügen"
„fahndung" — die Liste bietet den Handbuch-Artikel *Fahndung ausschreiben* und den Glossar-Begriff an. Er
wählt einen, klickt Hinzufügen, lädt neu.

**Warum:** `AddCompromiseAsync` prüft `targetType` nicht und schreibt die Zeile. Beim Laden kennt
`RecordsReference.ResolveAsync` keinen der beiden Typen → die Zeile steht ab sofort **dauerhaft** als
„(gelöschte Akte)" ohne Link in der Liste der vom Abfluss betroffenen Akten. Bei einem längeren Slug kommt
der Überlauf aus Befund 8 dazu (`AbductionCompromise.TargetId` ist ebenfalls `varchar(64)`).

**Richtung:** In beiden Suchmethoden dasselbe Prädikat setzen wie in `MentionService.CandidatesAsync`, und
`AddCompromiseAsync` den Typ serverseitig prüfen lassen — sonst bleibt der SignalR-Pfad offen.

---

### 10. Mehrbild-Absatz verliert beim Speichern alle Bilder außer dem ersten
`NOOSE-Website/Services/RichTextFigure.cs:65`

`ToStored` sucht mit `line.QuerySelector("img")` nur das **erste** Bild einer Zeile und ersetzt anschließend
mit `line.Replace(figure)` den **ganzen** Absatz. Alle weiteren `img` verschwinden mit ihm. Die Vorbedingung
`!string.IsNullOrWhiteSpace(line.TextContent)` schützt nicht — ein `<img>` steuert keinen Text bei.

**Ablauf:** Agent fügt per Strg+V einen Screenshot ein, fügt ohne Enter sofort einen zweiten ein → Quill
erzeugt `<p><img A><img B></p>`. Er beschriftet Bild A im Bild-Dialog und speichert. Gespeichert wird
`<figure><img A><figcaption>…</figcaption></figure>` — Bild B ist weg, und weil `ToStored` **vor**
`ReplaceImagesAsync` läuft, wurde es nie als Datei abgelegt.

**Warum:** Stiller, nicht wiederherstellbarer Inhaltsverlust beim Speichern. Auf demselben Weg geht auch
ein umschließendes `<a>` verloren. Die Rundlauf-Tests decken nur den Ein-Bild-Fall ab.

**Richtung:** Nur falten, wenn der Absatz genau ein Kind-Element trägt — sonst unverändert lassen;
alternativ alle Kinder in die `figure` übernehmen.

---

### 11. Ein ausgeblendetes Handbuch-Objekt ist über die Oberfläche unerreichbar
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:190`,
`NOOSE-Website/Services/Handbook/HandbookService.cs:411`

Alle drei Editor-Dialoge tragen einen „Sichtbar"-Schalter. Die Listen, aus denen die Bearbeiten-Knöpfe
gespeist werden, sind aber die reinen **Lese**-Listen: `GetChaptersAsync` und `GetGlossaryAsync` filtern
hart auf `IsVisible`. Ist ein Objekt einmal unsichtbar, zeigt es kein Lesepfad mehr — nicht das Rail, nicht
die seiteneigene Suche, nicht `/handbuch/{slug}`, nicht `/papierkorb` (dort wird auf `IsDeleted` gefiltert,
und für Glossarbegriffe gibt es überhaupt keine Papierkorb-Quelle).

**Beim Glossar zusätzlich eine Sackgasse:** `GlossaryTerm` ist bewusst kein `ISoftDelete`, der eindeutige
Index auf `Begriff` bleibt aber belegt und `CreateTermAsync` prüft ohne Sichtbarkeitsfilter — der Begriff
lässt sich weder wiederherstellen noch neu anlegen („Den Begriff … gibt es bereits").

**Warum:** Der Schalter ist als Umschalter gebaut, wirkt aber wie eine unwiderrufliche Löschung ohne
Papierkorb. Ein versteckter Begriff verschwindet zusätzlich aus dem `GlossaryMatcher`, also aus den
Erklär-Blasen auf **jeder** Seite. Zurück geht es nur per Hand in der Datenbank.

**Richtung:** Die Bearbeiten-Einstiege aus den Voll-Listen speisen (`_allChapters`/`_allArticles` liegen
bereits geladen vor, Unsichtbare gekennzeichnet anzeigen) und ein `GetAllTermsAsync` analog zu
`GetAllArticlesAsync` ergänzen. Alternativ den Schalter aus den Dialogen nehmen und Ausblenden über den
vorhandenen Papierkorb-Weg abbilden.

---

### 12. Build-Stempel wandert bei jedem Neustart eine Fassung weiter in die Vergangenheit
`NOOSE-Website/Infrastructure/Changelog/ChangelogSeeder.cs:231`

`StampBuildNumberAsync` sucht „die neueste Fassung **ohne** Stempel" und kennt keine Bedingung, die den
Schreibvorgang an einen *neuen* Build bindet. Der Seeder läuft bei **jedem** App-Start
(`Program.cs:550`), und `SeedReleasesAsync` legt beim Erstlauf alle 15 Fassungen gemeinsam ungestempelt an.

**Ablauf:** Frisches Deploy, Build 1.0.500: Start 1 stempelt 2.1.00 — richtig. `systemctl restart noose`
(nach einer TZ-Änderung, nach einem Absturz): Start 2 findet als neueste ungestempelte Fassung 2.0.00 und
schreibt dieselbe Build-Nummer hinein. Nach 15 Neustarts trägt jede historische Fassung bis hinunter zu
0.1.00 eine Build-Nummer aus einem völlig anderen Deploy.

**Warum:** Die Build-Nummer ist auf `/neuerungen` die einzige Brücke zwischen einer Fassung und dem
ausgelieferten Stand — genau das, wonach man bei einer Fehlersuche greift. Der Wert ist persistent und über
die Oberfläche nicht korrigierbar (der Fassungs-Dialog bietet ihn nicht an, und eine Fassung lässt sich
nicht löschen). Der Klassenkommentar von `ChangelogRelease` schließt genau das aus: „A retroactive release
simply carries no build number rather than an invented one." Der vorhandene Test seedet mit **zwei
verschiedenen** Build-Nummern und trifft den Fall nie.

**Richtung:** Die insgesamt neueste Fassung holen und nur stempeln, wenn **sie** ungestempelt ist. Ältere
bleiben dann bewusst ohne Nummer — die Anzeige kommt damit schon zurecht. Regressionstest: zweiter
`SeedAsync` mit derselben Build-Nummer, danach ist genau eine Fassung gestempelt.

---

### 13. Glossar-Synonym „Tag" feuert auf das deutsche Wort für den Kalendertag
`NOOSE-Website/Infrastructure/Handbook/Content/GlossaryContent.cs:166`

Der Begriff „Stichwort" trägt `Synonyms: "Tag"`. `GlossaryMatcher.Build` legt jedes Synonym als
eigenständige Phrase ab; der Vergleich ist case-insensitiv und Satzzeichen zählen als Wortgrenze.

**Ablauf:** „Die Festnahme erfolgte am selben Tag." → die Blase sitzt auf „Tag" und erklärt beim Hover
„Ein freies Schlagwort an einer Akte, um quer durch den Bestand zu filtern." Betroffen ist jede
Singularform ohne angehängten Buchstaben: „pro Tag", „am Tag der Tat", „guten Tag".

**Warum:** Eine **falsche** Erklärung, keine fehlende — die Fehlerklasse, die `CLAUDE.md` im
Handbuch-Abschnitt ausdrücklich als die schlimmere benennt. Sie erscheint in praktisch jedem längeren
Bericht. Die Datei verletzt damit ihren eigenen Klassenkommentar: „Terms are deliberately specific rather
than short … a one-word entry would fire on prose that never meant the term."

**Richtung:** Synonym entfernen. Weil der Seeder Bestände nicht überschreibt, zusätzlich
`HandbookContent.Revision` hochzählen, damit unbearbeitete Zeilen beim nächsten Start neu geschrieben
werden.

---

### 14. `scripts/deploy.ps1` findet das Projekt nach dem Umzug nicht mehr
`scripts/deploy.ps1:76`

```powershell
$project = Join-Path $PSScriptRoot "NOOSE-Website\NOOSE-Website.csproj"
```

Nach dem Umzug nach `scripts/` ist `$PSScriptRoot` das `scripts`-Verzeichnis; gesucht wird also
`scripts/NOOSE-Website/NOOSE-Website.csproj`. Das Skript bricht sofort ab, mit einer Meldung, die in die
falsche Richtung zeigt: „Liegt deploy.ps1 wirklich im Repo-Root?"

**Warum:** Produktion ist damit nicht deploybar. Die Zeilen 77/78 (`publish`, Tarball) sollen dagegen
bewusst unter `scripts/` liegen — es ist genau diese eine Zeile.

**Richtung:** `Join-Path $PSScriptRoot ".." "NOOSE-Website\NOOSE-Website.csproj"` (bzw. `Split-Path
$PSScriptRoot`). Danach einmal `-SkipPublish` gegen die Demo-Instanz testen.

---

## P2 — Mittel

### 15. Changelog-Redaktion zeigt Nur-Lese-Aufsicht und Demo-Besucher Knöpfe, die im Dienst scheitern
`NOOSE-Website/Components/Pages/Admin/Shared/ChangelogPanel.razor:19`

Das Panel rendert „Neue Fassung", „Neuer Eintrag" sowie Bearbeiten und Löschen je Zeile **ohne jedes**
`AuthorizeView` und ohne `_mayWrite`. Einzige Schranke ist `Settings.razor` mit `Policies.LeadershipPage`,
und die lässt laut `CLAUDE.md` bewusst auch die Nur-Lese-Aufsicht herein; der Demo-Besucher trägt
Director + HRB und kommt ebenfalls durch. Der Dienst prüft dagegen `MayWrite() && IsLeadership()`.

**Ablauf:** Aufsicht oder Demo-Besucher öffnet `/einstellungen?tab=neuerungen`, klickt „Neue Fassung",
füllt Version, Datum und Überschrift aus, bestätigt — und bekommt die rote Meldung „Die Neuerungen pflegt
die Führung." Die eingegebene Arbeit ist weg.

**Warum:** Genau das Muster, das `CLAUDE.md` wörtlich als Grund für die Schreib-Policy nennt. Handbuch und
Changelog kamen im selben Commit und teilen dasselbe Redaktionsmuster — nur das Handbuch ist gegated.

**Richtung:** Eine Policy anlegen, die `Permission.RequireChangelogWrite` exakt spiegelt
(`MayWrite() && IsLeadership()`), und die fünf Steuerelemente damit wickeln. Zusätzlich ein privates
`_mayWrite` am Anfang jeder Save-Methode prüfen, damit der SignalR-Pfad zu ist.

---

### 16. `NameChangeApproveAsync` ohne Schreib-Guard hält das prozessweite Dienstnummer-Schloss
`NOOSE-Website/Services/AgentManagementService.cs:276`

Die Methode trägt **keinen** `Permission.Require*`-Guard, nimmt aber als erste Anweisung das statische
`BadgeNumberGate` und läuft dann durch `GetOrThrow`, `ValidateBadgeNumberAsync` (liest die Dienstnummern
**aller** Benutzer), die Mutation und einen AuditLog-Insert, bevor erst `Save` am
`ReadOnlyBarrierInterceptor` scheitert. Die Schwestermethoden `MasterDataChangeAsync` und
`NameChangeRequestAsync` rufen `RequireWriteAccess` korrekt als erste Anweisung.

**Ablauf:** Demo-Modus aktiv. Ein anonymer Besucher bekommt das Demo-Principal (Director, Active), öffnet
`/admin/freigaben` und sieht unter „Namensänderungen" den Knopf „Genehmigen". Jeder Klick nimmt das
prozessweite Schloss und macht drei DB-Rundreisen inklusive Volltabellen-Lesen, bevor abgelehnt wird.
Solange er klickt, blockiert er jede echte Stammdaten-Änderung im ganzen Prozess.

**Warum:** `CLAUDE.md` verlangt den Schreib-Guard als erste Anweisung, ausdrücklich **vor** dem Rang-Guard,
damit genau das nicht passiert. Der fehlende Guard ist Altbestand — neu ist, dass dieser Bereich den
ungesicherten Pfad hinter ein **globales Schloss** gelegt hat.

**Richtung:** Schreib-Guard als erste Anweisung in `NameChangeApproveAsync` und `NameChangeRejectAsync`,
**vor** `BadgeNumberGate.WaitAsync()`. Dem `WaitAsync` zusätzlich einen `CancellationToken` geben.

---

### 17. Dropdown bietet die eigene Alt-Dienstnummer an, die der Validator immer ablehnt
`NOOSE-Website/Services/AgentManagementService.cs:86`

`GetAvailableBadgeNumbersAsync` hängt den eigenen Wert wieder an die Optionsliste, sobald er nicht mehr in
`options` steht — genau dann, wenn ein **anderer** Agent ihn hält. `ValidateBadgeNumberAsync` prüft die
Eindeutigkeit aber gegen alle Fremdzeilen und kennt dafür keine Ausnahme: `isLegacyValue` entschärft nur die
**Format**prüfung.

**Ablauf:** Zwei Agenten tragen aus der Freitext-Zeit beide `V`. Führung öffnet A zum Bearbeiten, will nur
den Klarnamen korrigieren — `V` ist vorausgewählt. Speichern → „Diese Dienstnummer ist bereits vergeben."
A's Stammdaten lassen sich fortan überhaupt nicht mehr speichern, ohne die Dienstnummer zu wechseln.
Derselbe Effekt trifft A auf `/profil`: schon ein reiner Codename-Antrag scheitert.

**Richtung:** Die Eindeutigkeitsprüfung überspringen, wenn der Kandidat dem eigenen aktuellen Wert
entspricht — das Prädikat steht als `isLegacyValue` bereits da. Alternativ den eigenen Wert nur anhängen,
wenn ihn kein anderer hält.

---

### 18. `/handbuch` lädt für jeden Leser alle 81 Artikelrümpfe
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:197`

`LoadAsync` holt nach den projizierten Lesedaten zusätzlich `GetAllChaptersAsync()` und dann **je Kapitel**
`GetAllArticlesAsync(c.Id)`. Beide materialisieren volle Entitäten inklusive `ContentHtml` und
`RollenspielHtml` — `longtext`. Das sind 11 Abfragen pro Aufruf, davon sieben mit dem kompletten
Handbuchtext, für **jeden** internen Agenten unabhängig von Redaktionsrechten, und wegen Prerendering
zweimal je Seitenaufruf.

**Warum:** `GetChaptersAsync` projiziert genau deswegen auf die Kartenfelder — der Kommentar dort nennt den
Grund ausdrücklich. Über den zweiten Pfad zahlt die Indexseite es weiter. `_allChapters`/`_allArticles`
werden ausschließlich in Redaktions-Zweigen gebraucht.

**Richtung:** Nur laden, wenn `MayWrite() && IsHrbOrLeadership()` — besser erst beim Öffnen eines Dialogs.
Die Kapitelschleife durch eine `WHERE ChapterId IN (…)`-Abfrage ersetzen, wie `GetChaptersAsync` es macht.

---

### 19. Glossar-Wortgrenze wird nur innerhalb eines Textknotens geprüft
`NOOSE-Website/Services/Handbook/GlossaryMatcher.cs:92`

`LongestAt` prüft links mit `text[index - 1]` und rechts mit `text[end]` — beides nur innerhalb des einen
Textknotens. Liegt der Treffer am Knotenanfang oder -ende, entfällt die Prüfung ersatzlos, obwohl im
gerenderten Text unmittelbar davor oder danach Buchstaben aus einem Nachbarknoten stehen.

**Ablauf:** Agent schreibt „Die Fahndungsliste wurde geprüft." und formatiert nur „Fahndung" fett →
`<p>Die <strong>Fahndung</strong>sliste …</p>`. Der Leser sieht in „Fahndungsliste" den Teil „Fahndung"
unterstrichen und bekommt die Erklärung für die öffentliche Ausschreibung angeboten.

**Warum:** Wieder eine **falsche** Erklärung statt einer fehlenden. Keiner der 34 `GlossaryHtmlTests` deckt
das ab — alle Grenz-Tests laufen über einen einzigen Textknoten.

**Richtung:** Beim Einsammeln je Textknoten das letzte Zeichen des vorherigen und das erste des folgenden
Geschwisters mitgeben und `LongestAt` an den Rändern dagegen prüfen lassen.

---

### 20. Das Inhaltsverzeichnis verliert seine Klassen, sobald Quill es einfügt
`NOOSE-Website/wwwroot/js/richtext.js:1684`

`setInhaltsverzeichnis` schiebt das Markup über `editor.clipboard.convert(html)` in den Editor. Anders als
bei Kasten, Bildtext und Trenner ist für `noose-inhaltsverzeichnis`/`noose-toc-N` **kein** Blot oder
Attributor registriert — Quill behält an `<ul>`/`<li>` nur registrierte Formate, die Klassen fallen sofort
weg und stehen nie im gespeicherten HTML.

**Warum:** Die Ebeneninformation, die `BuildToc` aus `TocEntry.Level` extra berechnet, wird unmittelbar
wieder verworfen; h1/h2/h3 sind im Verzeichnis nicht mehr unterscheidbar. Die dafür geschriebenen
CSS-Blöcke (`app.css:259` und `:749`) sind toter Code.

**Richtung:** Die Einrückung über ein Format ausdrücken, das Quill kennt (`ql-indent-1/2`), oder ein
eigenes Blot registrieren — so wie es für Kasten und Bildtext schon gemacht ist.

---

### 21. Markdown-Kürzel und Slash-Menü feuern auch in der ersten Zeile eines Codeblocks
`NOOSE-Website/wwwroot/js/richtext.js:1493` und `:903`

Beide Handler prüfen Auswahl, Länge und Zeilenanfang, aber nie das aktuelle **Blockformat**.

**Ablauf:** Agent tippt in einem Codeblock als erste Zeile `# install nginx`. Sobald das Leerzeichen steht,
verschwindet das `# ` und die Zeile wird als Überschrift 1 aus dem Codeblock herausgelöst. Genauso bei `- `
und `> `. Beim Slash-Menü zusätzlich: `/` öffnet die Einblendung, das folgende Enter wird abgefangen — statt
einer neuen Codezeile kommt ein Blockformat.

**Warum:** Ein Codeblock ist genau die Stelle, an der `#`, `-`, `>` und `/` wörtlicher Inhalt sind.

**Richtung:** In beiden Handlern vor der Musterprüfung aussteigen, wenn `code-block` gesetzt ist.

---

### 22. Slash-Befehl „Text" löst Zitat und Codeblock nicht auf
`NOOSE-Website/wwwroot/js/richtext.js:811`

Für den Eintrag „Text" fällt `anwenden` auf `formatLine(start, 1, 'header', false)` zurück. Das räumt nur
ein Header-Blot ab; bei `blockquote` oder `code-block` passiert nichts. Die Zeile darüber räumt gezielt nur
das Listenformat ab — für die beiden anderen gibt es kein Gegenstück.

**Ablauf:** Zeile auf „Zitat" setzen, dann `/` tippen und „Text — Normaler Absatz" wählen. Das `/` wird
gelöscht, die Zeile bleibt ein Zitat. Über die Toolbar funktioniert es dagegen.

**Richtung:** Beim Zurücksetzen alle Blockformate abräumen, nicht nur `header`.

---

### 23. Entwürfe bleiben nach dem Abmelden 30 Tage unverschlüsselt im Browser
`NOOSE-Website/wwwroot/js/richtext.js:1209`

Alle 800 ms wird der volle Editorinhalt im Klartext in die IndexedDB `noose-rte` geschrieben — bei
`DocumentEditor` also auch der Text eines Dokuments mit VS-Stufe, bei `NotesPanel`/`PromotionDialog`
Personalakten-Text. Gelöscht wird nur durch `MarkSavedAsync`, „Verwerfen" oder den 30-Tage-Sweep, und der
Sweep läuft **nur**, wenn wieder ein Editor geöffnet wird. `indexedDB` kommt im ganzen Projekt nur in
`richtext.js` vor; der Abmelde-Pfad löscht nichts.

**Ablauf:** Agent A öffnet auf einer gemeinsam genutzten RP-Station ein VS-Dokument, tippt, schließt den Tab
ohne zu speichern und meldet sich ab. Agent B öffnet die Entwicklerwerkzeuge → Anwendung → IndexedDB und
liest den vollständigen Text — obwohl er die Akte in der Anwendung nie zu sehen bekäme.

**Warum:** Die serverseitige VS-Durchsetzung endet am Browser. Über die Anwendung ist nichts zu holen (der
Schlüssel trägt die Agenten-Id), über die Entwicklerwerkzeuge alles.

**Richtung:** Ein `entwuerfeLoeschen()` exportieren (`indexedDB.deleteDatabase`) und im Abmelde-Pfad
auslösen. Aufbewahrung deutlich senken und den Sweep beim App-Start laufen lassen, nicht erst beim nächsten
Editor.

---

### 24. Abgelaufene Wochen werden mit dem **heutigen** Aufschlag geschlossen
`NOOSE-Website/Services/Llm/LlmQuotaService.cs:348`

`BuildStatusAsync` reicht den bereits aufgeschlagenen `baseWeekly` an `CloseElapsedAsync` weiter.
`LlmQuotaLedger.Backfill` benutzt ihn für **jede** noch offene Vorwoche — sowohl für `CarryOut` als auch
als eingefrorenes `PeriodDraft.BaseWeekly`. Das Schließen läuft träge beim ersten Lesen der neuen Woche, der
dabei geltende Aufschlag ist also der von *heute*.

**Ablauf:** Woche 1 läuft ganz auf OpenRouter, Aufschlag 0. Agent mit 50.000 Basis verbraucht 10.000,
korrekter Übertrag 10.000. Montag früh schaltet der KI-Eigner auf DeepSeek mit +400 %, bevor jemand eine
Kontingentseite geöffnet hat. Der erste Statusaufruf schließt Woche 1 jetzt mit 250.000 Basis →
Übertrag **60.000** statt 10.000.

**Warum:** Der Commit sagt zu, der Aufschlag gelte „nur, solange sein Anbieter aktiv ist". Hier wirkt er
rückwirkend auf eine Woche, die vollständig auf dem anderen Anbieter lief. Zusätzlich wird eine Audit-Zeile
dauerhaft mit einer Zahl eingefroren, die in dieser Woche nie galt.

**Richtung:** Die Rückabwicklung mit dem **unaufgeschlagenen** Wert fahren und den Aufschlag erst auf die
laufende Woche legen — oder den zur Wochenzeit gültigen Aufschlag auf `LlmQuotaPeriod` mitschreiben und beim
Schließen den gespeicherten verwenden.

---

### 25. `schlage_nach` verliert bei großem `max` jeden Artikel-Quellenchip
`NOOSE-Website/Services/Llm/Tools/HandbookLookupTool.cs:124`

`refs` wird erst mit bis zu `max` (bis 40) Glossar-Begriffen gefüllt und **danach** mit bis zu fünf
Artikeln. Am Ende schneidet `refs.Take(MaxRefs)` mit `MaxRefs = 8` vom Ende her ab.

**Ablauf:** „Erklär mir alles rund um die Fahndung", Modell ruft mit `max: 10`. Bei 143 Begriffen treffen
leicht acht — danach behält `Take(8)` **nur** Glossar-Chips. Die Artikel, aus deren Text die Antwort
besteht, bekommen keinen einzigen.

**Warum:** Die Glossar-zuerst-Reihenfolge ist für den **Antworttext** begründet (der Clip schneidet hinten
ab), wirkt für die Chip-Liste aber genau andersherum. Stiller Teilausfall — die Antwort sieht vollständig
aus.

**Richtung:** Beide Sorten getrennt deckeln: Artikel-Chips garantieren, den Rest bis `MaxRefs` mit Begriffen
auffüllen.

---

### 26. `ChangelogPanel` lädt die Einträge mit einer Abfrage je Fassung
`NOOSE-Website/Components/Pages/Admin/Shared/ChangelogPanel.razor:139`

`LoadAsync` ruft in der Schleife je Fassung `GetEntriesAsync(r.Id)` — jeweils mit eigenem DbContext. Bei 15
Fassungen sind das 16 Rundreisen pro Ladevorgang, und nach jedem Speichern lädt `GuardAsync` erneut. Fünf
nacheinander eingetragene Zeilen kosten ~96 Abfragen.

**Warum:** Derselbe Dienst löst das eine Ebene höher bereits richtig: `GetTimelineAsync` holt alle Einträge
mit einem flachen `WHERE IN` und kommentiert ausdrücklich, warum. Der Aufwand steigt mit jeder Fassung
dauerhaft mit.

**Richtung:** Eine `GetEntriesAsync`-Überladung für mehrere `ReleaseId`s ergänzen.

---

### 27. Onboarding-Schritt „Menü angepasst" verlinkt auf die Seite, auf der man schon steht
`NOOSE-Website/Services/Onboarding.cs:59`

Der Schritt gibt `Href="/dashboard"` an. Die Checkliste wird ausschließlich in `Home.razor` gerendert, und
das ist `@page "/dashboard"`. Der Menü-Anpasser ist überhaupt nicht routbar — `NavCustomizeDialog` wird nur
aus dem Drawer als Dialog geöffnet.

**Ablauf:** Neuer Agent klickt in „Erste Schritte" den Punkt „Menü angepasst" an. Blazor navigiert auf
`/dashboard`, nichts passiert, nichts öffnet sich. Es gibt keinen Hinweis, dass der Anpasser hinter dem
Zahnrad im Drawer liegt.

**Warum:** Jeder andere Schritt bringt den Agenten dorthin, wo er ihn erledigen kann.
`Every_step_names_a_route_and_an_icon` prüft nur, dass der Href mit „/" beginnt — der Schritt sieht
getestet aus und ist es nicht.

**Richtung:** Den Schritt ohne Link rendern oder ihn den Dialog per Callback öffnen lassen.

---

### 28. Ein Glossarbegriff verliert still seinen Artikel-Link
`NOOSE-Website/Models/Handbook/HandbookModels.cs:44`

`GlossaryTermView` trägt nur `ArticleSlug`, nicht `ArticleId`. `GetGlossaryAsync` füllt den Slug
ausschließlich aus **sichtbaren** Artikeln und setzt ihn sonst auf `null`. Das Preset rechnet die Id über
`ArticleIdOf(term.ArticleSlug)` zurück → `null`, der Dialog gibt das unverändert weiter, und
`RefreshTermAsync` schreibt `ArticleId = null`.

**Ablauf:** Begriff „EHK-Score" zeigt auf den Artikel „Bedrohungs-Score". Jemand blendet den Artikel aus.
Später korrigiert ein anderer nur einen Tippfehler in der Kurzerklärung des Begriffs und speichert. Wird der
Artikel danach wieder sichtbar, fehlt der „mehr dazu"-Link — ohne Meldung, und ohne dass irgendwo steht,
welcher Artikel es war.

**Warum:** Genau die Falle, die `CLAUDE.md` für `FollowupDialog`/`ObservationDialog` beschreibt: „muss auf
`FindAsync` zurückfallen … sonst löscht der Speichern-Pfad die Zuordnung still." Im Dialog steht
„— keiner —", was wie der gespeicherte Zustand aussieht.

**Richtung:** `GlossaryTermView` um `ArticleId` erweitern (der Wert liegt ohnehin vor) und das Preset direkt
daraus bauen.

---

## P3 — Niedrig

### 29. `GetAsync` schreibt den Cache außerhalb des gestreiften Schlosses
`NOOSE-Website/Services/NavPreferencesService.cs:44` — `MutateAsync` hält den Cache-Write bewusst **innerhalb**
des Schlosses (Kommentar Zeile 175), `GetAsync` tut genau das nicht. Ein Lesevorgang, der vor einer Mutation
startet und danach zurückkommt, überschreibt den frischen Eintrag mit dem alten Blob — ein gerade gesetzter
Onboarding-Stempel bleibt bis zu 30 s unsichtbar. Heilt sich selbst, braucht echte Überlappung.

### 30. Zwei der fünf Onboarding-Stempelstellen prüfen `IsInternalAgent` nicht
`NOOSE-Website/Components/Pages/Account/MyProfil.razor:254`, `Common/Shared/RecentsTracker.razor:65` — drei
andere Stellen prüfen es. Partner-Konten sammeln damit Marker, die sie nie zu sehen bekommen, und zahlen bei
jeder Akten-Navigation einen zusätzlichen Read-Modify-Write des ganzen Blobs.

### 31. Kapitel-Slug: Bereinigung kappt bei 120, die Spalte fasst 80
`NOOSE-Website/Services/Handbook/HandbookService.cs:530` — `Slug()` kappt für **beide** Typen bei 120,
`HandbuchKapitel.Slug` ist aber `HasMaxLength(80)`. Die Umlaut-Transliteration verlängert den Text
(`ä`→`ae`), sodass 80 eingegebene Zeichen 81+ ergeben können. Ergebnis ist eine rohe MySQL-Meldung im
Snackbar statt einer verständlichen Ablehnung.

### 32. Datum und Überschrift einer bestehenden Fassung erreichen die Produktion nie
`NOOSE-Website/Infrastructure/Changelog/ChangelogSeeder.cs:136` — `SeedReleasesAsync` ist rein einfügend.
Für Einträge gibt es den Revisions-Mechanismus, für den Fassungs**kopf** kein Gegenstück. Ein
Revisions-Bump wirkt also nur zur Hälfte, ohne Meldung.

### 33. `LinkPanel` schlägt bereits Verknüpftes weiter vor
`NOOSE-Website/Components/Common/Shared/LinkPanel.razor:232` — baut die Ausschlussliste nur aus sich selbst,
obwohl die vorhandenen Verknüpfungen in derselben Komponente geladen vorliegen. Die drei Schwester-Panels
derselben Umbau-Runde übergeben ihre Liste. Der ausgelieferte Changelog-Eintrag verspricht dagegen pauschal:
„schlägt schon Verknüpftes nicht mehr vor." Folge: rote Meldung „Diese Verknüpfung besteht bereits."

### 34. `Designation` ist ein toter Parameter des `LinkDialog`
`NOOSE-Website/Components/Common/Shared/LinkDialog.razor:360` — dokumentiert als „Drives the dialog title",
durchgereicht, im Komponentenrumpf aber nie gelesen. Den Titel setzt allein die lokale Variable in
`ShowAsync`. Der alte `OrgRelationDialog` las ihn noch.

### 35. Schema-Generator schreibt weiter in den Repo-Root
`docs/db-schema/generate_schema_html.py:18` — `OUTPUT = REPO_ROOT / "DatenbankStruktur.html"`, obwohl die
Datei nach `docs/` verzogen ist. Ein Regenerieren legt eine zweite, untrackte Kopie im Root an, während die
eingecheckte veraltet stehenbleibt.

### 36. `README.md` verweist auf fünf Docs unter dem falschen bzw. keinem Pfad
`README.md:521` — `Plan.md`, `PublicPlan.md`, `AlgoPlan.md` und `GoalOfTheSite.txt` wurden gelöscht,
`DEPLOYMENT.md` liegt unter `docs/`. Auch `README.md:191` nennt `PublicPlan.md` im Fließtext.
**Dasselbe in `CLAUDE.md`** unter „Weiterführende Docs" (alle vier gelöschten Dateien plus
`DEPLOYMENT.md`/`CODE_REVIEW_TODO.md` ohne `docs/`-Präfix). `AlgoPlan.md` wird zusätzlich aus
XML-Doc-Kommentaren zitiert (`Faction.cs:43`, `IThreatScoreService.cs:6`).

### 37. Beide Deploy-Anleitungen rufen die Skripte weiter aus dem Repo-Root auf
`docs/DEPLOYMENT.md:48`, `docs/DEPLOYMENT-DEMO.md:79` — beide wurden nur verschoben, nicht angepasst. Auch
`scripts/setup-demo.ps1:88` gibt dem Nutzer am Ende `.\deploy.ps1 …` aus. Commit `01d964c` hieß „fix script
paths in docs" und hat genau diese beiden Dokumente ausgelassen — damit widerspricht die ausführlichste
Anleitung der Kurzfassung überall sonst.

### 38. Dienstnummer-Eindeutigkeit nur im Anwendungscode
`NOOSE-Website/Data/AppDbContext.cs:284` — kein Unique-Index auf `Dienstnummer`, die Eindeutigkeit entsteht
allein aus dem Read-then-Write unter einem **prozesslokalen** Schloss. Das Projekt ist bewusst
Einzelinstanz, das Fenster ist also schmal — aber eine entstandene Dublette ist nicht selbstheilend: sie
sperrt beide Akten dauerhaft für jede Stammdaten-Änderung (siehe Befund 17).

### 39. `?begriff` bleibt in der Adresse stehen
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:216` — `OpenTermFromQuery` entfernt den Parameter
nicht, und `QueryState.WriteAsync` lässt nicht genannte Parameter unverändert. Ein Lesezeichen auf ein
Kapitel landet nach F5 wieder in der Trefferliste, die der Nutzer vorher weggeklickt hatte; das
`tab=`-Segment derselben Adresse wird dabei wirkungslos.

### 40. `RichHtml`-Memo kennt die Präferenz „Glossar-Blasen" nicht
`NOOSE-Website/Components/Common/Shared/RichHtml.razor:34` — der Kurzschluss schlüsselt auf `Html`, `Plain`
und `Glossary`, nicht auf den Präferenzwert, den `BubblesOnAsync` liest. Der Schalter im
`NavCustomizeDialog` wirkt deshalb erst nach einem Seitenwechsel; für den Agenten sieht es aus, als hätte er
nichts getan.

### 41. Hinweiskarte kann eine Fassung nennen, die `/neuerungen` gar nicht anzeigt
`NOOSE-Website/Services/Changelog/ChangelogService.cs:79` — `NewestVersion` kommt aus der nach Datum
neuesten frischen Fassung, ohne zu prüfen, ob sie sichtbare Einträge hat. `GetTimelineAsync` verwirft leere
Fassungen dagegen ausdrücklich. Die Zählung stimmt, nur die Beschriftung nicht.

### 42. Rechtliche Seiten sind öffentlich indexierbar, tragen aber keine Link-Vorschau
`NOOSE-Website/Components/Pages/Legal/Privacy.razor:8`, `Nutzungsbedingungen.razor:8` — `PublicRoutes`
erklärt beide zu öffentlichen Routen, `robots.txt` erlaubt sie, und jede öffentliche Seite verlinkt sie im
Fuß. `PublicPageScanTests` scannt aber nur `Pages/Public` und sieht sie nicht — die in diesem Bereich
ergänzte `CLAUDE.md`-Zeile behauptet trotzdem, der Test fordere die Vorschau für jede öffentliche Seite ein.

### 43. Der neue Einstellungs-Abschnitt `ki-anbieter` fehlt in zwei Spiegel-Listen
`NOOSE-Website/Navigation/MergedPageSections.cs:13`, `FeedbackPageTabs.cs:28` — beide Arrays spiegeln laut
ihren eigenen Kommentaren die `RecordSection`-Deklarationen. Nachgezogen wurde nur `neuerungen`. Folge: der
Feedback-Picker kennt den Abschnitt nicht, und `PublicSurfaceGuardTests` prüft Audit-Routen gegen eine
unvollständige Liste.

### 44. Handbuch und Neuerungen tragen keine eigene Policy
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:2` — die drei neuen Seiten tragen nur das globale
`ActiveAgent`, während 27 andere interne Seiten `Policies.InternalAgent` setzen. **Kein Leck** (siehe unten),
aber ein Musterbruch: der Schutz hängt allein am zentralen Layout-Gate. Der Kommentar in
`HandbookHelpButton.razor:11` behauptet eine Lücke, die es nicht gibt.

---

## Sieht aus wie ein Fehler, ist keiner

- **Partner können `/handbuch` und `/neuerungen` nicht öffnen.** Beide Seiten tragen kein
  `Policies.InternalAgent`, und `ActiveAgent` lässt Partner durch — der Zugriff scheitert trotzdem:
  `PartnerRoutes.AllowedPrefixes` ist eine **Positivliste** ohne beide Routen, und `MainLayout.razor:236`
  prüft `PartnerRoutes.IsAllowed` **vor** dem Rendern von `@Body`; `PrintLayout.razor:47` spiegelt das. Die
  Seite wird nie instanziiert. Bleibt als Befund 44 nur der Musterbruch.
- **Textbilder registrierter RichText-Träger lecken nicht.** Der neue Zweig in
  `TextImageService.MaySeeOwnerAsync` schickt sie durch `Visibility.IsRecordVisibleAsync`, dessen Schwanz
  `_ => true` lautet. `Document` und `Agent` werden **vorher** abgefangen, und alle sechs registrierten
  Träger haben im `Visibility`-Schalter einen eigenen, benannten Fall.
- **Die Interceptor-Reihenfolge stimmt.** `ReadOnlyBarrierInterceptor` läuft vor `RichTextHtmlInterceptor`;
  eine verweigerte Schreibberechtigung bricht also ab, bevor Dateien abgelegt werden.
- **Der Sanitizer wurde eng erweitert.** `data-checked`, `figure`, `figcaption`, `hr` — kein Wildcard, keine
  neue URL-Schema-Freigabe. Einzige Ausnahme ist das globale `id` (siehe Befund 3).
- **Die neuen Changelog-Zeilen sind formal korrekt.** `2.1.11` bis `2.1.14`, fortlaufend zweistellig,
  Alltagssprache ohne Technik. Inhaltlich versprechen `2.1.12` und `2.1.13` allerdings Funktionen, die laut
  Befund 4, 5 und 20 nicht tun, was dort steht.
- **Der Build ist grün.** 0 Fehler, 42 Warnungen (alle `MUD0002`, alle aus Altbestand). Ein zwischenzeitlich
  gebrochener Build (`LlmProvider.cs` ohne `using MudBlazor;`) wurde noch während des Reviews repariert.

---

## Grenzen dieser Prüfung

- **Keine Razor-Komponente ist getestet.** Das Projekt hat kein bUnit; alle Befunde in `.razor`-Dateien sind
  durch Lesen belegt, nicht durch einen Testlauf. Das betrifft unter anderem Befund 1, 6, 15 und 27.
- **Kein Lauf gegen eine echte MySQL.** Die Testsuite läuft auf In-Memory-SQLite, das Spaltenlängen
  ignoriert. Befund 8 und 31 (Längenüberläufe) sind aus dem Schema abgeleitet, nicht nachgestellt.
- **Migrationen wurden nicht angewendet.** `dotnet ef migrations has-pending-model-changes` wurde nicht
  ausgeführt (fremder Testlauf hielt die DLLs); Snapshot-Konsistenz ist ungeprüft.
- **Die Oberfläche wurde nicht bedient.** Kein Klicktest, keine Browsersitzung.
- **Bewegliches Ziel.** Während des Reviews sind zwei Feature-Commits und zwei Merges gelandet. Die Befunde
  sind gegen `7e2a19e` geprüft; alles danach ist nicht erfasst.
- **Testlauf war eingeschränkt.** Nur die betroffenen Klassen (1.065 Tests), nicht die volle Suite.
