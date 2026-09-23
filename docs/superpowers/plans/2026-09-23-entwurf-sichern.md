# Etappe 9 — Entwurf geht nicht mehr verloren

Roadmap-Idee **#9** aus `IdeenBacklog.md:218` (angenommen, Jury-Schnitt 5,3). Tristans Auftrag vom
23.09.2026: *„Stelle sicher, dass Phase 9 komplett und gut fertig ist."*

## Befund: halb gebaut

Der Backlog-Eintrag ist älter als die Arbeit am Editor. Seitdem gilt:

- **Der Rich-Text-Editor sichert bereits.** `richtext.js` schreibt den Inhalt entprellt (800 ms) in die
  Browser-IndexedDB (`noose-rte`/`entwuerfe`), Schlüssel = Agent + `DraftKey`, sieben Tage Haltbarkeit,
  Löschung beim Abmelden. **Alle 23 Editorstellen** geben einen `DraftKey` mit, nur der Bewerberchat
  (`Compact`) bewusst nicht. Es gibt allerdings **keinen Test**, der das festhält: eine neue Editorstelle
  ohne Schlüssel hätte still keine Wiederherstellung.
- **Die einfachen Textfelder sichern gar nichts.** `MentionInput` ist das Eingabefeld für fast jeden
  Fließtext außerhalb des Editors — 67 Stellen, darunter genau die, die der Backlog nennt: das **Dok**
  (`DocDialog`: „Grund", „Erhaltene Informationen"), die **Operation** (`OperationForm`: „Ablauf",
  „Ergebnis"), dazu Observationen, Vermerke, Taskforce-Chat und die Schnellerfassung.

Besonders bitter: `DocPanel.NewAsync` schließt den Dialog **vor** dem Speichern. Wirft der Dienst, ist der
Dialog weg und der Text mit ihm, auch ohne jeden Verbindungsabbruch.

## Entscheidungen

**1. Eine Ablage, nicht zwei.** Die IndexedDB-Hilfen wandern aus `richtext.js` in ein neues Modul
`wwwroot/js/entwurf.js`, das beide Seiten importieren. So gelten Aufräumen, Sieben-Tage-Grenze und die
Löschung beim Abmelden automatisch auch für die Textfelder; der Datenbankname `noose-rte` bleibt, damit
vorhandene Entwürfe und `window.nooseEntwuerfeLoeschen` weiter passen.

**2. Ein Baustein `TextDraft`** (`Components/Common/Shared/TextDraft.razor`) umhüllt ein beliebiges
Textfeld, horcht im Browser auf `input` und zeigt darunter dasselbe Angebot wie der Editor: *„Nicht
gespeicherter Entwurf vom … gefunden. Wiederherstellen · Verwerfen"*. `MentionInput` bekommt einen
Parameter `DraftKey` und nutzt ihn intern, zwei einfache `MudTextField`-Stellen (Antwort an den Bürger)
umhüllen ihr Feld direkt.

**3. Gesichert wird im Browser, nicht über den Kreis.** Die Schreibvorgänge laufen rein in JS; ist die
SignalR-Verbindung weg, sichert das Feld weiter. Genau dafür existiert die Etappe.

**4. Nur erfolgreiches Speichern oder „Verwerfen" löscht einen Entwurf.** „Abbrechen", Escape, ein Klick
neben den Dialog oder Wegnavigieren lassen ihn stehen — das ist das versehentliche Weg-Navigieren aus dem
Backlog. Dieselbe Regel hat der Editor schon.

**5. Dialoge verwerfen nach Bereich.** Ein Dialog schließt, bevor sein Aufrufer speichert (Begründung
steht schon an `RichTextEditor.DiscardDraftAsync`). Statt jedes Feld einzeln zu nennen, bekommen alle
Felder eines Dialogs denselben **Bereich** (`dok:neu:{PersonId}`) plus Feldnamen, und der Aufrufer
verwirft nach erfolgreichem Speichern den ganzen Bereich. Die Schlüssellogik liegt in einem reinen
Helfer `DraftKeys` (testbar); der Präfix endet auf `:`, damit `dok:1` nicht `dok:12` mitnimmt.

**6. Verwerfen erreicht auch noch offene Felder.** Eine Seite, die nach dem Speichern weiternavigiert,
entsorgt ihre Felder erst danach — ein noch wartender Schreibvorgang (die letzten 800 ms Tippen) würde
den gerade verworfenen Entwurf wieder anlegen. `entwurf.js` führt deshalb eine Liste der angehängten
Felder; Verwerfen markiert passende Felder zugleich als gespeichert.

**7. Nachgezogen am Editor** (Befunde beim Lesen):
- Die Datenbankverbindung hatte keinen `onversionchange`-Handler. Das Löschen beim Abmelden wartet dann,
  bis **jeder** andere Tab die Verbindung schließt — ein zweiter offener Tab hielt die Entwürfe über das
  Abmelden hinaus fest. Die abmeldende Seite selbst schreibt danach nichts mehr; ein anderer Tab, der
  angemeldet bleibt, sichert weiter.
- Wer die Seite schließt oder neu lädt, verlor die letzten 800 ms Tippen: der entprellte Schreibvorgang
  lief nie. `pagehide` und `visibilitychange` sichern jetzt sofort.

## Welche Felder

Maßstab: **Wo ein Agent einen Bericht schreibt.** Nicht jede Begründungszeile.

| Bereich | Stellen |
|---|---|
| Personenakte | Dok (`DocDialog`, `DocCreateDialog`), Observation (`ObservationDialog`) |
| Überall | Vermerke (`CommentPanel`, neu und bearbeiten), Schnellerfassung (`QuickAddDialog`), Quelle (`SourceDialog`) |
| Akten-Formulare | Vorgang, Operation, Fraktion, Partei, Personengruppe, Taskforce, Aufgabe, Termin, Person, Entführung, Asservat, V-Person |
| Einzelseiten | Taskforce-Chat, V-Person-Treffen, Gesetzestext, Finanzierungsantrag, Rückmeldung (Meldung und Antwort) |
| Bürgerkontakt (intern) | Ticket- und Hinweis-Antworten, Ticket-Notizen, Bewerbungs-Notizen |

**Bewusst ohne Entwurf** — ein `TextDraftScanTests` hält die Liste mit Grund fest, damit eine neue lange
Eingabe sich entscheiden muss:
- **Begründungen in Entscheidungsdialogen** (Ablehnung, Kündigung, Einstufungsantrag, Freigabe …): ein,
  zwei Sätze, und ein wiederhergestellter Grund an einer *anderen* Entscheidung wäre schlimmer als keiner.
- **NOOSEI-Frage**: die Antwort ist das Wertvolle, nicht die Frage.
- **Öffentlicher Bereich und Bürgerportal**: andere Nutzer, und für Hinweise gilt die Anonymitätszusage —
  ein im Browser liegender Hinweistext auf einem geteilten Rechner bricht sie.

## Tests (kein bUnit, kein JS-Test-Harnisch)

- `DraftKeysTests`: kein Schlüssel ohne Agent oder Feld; Feld-Schlüssel liegt im Bereichs-Präfix; `dok:1`
  deckt `dok:12` **nicht**; Editor- und Feld-Schlüssel können nie kollidieren.
- `TextDraftScanTests`: jeder `RichTextEditor` hat `DraftKey` oder `Compact`; jede `MentionInput` mit drei
  oder mehr Zeilen hat einen `DraftKey` oder steht mit Grund auf der Ausnahmeliste; jede Datei, die einen
  Bereich vergibt, verwirft ihn (selbst oder im genannten Aufrufer); `richtext.js` und `entwurf.js` werden
  überall mit derselben `?v=` geladen.
- Sabotage jeder neuen Schranke einmal, Sicherung im Scratchpad, nie `git checkout`.

## Was gebaut wurde

- `wwwroot/js/entwurf.js` (neu): Ablage, Aufräumen, Öffnen mit 3-s-Frist, Freigabe der Verbindung bei
  `onversionchange`, Sofort-Sichern bei `pagehide`/`visibilitychange`, Zustandsmaschine der Textfelder.
  `richtext.js` importiert die Ablage daraus (`?v=21`).
- `TextDraft.razor` + `DraftKeys.cs` (neu), `MentionInput` mit `DraftKey`.
- Verdrahtet: 13 Seiten-Editoren samt Formularen, 7 Dialoge samt Aufrufern, 9 Inline-Felder, Zusatzfelder,
  Gefahrenlage, Bürger-Vorlagen. 19 lange Felder stehen mit Grund auf der Ausnahmeliste.
- Changelog `2.2.08-entwuerfe`, Handbuch-Artikel „Nichts geht verloren" (neu, keine Revision),
  `CLAUDE.md`-Regel für einfache Textfelder.

## Abweichungen vom Plan

- **Kein Umschlüsseln.** Die Schnellerfassung bekam zuerst einen Schlüssel je gewählter Akte, der beim Wechsel
  „mitwandern" sollte — das löschte Entwürfe, die nie angeboten wurden. Ein fester Schlüssel für alle Akten war
  der zweite Versuch und trug ein eingefügtes Bild von Akte A auf Akte B. Jetzt: **ein Schlüssel je Akte, ohne
  Wandern**; der Dialog merkt sich, unter welchen Akten getippt wurde, und verwirft nach dem Speichern auch
  diese. Ein Bild sperrt die Aktenwahl ohnehin (`QuickCapture.HoldsImage`).
- **Grundlinie von außen.** Ein Feld nimmt beim Anhängen den angezeigten Wert als gespeicherten Stand. Lädt ein
  Formular seinen Text erst danach, meldet `MentionInput` den neuen Wert (`SetBaselineAsync`) — aber erst ab dem
  zweiten Parametersatz, sonst hielte ein neu aufgebautes Feld den mitgebrachten Entwurf für gespeichert.
- **Nur wer speichern kann, sammelt Entwürfe** (`MayContribute()`), im Editor wie in den Feldern. Sonst bot die
  Seite der Nur-Lese-Aufsicht bei jedem Besuch einen Entwurf an, den nichts je entfernen konnte.

## Was der Review ergab

**Vorab-Audit des Editors** (1 Agent, alle Befunde von Hand nachgelesen) — behoben:
Veröffentlichen (Fahndung, Organisationsprofil) und die FAQ-Autospeicherung ließen den Entwurf stehen; die
FAQ setzte ihre Grundlinie auch nach einem Fehlschlag; „erledigt" an der Tagesordnung verwarf nicht; Handbuch-
und Glossar-Dialog speicherten den um 300 ms verzögerten Wert statt den Editor zu lesen; ein zweiter Tab hielt
die Löschung beim Abmelden auf und ließ danach jede Speicherung eine Minute hängen; Tippen über ein Angebot
hinweg überschrieb es unbemerkt; eine neue Aktivität aus Fraktion A wurde in Fraktion B angeboten; ein
Dokument, dessen Quellen-Verknüpfung scheiterte, blieb als „neu" im Entwurf.

**Runde 1** (5 Agents, 2 davon am Sitzungslimit abgebrochen): bestätigt und behoben — der Bereich `dok:neu`
der Fahndungs-Doks war Präfix der Dok-Entwürfe jeder Person (Speichern dort löschte sie alle); ein Feld, das
unter demselben Schlüssel neu aufgebaut wurde (Quellen-Art, Reiter), löschte seinen eigenen Entwurf; die
Observation füllte ihre Felder erst nach einem `await`; Handbuch-, Glossar- und Gesetzes-Dialog setzten ihre
Vorgabe bei jedem Neuzeichnen zurück (ein Bild-Dialog im Editor reichte); Zusatzfelder sammelten Entwürfe auch
für Leute ohne Speichern-Knopf. Widerlegt: der Wechsel Person bearbeiten → neue Person (die Seite wird über
den `ErrorBoundary`-Schlüssel ohnehin neu aufgebaut) — das Schreiben liest trotzdem nicht mehr aus dem DOM.

**Runde 2** (12 Agents, Workflow; 17 Befunde, 8 gegengeprüft: 6 bestätigt, 2 widerlegt, 9 von Hand gelesen) —
behoben: das Umschlüsseln (s. o., dreimal gefunden); eine Vorlage im Editor überschrieb den angebotenen
Entwurf, während das Angebot stand; die erste Grundlinie eines neu aufgebauten Felds löschte den
mitgebrachten Entwurf; ein gleichlautender Entwurf blieb für immer liegen und konnte später veraltet angeboten
werden (jetzt nur behalten, wenn das Feld ihn auf dieser Seite gerade mitgebracht hat); ein dauerhaft
scheiterndes Anhängen versuchte es endlos; ein während des Anhängens entsorgtes Feld blieb im Speicher;
das Abmelden sperrte Entwürfe in einem weiter angemeldeten Tab für immer; was während der Speicher-Rundreise
getippt wurde, galt als gespeichert. Tests: Abwurf-Prüfung zählt je Feld, `<TextDraft>`-Umschluss wird echt
geprüft, ein Import ohne `?v=` zählt als eigene Fassung, neue Schutztests für Portal/öffentlich und für
auslaufende Anführungszeichen — der fand einen echten Fehler: in `LlmQuotaRulesPanel` beendete ein
ASCII-Anführungszeichen den Hilfetext mitten im Satz.

**Runde 3** (6 Agents, Workflow, nur die Korrekturen aus Runde 2; 10 Befunde, 4 gegengeprüft: 3 bestätigt,
1 widerlegt, 6 von Hand gelesen) — behoben: der feste Schnellerfassungs-Schlüssel (s. o.); ein `return` im
`try` übersprang das Aufräumen eines während des Imports entsorgten Felds; eine vorgemerkte Grundlinie
landete bei einem anderen Schlüssel; gleichzeitiger Schlüssel- und Textwechsel schrieb die Grundlinie ins alte
Feld; wer vor dem Lesen der Ablage schon tippte, überschrieb einen nie gezeigten Entwurf (jetzt kommt das
Angebot immer, und der wartende Schreibvorgang wartet auf die Antwort); *Wiederherstellen* im Editor holte,
was gerade in der Ablage lag, statt des Angebotenen; ein vor dem Abmelden getippter, noch wartender Stand
eines anderen Tabs landete in der frisch gelöschten Ablage. Widerlegt: Tippen während des Speicherns eines
neuen Datensatzes hinterlässt einen Entwurf — das ist ungespeicherter Text und soll bleiben.

**Runde 4** (1 Agent auf den Korrekturen aus Runde 3; kein Fehler mit nennenswertem Datenverlust, fünf kleine
Lücken) — behoben: ein Entwurf, der dem gespeicherten Stand gleicht, wurde nach frühem Tippen doch angeboten;
ein schon laufender Schreibvorgang wartete die Prüfung nicht ab (jetzt wartet jeder Schreibvorgang auf sie und
pausiert, solange ein Angebot steht); die Schnellerfassung verwirft jetzt die Schlüssel, unter denen das Feld
im Browser **wirklich** geschrieben hat (`OnDraftWritten`), nicht die vermuteten; *Verwerfen* sichert, was vor
dem Angebot schon getippt war. Bewusst so gelassen: Tippen beantwortet ein Angebot — auf einer langsamen
Verbindung steht der Knopf danach noch einen Moment da und tut nichts; wer in der Schnellerfassung den Entwurf
der neu gewählten Akte wiederherstellt, gibt den eben getippten auf.

## Falsifikation

Jede Schranke einmal kaputt gemacht, Sicherung im Scratchpad, Rückspielung byte-gleich geprüft:

| Nr. | Sabotage | rot |
|---|---|---|
| 1 | Bereichs-Präfix ohne abschließendes `:` | `A_scope_does_not_reach_into_a_longer_one` |
| 2 | Feld-Schlüssel ohne `text:`-Segment | `A_plain_field_can_never_meet_a_rich_text_draft` |
| 3 | `DraftKey` am Dok-Feld „Erhaltene Informationen" entfernt | `Every_long_plain_field_keeps_a_draft_or_says_why_not` |
| 4 | Abwurf in `DocPanel` entfernt | `Every_dialog_scope_has_a_caller_that_drops_it` |
| 5 | Abwurf im Anlege-Zweig des Vorgangs entfernt | `Every_page_that_owns_a_scope_drops_it_on_each_save_path` |
| 6 | `MarkSavedAsync` im Taskforce-Chat entfernt | `Every_field_draft_is_dropped_after_its_save` |
| 7 | `entwurf.js?v=2` an einer Stelle | `Each_draft_module_is_loaded_under_one_version` |
| 8 | `DraftKey` an den Personalnotizen entfernt | `Every_rich_text_editor_keeps_a_draft_or_is_the_compact_chat` |
| 9 | Ausnahme auf eine nicht existierende Bindung | `The_exemptions_still_name_a_field_that_exists` |
| 10 | `DocCreateDialog` wieder auf `dok:neu` | `No_dialog_scope_reaches_into_another` |
| 11 | nur einer der zwei Vermerk-Abwürfe entfernt | `Every_field_draft_is_dropped_after_its_save` |
| 12 | `DraftKey` an einem Portal-Feld | `The_citizen_portal_and_the_public_pages_keep_nothing_in_the_browser` |
| 13 | ASCII-Anführungszeichen im Hilfetext zurück | `No_field_tag_runs_past_its_end` |
| 14 | `<TextDraft>` vor dem Ticket-Antwortfeld geschlossen | `Every_long_plain_field_keeps_a_draft_or_says_why_not` |
| 15 | `richtext.js` importiert `entwurf.js` ohne `?v=` | `Each_draft_module_is_loaded_under_one_version` |

## Bekannt und bewusst offen

- **Zwei Tabs auf demselben Schlüssel teilen sich einen Entwurf** — der zuletzt schreibende gewinnt.
- **Ein Formular-Verwerfen, das scheitert** (Verbindung reißt genau nach dem Speichern), lässt den Entwurf
  stehen; er wird beim nächsten Öffnen gelöscht, weil er dem gespeicherten Text gleicht.
- **Keine JS-Tests.** Die Zustandsmaschine in `entwurf.js` ist nur durch Review und die Klickstrecke gedeckt.

## Abnahme im Browser (Klickstrecke)

1. Personenakte → *Neues Dok* → in „Erhaltene Informationen" einen Satz tippen → **Escape**. Dialog erneut
   öffnen → Angebot erscheint → *Wiederherstellen* → Text steht wieder da.
2. Dasselbe, dann *Dok speichern* → Dialog erneut öffnen → **kein** Angebot.
3. Vorgang bearbeiten → „Sachverhalt" ändern → Server stoppen (oder WLAN aus) → weitertippen → Seite neu
   laden, sobald der Server wieder läuft → Angebot → wiederherstellen → speichern.
4. Vermerk tippen, Tab schließen, Akte wieder öffnen → Angebot unter dem Vermerk-Feld.
5. Abmelden, wieder anmelden → keine Angebote mehr (auch nicht, wenn vorher ein zweiter Tab offen war).
6. Ein Angebot sehen und einfach weitertippen → das Angebot verschwindet.
7. *Quelle hinzufügen* → Art *Freitext* → Inhalt tippen → Art auf *Link* wechseln → Escape → Dialog erneut
   öffnen → Angebot erscheint (das Feld wurde beim Artwechsel neu aufgebaut und durfte den Text nicht verlieren).
8. Dokument mit Entwurf öffnen, Angebot steht → eine Vorlage wählen → *Wiederherstellen* bringt den alten
   Entwurf, nicht die Vorlage.
