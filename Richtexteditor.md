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

- [ ] **2.1 Whitelist als eine Wahrheit** — `HtmlCleanup` exponiert Tags/Attribute/CSS-Properties/Schemes;
  `RichTextEditor` übergibt sie an `initRichText`.
- [ ] **2.2 Clipboard-Matcher** — unbekannte Tags (`div`→`p`), Attribute und `mso-*` verwerfen,
  Inline-Styles kürzen, Klassen-Whitelist (`ql-*`, `noose-*`), leere Spans/`<br>`-Ketten aufräumen.
- [ ] **2.3 Strg+Shift+V** — Einfügen ohne Formatierung (reiner Text).
- [ ] **2.4 Profil-Tests** — C#-Profil gegen `HtmlCleanupTests` absichern.

## Paket 3 — Struktur für große Texte

- [ ] **3.1 Hinweis-Kästen** — Hinweis/Warnung/Info als Zeilenformat (Text bleibt tippbar), Toolbar +
  Slash, Lese-/Druck-CSS.
- [ ] **3.2 Trennlinie** — `<hr>` als Block-Embed, Sanitizer + CSS + Slash/Toolbar.
- [ ] **3.3 Tabellen-Feinschliff** — Einfüge-Hinweis, Kopfzeilen-Optik, Zellhintergrund, Lese-/Druckprüfung.
- [ ] **3.4 Inhaltsverzeichnis** — `RichTextAnchors` vergibt beim Speichern `id` an `h1–h3` (Slug-Regeln
  wie Handbuch, Dubletten `-2`), TOC-Befehl erzeugt normale Liste mit `#id`-Links aus denselben Slugs,
  „Verzeichnis aktualisieren"; Sanitizer muss `id` erlauben, Lese-CSS `scroll-margin-top`.
  Tests: Idempotenz, Dubletten, `id` überlebt `HtmlCleanup`.

## Paket 4 — Gefühl & Feinschliff

- [ ] **4.1 Undo/Redo-Knöpfe** (History-Modul; Ctrl+Z/Y läuft bereits).
- [ ] **4.2 Speicherstatus** in der Statuszeile („Entwurf gesichert 12:03").
- [ ] **4.3 Kürzel** — Ctrl+K (Link), Ctrl+S über `OnSaveRequested`.
- [ ] **4.4 Auswahl-Bubble** — eigenes JS-Overlay über der Markierung (Fett/Kursiv/Unterstrichen/
  Durchgestrichen/Link/Formatierung entfernen), versteckt bei leerer Auswahl, Esc, Scroll, Vollbild;
  aus im Kompaktmodus.
- [ ] **4.5 Kleinigkeiten** — Tooltips/Aria neuer Knöpfe, Fokus zurück nach Dialogschluss.

## Fortschritt

- [ ] Paket 1 · [ ] Paket 2 · [ ] Paket 3 · [ ] Paket 4

## Offene Verifikationen beim Bauen

- Quill 1: `formatText` auf dem Bild-Blot, `getBounds` im Dialog, `id`-Rundlauf durch
  `dangerouslyPasteHTML` — jeweils zuerst im Editor prüfen; Ausweichvarianten stehen im Plan.
