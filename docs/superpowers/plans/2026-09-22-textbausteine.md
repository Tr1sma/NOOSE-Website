# Persönliche Textbausteine

Roadmap-Idee **#7** aus `IdeenBacklog.md:182` (Aufwand mittel, Jury-Schnitt 6.3). Nachgelesen am
22.09.2026, bevor eine Zeile entstand. Etappen #1–#6 sind gebaut und gepusht.

## Kontext

Vorlagen gibt es für Dokumente, Dienst-Aktivitäten, die Personalakte, die Kasse und für
Bürger-Nachrichten — also für alles, was die **Führung** pflegt. Ausgerechnet die Felder, in die ein
Agent täglich tippt, haben keine: der Vermerk an der Akte, der Taskforce-Chat, Grund und Ergebnis
eines Doks, die Beobachtung. Die immer gleichen Formulierungen („Observation ohne Feststellung",
„Kontakt bestätigt durch …") werden jedes Mal neu geschrieben oder aus einer alten Akte kopiert.

Diese Etappe gibt jedem Agenten eine **eigene** Sammlung: Baustein wählen, Text landet im Feld,
Platzhalter werden dabei ersetzt.

## Der Befund aus dem Code

- **`MentionInput.razor`** ist der eine Eingabe-Baustein, durch den diese Felder laufen — rund
  sechzig Einbaustellen. Er hat genau **zwei** belegte Tasten (`Escape`, `Strg+Enter`) und eine
  **volle** Adornment-Position (das `@`). Ein zweiter Knopf passt dort nicht hinein, eine zweite
  Taste sehr wohl.
- **Der `@`-Picker ist die fertige Blaupause.** `ActiveQuery = @"@([^\s@{}]*)$"` greift nur am
  **Textende**; `InsertAsync` ersetzt diesen Rest oder hängt an. Es gibt **keine Schreibmarken-Logik**
  in C# — die einzige Stelle, die eine Schreibmarke kennt, ist das Bild-Einfügen über `textbild.js`.
  Ein zweiter Auslöser erbt diese Mechanik unverändert.
- **`MentionPicker.razor`** ist bereits geteilt (`MentionInput` **und** `RichTextEditor`) und hält
  keinen eigenen Zustand: Kandidaten rein, `OnPick` raus. Im `MentionInput` läuft er bewusst **ohne**
  Tastatursteuerung (`ActiveIndex = -1`), weil `MudTextField` nur ein pauschales `preventDefault`
  anbietet und die Pfeiltasten sonst der Schreibmarke fehlten.
- **Die fünf Felder, um die es geht, benennen ihre Trägerakte schon** — über
  `ImageOwnerType`/`ImageOwnerId`, gesetzt in `CommentPanel`, `TaskforceChatPanel`, `DocDialog`,
  `ObservationDialog` und (seit Etappe 6) `QuickAddDialog`. Das ist genau das Paar, das
  `PlaceholderService` für `{{Name}}` und `{{Aktenzeichen}}` braucht. Es muss **kein neuer Parameter**
  durch die Aufrufstellen gereicht werden.
- **`PlaceholderService.ApplyAsync` kodiert seine Werte mit `WebUtility.HtmlEncode`.** Für HTML ist
  das richtig, für ein Klartextfeld falsch: aus „Müller" würde `M&#252;ller`. Genau diese Falle ist in
  Etappe 6 schon einmal zugeschlagen. Der Dienst prüft immerhin selbst die Sichtbarkeit der Akte
  (`Visibility.IsRecordVisibleAsync`), bevor er einen Namen einsetzt.
- **`SavedSearch` ist der exakte Präzedenzfall** für eine Akte, die einem einzelnen Agenten gehört:
  `IAuditable`, **kein** `ISoftDelete` („owned by an agent, hard-deletable"), Tabelle
  `GespeicherteSuchen`, ein Anbieter in `Services/Search/Providers/PersonalSearchProviders.cs`
  (`SearchGroup.Personal`, `PartnerAccess.Never`, Route `null`), ein `NeverPublic`-Eintrag, ein
  `NotAssistantReadable`-Eintrag („reine UI-Voreinstellung ohne Ermittlungsinhalt"), **kein**
  Papierkorb-Eintrag und **kein** Eintrag im `ReadOnlyBarrierInterceptor` — Partner und
  Nur-Lese-Aufsicht legen keine an.
- **Der Schrägstrich ist im Rich-Text-Editor bereits die Hausgeste.** `richtext.js:832` trägt ein
  Slash-Menü mit vierzehn Blockformaten, und der Eintrag `toc` ist der fertige Beweis, dass ein
  Menüeintrag **C# fragen und das Ergebnis an der Schreibmarke einsetzen** kann
  (`OnTocRequested` → `editor.updateContents`).
- **Die Datenbank läuft gerade nicht** (nichts hört auf 3306), und `AppDbContextDesignTimeFactory`
  ruft `ServerVersion.AutoDetect`, was eine **echte Verbindung** öffnet. Die Migration entsteht deshalb
  über den Offline-Weg: Fabrik vorübergehend auf `ServerVersion.Parse("8.0.0-mysql")`,
  Dummy-Verbindungszeichenfolge als Umgebungsvariable, danach zurückbauen. Höchste vorhandene
  Migration: `Phase82_Funkplan` ⇒ neu **`Phase83_Textbausteine`**.
- **`AgentDeleteCoverageTests` ist die Falle, die man nicht kommen sieht.** Der Test reflektiert über
  jeden Fremdschlüssel auf die Agenten-Tabelle mit `DeleteBehavior.Restrict` und verlangt für jeden
  eine Aufräumzeile in `AgentManagementService.DeleteAccountAsync`. `Cascade` ist davon befreit — und
  genau das nutzt `SavedSearch`. Die neue Tabelle hängt deshalb mit **`Cascade`** am Agenten: wird ein
  Konto gelöscht, gehen seine Bausteine mit, und niemand muss daran denken.
- **Der Präferenzen-Blob ist der falsche Ort.** `NavPreferences` liegt als JSON-Spalte am Konto, wird
  bei **jeder** Navigation vollständig neu geschrieben und beim Zeichnen des Menüs gelesen; zwei
  überlappende Änderungen verlieren eine der beiden (deshalb das gestreifte Schloss). Ein Baustein
  trägt bis zu ein paar tausend Zeichen — den in diesen Blob zu legen hieße, ihn bei jedem Seitenwechsel
  mitzuschleppen. Außerdem umgeht der Blob-Schreibweg den Audit-Pfad bewusst. Also eine Tabelle.

## Was gebaut wird

| Stelle | Was passiert |
|---|---|
| `/` am Wortanfang in einem Klartextfeld | öffnet die Baustein-Liste, gefiltert nach dem Getippten |
| Baustein gewählt | Text ersetzt das `/…`, Platzhalter sind ersetzt |
| `/profil` → neuer Abschnitt | Bausteine anlegen, umbenennen, ändern, löschen |
| Slash-Menü im Rich-Text-Editor | dieselben Bausteine als eigene Gruppe |

**Der Auslöser ist `/` am Wortanfang**, nicht irgendwo: `(?:^|\s)/([^\s/]*)$`. Ohne diese Schranke
würde „Vinewood/Ost" beim Tippen von „Ost" die Liste aufklappen. Am Textende verankert wie beim `@`,
damit dieselbe (getestete) Einfüge-Mechanik trägt.

## Was bewusst **nicht** gebaut wird

- **Keine Schreibmarken-genaue Einfügung im Klartextfeld.** Der `@`-Picker fügt am Ende ein, weil
  `MudTextField` keine Schreibmarke herausgibt; ein zweiter Weg dafür wäre eine neue JS-Brücke für
  einen Randfall. Wer mitten im Text einen Baustein braucht, tippt ihn ans Ende und schiebt ihn.
- **Keine Tastatursteuerung in der Klartext-Liste.** Aus demselben Grund, aus dem der `@`-Picker dort
  keine hat — die Pfeiltasten gehören der Schreibmarke. Im Rich-Text-Editor gibt es sie, weil das
  Slash-Menü sie dort schon hat.
- **Keine geteilten Bausteine, keine Führungs-Vorlagen.** Dafür gibt es die fünf vorhandenen
  Vorlagen-Systeme. Ein sechstes, das auch noch geteilt wird, wäre ein zweites Dokument-Vorlagenwesen.
- **Kein Baustein für Partner und Nur-Lese-Aufsicht.** Beide sind im `ReadOnlyBarrierInterceptor`
  nicht freigeschaltet, und `SavedSearch` macht es genauso. Eine Ausnahme in der Schreibsperre für
  Schreibkomfort wäre der falsche Preis.

## Aufbau

1. **Plan festschreiben** — `docs/superpowers/plans/2026-09-22-textbausteine.md`.

2. **Entität** `Data/Entities/Common/TextSnippet.cs`, `[Table("Textbausteine")]`, `IAuditable`, **kein**
   `ISoftDelete` — wie `SavedSearch`, und damit ohne Papierkorb-Pflicht. Felder: `Id`, `AgentId`,
   `Name` (Spalte `Bezeichnung`, max 80), `Text` (Spalte `Text`, max 4000), `Sorting`
   (`Reihenfolge`). Namensparallele zu `TextImage`/`Textbilder`. `DbSet` heißt **`Textbausteine`**
   (deutsch, wie 147 der 148 anderen). EF-Block neben dem von `SavedSearch`, Fremdschlüssel auf den
   Agenten mit **`Cascade`** (siehe oben), eindeutiger Index auf `(AgentId, Bezeichnung)`.

3. **Dienst** `Services/ITextSnippetService.cs` + `TextSnippetService.cs`:
   `GetForAgentAsync`, `CreateAsync`, `RefreshAsync`, `DeleteAsync`. Jeder Schreibpfad beginnt mit
   `Permission.RequireWriteAccess(actor)`; **jeder Pfad arbeitet ausschließlich auf der eigenen
   `AgentId`** (aus dem Principal, nie aus dem Aufruf), damit es keine fremde Sammlung gibt.
   Obergrenze **50 Bausteine je Agent** — eine Liste, die nicht mehr überblickbar ist, wird nicht
   mehr benutzt; die Grenze steht im Dienst, nicht in der Oberfläche.

4. **Platzhalter ohne HTML-Kodierung** — `PlaceholderService` bekommt einen zweiten Weg für
   Klartextziele. Zwei Unterschiede zum vorhandenen `ApplyAsync`, beide bewusst: die Werte werden
   **nicht** kodiert, und ein Akten-Token bleibt **roh stehen**, wenn gar keine Akte da ist (statt zu
   leeren) — dann sieht der Agent, dass dort etwas hingehört. Gemeinsame Auflösung, gemeinsame
   Sichtbarkeitsprüfung.

5. **Auslöser im Klartextfeld** — `MentionInput.razor`: zweiter Regex, zweite Kandidatenliste,
   derselbe `MentionPicker`. Die Bausteine werden **einmal je Komponente** geladen (beim ersten
   Auslösen), nicht je Tastendruck. Einfügen ersetzt das `/…` und ruft die Klartext-Ersetzung mit
   `ImageOwnerType`/`ImageOwnerId` als Aktenbezug. Leerzustand: ein Hinweis, der auf `/profil` zeigt.

6. **Verwaltung** — `Components/Pages/Account/Shared/TextSnippetsPanel.razor`, eingehängt in
   `MyProfil.razor`. Kein neuer Menü-Eintrag: damit auch kein neuer Pflicht-Artikel im Handbuch, und
   „meine Sachen" liegen dort, wo sie hingehören. Vorschau der Platzhalter über die vorhandene Liste
   `PlaceholderService.AvailablePlaceholder`.

7. **Slash-Menü im Rich-Text-Editor** — die Bausteine des Agenten werden beim Start als Parameter an
   `initRichText` gereicht (so wie die Sanitizer-Listen), erscheinen als eigene Gruppe im vorhandenen
   Menü und holen sich beim Auswählen den fertigen Text über **einen** Rundruf nach C# — exakt der
   Weg, den `toc` schon geht. `richtext.js?v=19` → `?v=20`. **Diese Aufgabe steht zuletzt**: wenn sie
   mehr verlangt als den `toc`-Pfad, wird sie eine eigene Etappe, statt den Rest aufzuhalten.

8. **Pflichtregister** (alles nach dem Muster von `SavedSearch`): `DbSet` + Fluent-Block,
   `PublicVisibility` → `NeverPublic` mit Begründung, `SearchCatalog`-Zeile in `SearchGroup.Personal`
   mit `SearchTraits.Personal` (das Merkmal heißt „Zeilen gehören dem Betrachter, der Anbieter filtert
   auf `MeId`") + Anbieter in `PersonalSearchProviders.cs` (`PartnerAccess.Never`, Treffer-Adresse
   `/profil` **im Anbieter**, so wie die gespeicherte Suche ihre `/suche` setzt) + eine Zeile in
   `SearchProviderRegistration.cs`, `INooseiTool` → `NotAssistantReadable` mit Begründung,
   DI-Zeile in `Program.cs`, Migration `Phase83_Textbausteine`.
   **Kein** Papierkorb-Eintrag (harte Löschung), **kein** Eintrag im `ReadOnlyBarrierInterceptor`,
   **kein** `FeedbackPageTabs`-Eintrag (der Abschnitt liegt auf `/profil`, nicht in den Einstellungen).
   **Zusätzlich**, obwohl kein Test es erzwingt: ein Label und eine Adresse in `AuditEntityDisplay`.
   Die drei persönlichen Nachbarn haben keine, ihre Zeilen stehen im Nachweis deshalb mit dem rohen
   englischen Klassennamen da. Das ist kein Grund, den vierten genauso zu lassen.

9. **Changelog** `2.2.06-textbausteine`, Bereich „Bedienung". **Handbuch:** ein neuer Artikel
   „Textbausteine" im Kapitel *Erste Schritte* — neuer Artikel, also **keine** Revisions-Erhöhung,
   solange keine bestehende Zeile angefasst wird.

10. **Bauen** und **gezielt testen**; danach Review durch Agents, Korrekturen, Commit und Push in
    Tristans Namen.

## Tests

- `TextSnippetTriggerTests` (rein, testbar): der Regex greift bei `/obs` am Zeilenanfang und nach
  einem Leerzeichen, **nicht** in `Vinewood/Ost`, nicht bei einem Schrägstrich ohne Buchstaben
  dahinter, nicht mitten im Text; das Einfügen ersetzt genau das `/…` und hängt sonst an.
- `PlaceholderServiceTests` (+): der Klartext-Weg kodiert nichts (Umlaute, `&`, `<` bleiben stehen),
  lässt ein Akten-Token ohne Akte **roh**, und setzt keinen Namen ein, den der Agent nicht sehen darf
  — der letzte Fall über einen als Verschlusssache eingestuften Datensatz.
- `TextSnippetServiceTests` (Integration, `SqliteTestContext`): fremde Sammlung ist unerreichbar
  (Lesen leer, Ändern und Löschen werfen), doppelter Name wird abgelehnt, die Obergrenze greift,
  Nur-Lese-Aufsicht wird abgewiesen (der Testkontext hängt keine Interceptors an, misst also wirklich
  den Guard).
- Die vorhandenen Deckungstests müssen **grün werden, nicht angepasst**:
  `PublicVisibilityCoverageTests`, `SearchCoverageTests`, `SearchCatalogTests`, `NooseiCoverageTests`,
  `TrashServiceTests`, `AgentDeleteCoverageTests`, `ChangelogTests`, `HandbookTests`.
- Jede neue Schranke wird einmal sabotiert, um zu sehen, dass der richtige Test rot wird — Sicherung
  vorher in den Scratchpad, Rückspielung von dort, nie `git checkout`.

## Abnahme im Browser (Tristans Klickstrecke)

1. `/profil` → Abschnitt *Textbausteine* → zwei anlegen, einen davon mit `{{Name}}` und `{{Datum}}`.
2. Eine Personenakte öffnen, im Kommentarfeld `/` tippen → Liste erscheint; weiter tippen filtert.
3. Den Baustein mit Platzhaltern wählen → im Feld steht der Name **dieser** Person und das heutige
   Datum, mit korrekten Umlauten.
4. Im Taskforce-Chat dasselbe; in einem Feld ohne Akte (etwa Feedback) bleibt `{{Name}}` stehen.
5. „Vinewood/Ost" tippen → **keine** Liste.
6. In einem Dokument `/` am Zeilenanfang → die Bausteine stehen im Menü zwischen den Blockformaten.

## Offen aus früheren Etappen

Sieben Vorhaben sind gebaut und gepusht, aber **nie im Browser gesehen**: Handbuch/Changelog/NOOSEI,
Aktenarchiv, Score-Verlauf, Funkplan, Abwesenheits-Hinweis, Tastatur-Kurzbefehle, Schnellerfassung.
Es gibt kein bUnit; `.razor` läuft in keinem Testlauf. Die Klickstrecken stehen je Vorhaben in
`docs/superpowers/plans/`.

**Nebenbefund, nicht Teil dieser Etappe:** der Backlog-Eintrag **#9** („Entwurf geht nicht mehr
verloren") ist überholt — der Rich-Text-Editor sichert Entwürfe längst in der Browser-Datenbank, an
über zwanzig Stellen verdrahtet. Offen wäre dort nur noch die Sicherung der **Klartext**-Felder.

## Umgesetzt am 22.09.2026

Aufgaben 1-10 stehen, einschließlich der zuletzt gestellten Aufgabe 7 (Slash-Menü im Rich-Text-Editor);
die Abnahme im Browser ist offen.

**Abweichungen vom Plan, alle bewusst:**

1. **Der Rich-Text-Editor kennt seine Akte nicht.** Aufgabe 7 ist gebaut, aber `{{Name}}` und
   `{{Aktenzeichen}}` bleiben dort **roh stehen**: die Komponente bekommt nirgends einen Aktenbezug
   übergeben, und ihn durch einundzwanzig Aufrufstellen zu reichen wäre mehr gewesen als der
   `toc`-Pfad, den der Plan als Grenze gesetzt hat. Datum, Uhrzeit, Codename und Dienstgrad lösen
   auch dort auf. Im Klartextfeld — also dort, wo der Backlog die Bausteine haben wollte — löst alles
   auf, weil `ImageOwnerType`/`ImageOwnerId` die Trägerakte bereits benennen.
2. **`Preview` stand zweimal.** Die Vorschauzeile war in beiden Komponenten dieselben vier Zeilen und
   ist jetzt eine Methode neben dem Auslöser, mit eigenen Tests. Zwei Kopien derselben Regel driften.
3. **`richtext.js` wird an zwei Stellen importiert.** Die zweite (`DiscardDraftAsync`) stand noch auf
   `?v=19`, nachdem die erste auf `?v=20` ging — zwei verschiedene Werte holen zwei Kopien des Moduls.
   Beide sind jetzt gleich.
4. **`setInhaltsverzeichnis` delegiert.** Das Einfügen an der Schreibmarke war an das
   Inhaltsverzeichnis gebunden; es ist jetzt eine eigene Funktion, die beide benutzen. Der Export
   bleibt, damit nichts außerhalb bricht.

## Falsifikation

Jede neue Schranke wurde einmal sabotiert — Sicherung vorher in den Scratchpad, Rückspielung von dort,
nie `git checkout`:

| Sabotage | Erwartet rot | Ergebnis |
|---|---|---|
| Der Eigentümer fliegt aus der Suche in `RefreshAsync` | `ForeignCollection_IsUnreadable_UneditableAndUndeletable` | rot |
| `Permission.RequireWriteAccess` entfernt | `Partner_MayNotCreate`, `OnlyReader_MayNotCreateChangeOrDelete` | rot |
| Der Klartext-Weg kodiert wieder | `ApplyPlainAsync_KeepsUmlautsAndMarkupCharacters_WhereTheHtmlPathEncodesThem` | rot |
| Die Wortgrenze vor dem Schrägstrich entfernt | `ASlashInsideAWordAsksNothing`, `ASecondSlashEndsTheQuery`, `InsertingLeavesASlashInsideAWordAlone` | rot |

Danach alles zurückgespielt und erneut grün geprüft: Bau 3 Projekte, 0 Fehler, 41 Warnungen
(Ausgangsstand); 312 gezielte Tests bestanden.

## Was der Review ergab

Sechs Blickrichtungen (Eigentum, Auslöser, Rich-Text, Platzhalter, Daten, Inhalt), jeder Befund von
zwei Skeptikern mit unterschiedlicher Linse gegengeprüft, Voreinstellung „widerlegt": **16 Rohbefunde,
13 überlebt, 3 widerlegt**. Die dreizehn fallen auf fünf Sachen zusammen.

**1. Die beiden Akten-Platzhalter verschwanden still, statt roh stehen zu bleiben.** Von **sechs** der
sechs Blickrichtungen unabhängig gefunden. `resolved` wurde gesetzt, sobald die Sichtbarkeitsprüfung
ja sagte — nicht, sobald wirklich ein Datensatz geantwortet hatte. `RecordNameAsync` kennt aber nur neun
Aktentypen; ein Kommentar hängt unter anderem an Besprechung, Termin, Asservat, Kassenbuchung,
Entführung und Finanzierungsantrag. Für die alle fiel die Auflösung in ihren Standardzweig und lieferte
zwei leere Zeichenketten, die dann als „aufgelöst" durchgingen. Aus „zu {{Name}} ({{Aktenzeichen}})"
wurde „zu  ()" — **genau das Gegenteil** dessen, was die Methode zusichert und was das Panel dem Agenten
verspricht. `RecordNameAsync` gibt jetzt `null` zurück, wenn sie den Typ nicht kennt oder die Zeile nicht
findet; der Unterschied zwischen „kenne ich nicht" und „hat kein Aktenzeichen" ist damit im Typ verankert
und nicht mehr in der Sorgfalt. Zwei Tests halten beide Wege fest — der HTML-Weg leert weiter, wie er soll.

**2. Der private Text landete im Änderungsprotokoll.** Von vier Blickrichtungen gefunden. `/nachweis`
liest **jeder interne Agent**, und der Audit-Interceptor schreibt bei jeder Änderung Name und Text
(alt und neu) in die Zeile. Das Panel sagt „Sie gehören dir allein", der Handbuch-Artikel „niemand sonst
sieht sie" — beides war damit unwahr. Genau dafür gibt es `AuditRedaction`: Name und Text stehen jetzt
drin. Die Zeile bleibt (wer wann etwas geändert hat, ist Nachweis), der Inhalt nicht.

**3. Das Einfügen lief an der Feldgrenze vorbei.** `MudTextField.MaxLength` hält nur das Tippen auf;
ein Baustein, der direkt in die gebundene Zeichenkette geschrieben wird, geht daran vorbei — und an der
Spalte dahinter. `Insert` nimmt jetzt die Grenze des Feldes entgegen und schneidet, wie es das Feld bei
getippten Zeichen auch täte.

**4. Das Slash-Menü aß die Zeile, wenn der Rundruf fehlschlug.** Der gemeinsame Zweig löschte das
getippte `/wort`, bevor feststand, dass überhaupt etwas zurückkommt. Der Baustein-Zweig sitzt jetzt
**vor** dem Löschen und räumt selbst auf — erst wenn die Antwort da ist.

**5. Die Oberfläche beschrieb den Auslöser zu grob.** „In jedem Textfeld … am Wortanfang" stimmt für
gewöhnliche Felder, nicht für den Rich-Text-Editor (dort: Anfang einer Zeile). Panel und Handbuch sagen
jetzt beides, und der Artikel hält zusätzlich fest, dass die Akten-Platzhalter im Editor immer stehen
bleiben. Dazu nennt das Panel die Obergrenze, statt den Knopf nur grau werden zu lassen.

**Widerlegt (3):** der Vorwurf, der Audit-Befund sei hoch statt mittel (dieselbe Sache, andere Bewertung);
die Behauptung, Handbuch und Changelog versprächen die Akten-Platzhalter an allen 43 Feldern; und die
fehlende Anzeige der Obergrenze — letztere habe ich trotzdem behoben, weil sie stimmt.

**Zusätzliche Falsifikation nach den Korrekturen:**

| Sabotage | Erwartet rot | Ergebnis |
|---|---|---|
| `resolved` bedeutet wieder „war sichtbar" | `ApplyPlainAsync_CarrierTypeItCannotResolve_LeavesTheTokensRaw` | rot |
| Die Feldgrenze wird ignoriert | `InsertingRespectsTheFieldsOwnLimit` | rot |
| Der Baustein-Text ist nicht mehr geschwärzt | `A_personal_text_snippet_keeps_its_wording_out_of_the_protocol` | rot |

Nebenbefund: die erste Sabotage von (1) ließ sich gar nicht bauen — seit der Rückgabetyp nullable ist,
weist der Compiler den Rückfall selbst ab. Für den Testlauf brauchte es eine Variante, die kompiliert.

Endstand: Bau 3 Projekte, **0 Fehler, 41 Warnungen** (Ausgangsstand); **543 gezielte Tests bestanden**.

