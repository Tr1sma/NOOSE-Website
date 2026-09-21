# Abwesenheit im Agenten-Picker anzeigen

Roadmap-Idee #4 aus `IdeenBacklog.md`. Nachgelesen am 21.09.2026, bevor eine Zeile entstand.

## Was gebaut wird

Wer in einer Auswahlliste steht und für den Zieltag abgemeldet ist, trägt dort einen Hinweis —
und unter dem Feld steht eine Warnzeile, sobald ein Abgemeldeter wirklich ausgewählt ist.

## Der Befund aus dem Code

- `AbsenceVisibility.Covering(day)` ist fertig und wird schon von `MeetingService` und dem
  `MeetingReminderWorker` benutzt. Die Abfrage muss nicht erfunden werden.
- Es gibt **keine** gemeinsame Picker-Komponente. Vierzehn Stellen rufen
  `IAgentManagementService.GetSelectableAsync()` auf und rendern ihre Liste selbst.
- Drei davon tragen ein Zieldatum und eine Frist, und nur dort ist die Abwesenheit eine Aussage:
  `Jobs/Shared/JobForm.razor` (Fälligkeit), `Calendar/Shared/AppointmentForm.razor` (Beginn),
  `Common/Shared/FollowupDialog.razor` (fällig am). Die übrigen elf — Verknüpfung, Entführung,
  Asservat, Kasse, Vermerk, Ticket, Gruppe, Führungsprofil — haben kein Datum; ein „abgemeldet"
  wäre dort eine Behauptung ohne Bezugspunkt und bleibt deshalb weg.

## Entscheidungen

| Frage | Entscheidung |
|---|---|
| Zieltag | das Datum des Formulars, solange keins gesetzt ist: heute |
| Datum geändert | Hinweise werden neu geladen, nicht eingefroren |
| Auswahlmenge | bleibt unverändert — der Hinweis ist reine Anzeige, niemand wird ausgesperrt |
| Grund der Abmeldung | **nie**. `AbsenceVisibility.MayReadPrivateFields` gibt Kollegen die Zeile, nicht das Warum |
| Partner | sehen den Hinweis nicht; die Abfrage verlangt einen internen Agenten |
| Nur Dropdown? | nein — ein Hinweis, den man mit dem Zuklappen verliert, hat man nicht gelesen |

## Aufgaben

1. **Abfrage** — `IAbsenceService.GetAbsentOnAsync(day, actor)` liefert je Roster-Agent den Tag,
   bis zu dem seine Abmeldung läuft. Über `RosterVisible` + `Covering`, also über dieselben zwei
   Erweiterungen, die es schon gibt. `Permission.RequireInternalAgent` als erste Anweisung.
   Überlappen zwei Abmeldungen, gilt die, die später endet.
2. **Wortlaut** — `Services/AbsenceHint.cs`, ein statischer Helfer, damit die drei Stellen
   denselben Satz schreiben. Im selben Jahr `bis 14.09.`, sonst mit Jahreszahl.
3. **Drei Picker** — Hinweis in der Zeile, Warnzeile unter dem Feld für das, was ausgewählt ist.
4. **Tests** — Wortlaut als reiner Helfer; die Abfrage gegen Randtage (beide Grenzen einschließend),
   gegen Teamleitungen und Partner im Bestand, gegen zwei überlappende Abmeldungen, gegen einen
   Partner als Aufrufer.
5. **Changelog** `2.1.68-abwesenheit` und ein Absatz im Handbuch-Artikel `art-abmeldungen` —
   der ist eine Änderung an einer bestehenden Zeile, also **`HandbookContent.Revision` 11 → 12**.
6. **Abnahme** — Tristans Klickstrecke: sich selbst für morgen abmelden, dann eine Aufgabe mit
   Fälligkeit morgen anlegen und sich selbst zuweisen.

## Umgesetzt am 21.09.2026

Aufgaben 1-5 stehen; Aufgabe 6 (Abnahme im Browser) ist offen.

**Eine Erweiterung gegenüber dem Plan.** Der Plan nannte drei Formulare. Der Review hat gezeigt, dass das
Zuweisen *nach* dem Anlegen durch eine andere Oberfläche läuft — `JobAgentsPanel` und
`AppointmentParticipantPanel` — und die versprach das Handbuch mit. Beide teilen sich
`AgentAllocateDialog` mit sieben weiteren Panels, deshalb hat der Dialog jetzt ein **optionales**
`ReferenceDay`: gesetzt zeigt er den Hinweis, nicht gesetzt verhält er sich wie zuvor. Fraktion,
Partei, Gruppe, Vorgang, Operation, Taskforce und Ticket sind unberührt.

**Was die drei Review-Durchgänge ergaben.** Hauptteil: 18 Rohbefunde, keiner überlebte die
Gegenprüfung; fünf habe ich trotzdem abgearbeitet, weil die Skeptiker auf *erreichbar* prüfen, nicht
auf *richtig*. Nachtrag: ein bestätigter Befund, und ein präziser — der Glossarbegriff
`beg-abmeldung` trug wörtlich noch den Satz, den der Artikel gerade losgeworden war ("Wer abgemeldet
ist, erscheint in Auswahllisten als abwesend"), und die Erklär-Blase rendert **in** den korrigierten
Absatz hinein. Wer das Wort überfährt, bekam eine Zeile später das Gegenteil dessen zu lesen, was
dort stand. Behoben; der Begriff fährt auf derselben Revisions-Erhöhung mit.

Dazu vier Dinge aus dem Hauptteil, die zwar widerlegt, aber richtig waren:

- Der Handbuch-Absatz behauptete "maßgeblich ist das Datum im Formular, **nicht** der heutige Tag" —
  ohne eingetragenes Datum ist es aber sehr wohl heute.
- Das Anlegen-Formular fiel auf heute zurück, das Panel schwieg. Dieselbe Aufgabe hätte beim Anlegen
  gewarnt und beim späteren Zuweisen nicht. Jetzt derselbe Rückfall an beiden Stellen.
- Die sechs Dienst-Tests lasen die Wanduhr mehrfach je Test; ein Lauf über Mitternacht wäre
  geflackert. Jetzt einmal je Test fixiert.
- Der Test zur unveränderten Auswahlliste hieß, als prüfe er die Zusage vollständig. Er prüft die
  Roster-Abfrage, nicht die Darstellung darüber — der Name sagt das jetzt.

**Bekannte Kanten, bewusst nicht geändert.**

1. **Aneinandergrenzende Abmeldungen werden nicht verkettet.** Wer vom 21. bis 23. und getrennt davon
   vom 24. bis 28. abgemeldet ist, bekommt für den 22. den Hinweis "bis 23." — obwohl er erst am 29.
   zurück ist. Zusammengefasst wird nur, was denselben Tag abdeckt. Eine Verkettung wäre ein Lauf über
   die Folgetage; das lohnt für einen Hinweis nicht, der ohnehin nur warnt.
2. **Der Rückfall auf heute liest die Prozess-Zeitzone** (`DateTime.Now`), während der Dienst-Layer die
   fest verdrahtete Berliner Zone benutzt. In der dokumentierten Konfiguration (`TZ=Europe/Berlin`) ist
   das identisch; ohne diese Variable sind laut `docs/DEPLOYMENT.md` ohnehin alle Zeiten verschoben.
   Der Abmelde-Dialog selbst macht es seit jeher genauso.
