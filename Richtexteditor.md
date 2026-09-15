# Rich-Text-Editor — Ausbauplan

Beschlossen: Bilder nur für **interne Träger** als Datei (öffentliche Seiten bleiben Base64),
Speicherung als `<figure>`/`<figcaption>`, Reihenfolge Bilder → Sauberkeit → Struktur → Gefühl.
Jedes Paket endet mit Build, Tests, Changelog-Zeile, `?v=`-Bump und Klicktest. Kein Commit ohne Freigabe.

## Paket 1 — Bilder

- [x] **1.1 Verkleinern beim Einfügen** — Screenshots kommen mit mehreren MB vom Clipboard; sie werden
  vor dem Einfügen auf max. 2000 px Kante skaliert und als WebP neu kodiert (JPEG-Rückfall, GIF
  unangetastet, bei Fehlschlag Original). Dateien: `wwwroot/js/richtext.js` (`haengeBildEinfuegungAn`,
  `registriereBildMatcher`). Risiko: Canvas/WebP fehlt im Browser → Original bleibt, Einfügen darf nie
  scheitern. Test: `node --check` + Klicktest (Screenshot, Copilot-Bild, GIF).
- [x] **1.2 Base64 → Datei beim Speichern** — neuer `RichTextHtmlInterceptor` (letzter in der Kette),
  Registry `RichTextImageFields` (nur interne Spalten), legt `TextImage`-Zeile im selben Kontext an und
  ersetzt `data:`-URIs durch `/dateien/textbilder/{id}`; danach `HtmlCleanup.Clean` erneut. Zu groß oder
  kein Träger → Base64 bleibt. Dateien: `Infrastructure/RichTextHtmlInterceptor.cs`,
  `Services/RichTextImageFields.cs`, `Services/TextImageService.cs` (URL statt Token), `Program.cs`.
  Risiko: Schreibpfad umgeht Sanitizer → deshalb Nachreinigung; Waisen-Dateien bei Rollback (harmlos).
- [x] **1.2b Sichtbarkeits-Gate je Träger** — registrierte Typen antworten über
  `Visibility.IsRecordVisibleAsync` (Document behält seinen eigenen Sonderfall); registriert sind
  `Document`, `AgentActivity`, `Meeting`, `MeetingAgendaItem`, `Announcement`, `AgentNote`.
  Bewusst **nicht** konvertiert: Templates, `HandbookArticle`/`GlossaryTerm` und `AgentPromotionRequest`
  (kein Gate in `Visibility`, ein Fallback wäre „unbekannt = sichtbar"), `BewerbungMessage` (die
  Bewerber-Ansicht ist öffentlich, eine Bild-URL liefe dort ins Leere). Wer einen davon aufnimmt, ergänzt
  zuerst ein Gate und dann die Registry. `RichTextImageFieldsTests` erzwingt, dass jeder registrierte Typ
  vom zentralen Gate entschieden wird, und dass kein öffentlicher Träger in der Liste steht.
- [x] **1.3 Bild-Dialog** — Klick aufs Bild öffnet `ImageFormatDialog` (Größe 25/50/75/100 %, Ausrichtung,
  Alt-Text, Beschriftung, Entfernen); die Werte kommen beim Klick mit, die Anwendung läuft über
  `setBildOptionen` mit Index-Prüfung. Kein „Ersetzen" (kommt später, falls gebraucht).
  Dateien: neu `Components/Common/Shared/ImageFormatDialog.razor`, `RichTextEditor.razor`, `richtext.js`.
- [x] **1.4 figure/figcaption** — Editor-Modell: Bildzeile + Absatz `noose-bildtext` (inline tippbar,
  eigenes Quill-Zeilenformat); `RichTextFigure.ToStored`/`ToEditor` falten zu `<figure>` bzw. entfalten,
  beide Richtungen in C# und getestet. Breite ist das Quill-`width`-Attribut in Prozent (Quill kann keine
  Klassen auf dem Bild-Blot), gelesen über `img[width="…"]`; Ausrichtung über das Zeilenformat.
  Dateien: neu `Services/RichTextFigure.cs`, `richtext.js`, `RichTextEditor.razor`, `HtmlCleanup.cs`.
- [x] **1.5 Lese-/Druck-CSS** — `figure`, `figcaption`, Breiten-Klassen; `img` bleibt `max-width: 100%`.
  Tests: Sanitizer behält `figure`/`figcaption`, entfernt Skripte; Rundlauf-Tests für `RichTextFigure`.
