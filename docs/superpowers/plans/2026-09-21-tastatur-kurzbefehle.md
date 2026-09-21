# Tastatur-Kurzbefehle

Roadmap-Idee #5 aus `IdeenBacklog.md`. Nachgelesen am 21.09.2026, bevor eine Zeile entstand.

## Der Befund aus dem Code

- `wwwroot/app.js` hat genau **einen** globalen Tastatur-Handler (`registerCommandPalette`, Strg+K)
  und sonst nichts. Das Modul ist 26 Zeilen lang.
- Geladen wird es an **zwei** Stellen mit `?v=3` — `CommandPalette.razor` und
  `FinancingCatalogPanel.razor`. Beide müssen mitgezogen werden, sonst holt der Browser zwei
  Kopien (steht so in `CLAUDE.md`).
- `NavCatalog.ByKey(key)` liefert Route, Beschriftung und Symbol fertig; 42 Einträge.
- `CommandPalette` hat die Anlege-Routen schon als Tabelle (`CreateCommands`, neun Stück) und gated
  seine Einträge über `NavSectionPolicy`.
- Fünfzehn Akten-Typen haben eine Route `/{typ}/{Id}/bearbeiten`.

## Die angekündigte Kollision gibt es nicht

Der Backlog warnt: „Das Fragezeichen kollidiert mit dem geplanten Hilfeknopf des Handbuchs. Eines von
beiden muss weichen." Nachgemessen: der Hilfeknopf ist ein **Knopf in der Kopfzeile**
(`HandbookHelpButton`, gerendert von `PageHeader`), kein Tastenbefehl. Es gibt heute genau eine
Tastenbindung auf der ganzen Seite, und das ist Strg+K. `?` ist frei. Nichts muss weichen.

## Was gebaut wird

| Taste | Wirkung |
|---|---|
| `?` | Übersicht aller Kürzel |
| `g` dann Buchstabe | Sprung in einen Bereich (12 Ziele) |
| `n` | Neu anlegen, passend zu der Liste, auf der man steht |
| `e` | Die Akte, auf der man steht, im Bearbeiten-Modus öffnen |

## Was bewusst **nicht** gebaut wird

- **`f` (einer Akte folgen).** Der Backlog nennt es, aber Folgen ist ein **Schreibvorgang**. Ein
  Kürzel, das bei einem versehentlichen Tastendruck Daten ändert, ist den Weg nicht wert — alle
  anderen hier navigieren nur.
- **`/` springt ins Suchfeld.** Es gibt kein seitenweites Suchfeld; jede Liste baut ihr eigenes. Ein
  Kürzel, das auf zwei Dritteln der Seiten ins Leere greift, ist schlechter als keins. Die Suche,
  die es überall gibt, ist Strg+K.

Beides gehört in die Übersicht **nicht** hinein und in diesen Plan schon, damit die Frage nicht
wiederkommt.

## Aufbau

**Das JavaScript bleibt dumm.** `app.js` erkennt nur die Tastenfolge und meldet sie; welche Taste
wohin führt und wer das darf, entscheidet C#. Grund: es gibt kein bUnit, aber ein statischer Helfer
ist testbar — eine Tabelle im `@code`-Block einer `.razor` wäre es nicht.

1. **`Services/KeyboardShortcuts.cs`** — rein und testbar: die `g`-Tabelle (Buchstabe → Nav-Schlüssel),
   `NewRouteFor(pfad)` und `EditRouteFor(pfad)`. Beide arbeiten nur auf der Adresse.
2. **`Components/Common/Shared/KeyboardShortcuts.razor`** — registriert den Handler, nimmt die Taste
   entgegen, löst über den Helfer auf, prüft die Rechte wie die Palette (`NavSectionPolicy` für das
   Ziel, `MayWrite()` für `n` und `e`) und navigiert. Enthält den Übersichts-Dialog.
3. **`app.js`** — ein zweiter Handler neben dem vorhandenen. Er **schweigt, solange der Fokus in einem
   Eingabefeld steht** (`input`, `textarea`, `contenteditable` — damit auch im Quill-Editor), und bei
   jeder Modifikatortaste. `g` öffnet ein Zeitfenster von 1,2 s für den zweiten Buchstaben.
4. **`?v=3` → `?v=4`** an **beiden** Importstellen.
5. **Tests** — der Helfer vollständig: jede `g`-Zuordnung zeigt auf einen existierenden
   `NavCatalog`-Schlüssel; `NewRouteFor`/`EditRouteFor` für Treffer, Nicht-Treffer, Unterrouten und
   die Bearbeiten-Route selbst (kein `…/bearbeiten/bearbeiten`).
6. **Changelog** `2.1.69-kurzbefehle` und ein Handbuch-Artikel — er braucht **keinen** eigenen
   Nav-Eintrag, also auch keinen `NavSchluessel`; er hängt an „Erste Schritte".
7. **Abnahme** — Tristans Klickstrecke: `?` drücken, `g` `p` drücken, auf einer Personenakte `e`,
   auf `/personen` `n`, und einmal mitten in einem Textfeld `g` tippen, damit nichts passiert.

## Umgesetzt am 21.09.2026

Aufgaben 1-6 stehen; Aufgabe 7 (Abnahme im Browser) ist offen.

**Was der Review ergab.** Vier Prüfrichtungen, jeder Befund von zwei Skeptikern gegengeprüft; fünf
überlebten, die auf zwei echte Fehler zusammenfallen — beide in der Autorisierung, beide in Code, den
es vorher nicht gab:

1. **`e` war zu großzügig.** Fünfzehn Akten-Typen haben eine `/bearbeiten`-Route, aber **sechs** davon
   verlangen mehr als `MayWrite()`: die Besprechung eine Sicherheitsfreigabe, Aufgabe, Termin und
   Aktivität den Ersteller oder die Führung, das Brett sein eigenes Verwaltungsrecht, das Dokument
   Urheberschaft und VS-Stufe. Wer dort `e` drückte, verlor die Seite, auf der er las, und bekam eine
   Absage. Die sechs sind jetzt draußen; ein Test nennt jeden einzeln mit seinem Grund.
2. **Der Partner-Zweig war doppelt falsch.** `NavCatalog.PartnerRecordEntries` vergibt Schlüssel der
   Form `partner.Person`, die Buchstaben-Tabelle zeigt auf `personen` — die beiden Schlüsselräume
   schneiden sich nie, der `g`-Sprung war für Partner also ohnehin tot. Schlimmer war die zweite
   Hälfte: ich übergab `null`, und das heißt dort **keine Einschränkung**. Hätte jemand später die
   Buchstaben „repariert", wäre einem Partner jeder Aktentyp angeboten worden, unabhängig davon, was
   seine Behörde freigegeben hat. Der Zweig ist ersatzlos weg — die Abschnitts-Richtlinien antworten
   für einen Partner ohnehin überall mit Nein —, und die `g`-Zeile verschwindet aus der Übersicht,
   statt leer dazustehen.

**Bekannte Kante, bewusst nicht geändert.** Ein Partner, der ein Dokument selbst verfasst hat, darf es
laut Seite bearbeiten (`DocumentView` zeigt ihm den Knopf), `e` tut für ihn aber nichts, weil die Taste
an `MayWrite()` hängt. Das Kürzel schweigt dort, wo der Knopf spricht — schweigen ist die richtige
Richtung, und `dokumente` ist aus dem Grund oben ohnehin draußen.