- [ ] **1.6 Backfill Altbestand (optional)** — Muster `SearchIndexBackfillWorker`.

## Paket 2 — Einfüge-Sauberkeit (WYSIWYG stimmt)

- [x] **2.1 Whitelist als eine Wahrheit** — `HtmlCleanup.Profile` (Tags/Attribute/CSS-Properties/Schemes) ist
  aus denselben Arrays gebaut, die der Sanitizer benutzt; `initRichText` bekommt es als Parameter.
- [x] **2.2 Clipboard-Matcher** — der Einfüge-Pfad putzt vor Quill: unbekannte Tags werden entpackt,
  `script`/`style`/`meta`/Kommentare fliegen, Attribute/Klassen/Inline-Styles gegen das Profil gefiltert,
  Word-Listen (`mso-list`) werden echte Listen, `font-weight`/`font-style`/`text-decoration` zu
  `<strong>`/`<em>`/`<u>`/`<s>` (deshalb braucht der Sanitizer kein `font-weight`).
- [x] **2.3 Strg+Shift+V** — fügt über `navigator.clipboard.readText` reinen Text ein.
- [x] **2.4 Profil-Tests** — `Profile_NamesTheAllowlistTheEditorCleansAgainst` hält die Liste gegen die
  Erwartungen der Editor-Funktionen; durch die gemeinsame Quelle ist Drift strukturell ausgeschlossen.

## Paket 3 — Struktur für große Texte

- [x] **3.1 Hinweis-Kästen** — `noose-kasten-hinweis|warnung|info` als Zeilenformat (Text bleibt tippbar),
  drei Toolbar-Knöpfe, Slash-Einträge, Editor- und Lese-CSS. Kein Sanitizer-Eintrag nötig (nur Klassen).
- [x] **3.2 Trennlinie** — `trenner` als `<hr>`-Block-Embed, Toolbar-Knopf und Slash-Eintrag, `hr` im Sanitizer.
- [x] **3.3 Tabellen-Feinschliff** — Modul-Kontextmenü und Tooltip waren vorhanden; Kopfzeilen (`th`) haben
  jetzt eine eigene Optik. Ein Einfüge-Hinweis bleibt offen (bewusst, das Modul zeigt sein Menü selbst).
- [x] **3.4 Inhaltsverzeichnis** — `RichTextAnchors.ToStored` vergibt beim Speichern Ids an `h1–h3`
  (Slug-Regeln wie Handbuch, Dubletten `-2`), `BuildToc` baut aus denselben Slugs die Liste; Toolbar-Knopf
  und Slash-Eintrag, `id` im Sanitizer, `scroll-margin-top` gegen die App-Bar. Die Ids entstehen erst beim
  Speichern — vor dem ersten Speichern zeigt ein TOC-Link im Editor noch ins Leere (dokumentiert).

## Paket 4 — Gefühl & Feinschliff

- [x] **4.1 Undo/Redo-Knöpfe** (History-Modul; Ctrl+Z/Y läuft bereits).
- [x] **4.2 Speicherstatus** — JS meldet nach jedem Entwurfs-Schreibvorgang `OnDraftSaved`; die Statuszeile
  zeigt „Entwurf gesichert HH:mm", `MarkSavedAsync` löscht die Anzeige.
- [x] **4.3 Kürzel** — Ctrl+K öffnet den Link-Dialog, Ctrl+S ruft `OnSaveRequested` (verdrahtet in
  Dokument- und Aktivitäts-Editor; weitere Seiten können den Callback anschließen).
- [x] **4.4 Auswahl-Bubble** — eigenes JS-Overlay (`.noose-auswahl`) mit Fett/Kursiv/Unterstrichen/
  Durchgestrichen/Link/Formatierung entfernen; verschwindet bei leerer Auswahl, im Kompaktmodus aus.
- [x] **4.5 Kleinigkeiten** — Tooltips/Aria für alle neuen Knöpfe, Fokus zurück ins Feld nach dem Bild-Dialog.

## Fortschritt

- [ ] Paket 1 · [ ] Paket 2 · [ ] Paket 3 · [ ] Paket 4

## Offene Verifikationen beim Bauen

- Quill 1: `formatText` auf dem Bild-Blot, `getBounds` im Dialog, `id`-Rundlauf durch
  `dangerouslyPasteHTML` — jeweils zuerst im Editor prüfen; Ausweichvarianten stehen im Plan.
