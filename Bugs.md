# Bugs.md — Review aller Änderungen seit `8893f17`

**Geprüfter Stand:** `19c10f6` (HEAD, master), Arbeitsbaum sauber.
**Umfang:** 32 Commits, ~190 geänderte Produktionsdateien, ~43.000 neue Zeilen — Handbuch, Changelog,
Onboarding, Rechte, LinkPreview, LinkDialog-Zusammenlegung, Dienstnummer, NOOSEI-Anbieterumschaltung
und der gesamte Texteditor-Ausbau.

**Harte Belege vorab:** `dotnet build NOOSE-Website.slnx` → **0 Fehler**.
`dotnet test` → **7537 Tests, 0 Fehlschläge**. Eine Compiler-Warnung (`CS8604` in
`RichTextAnchors.cs:44`; `heading.Id` ist dort durch `IsOwnSlug` bereits nicht-null — harmlos).
**Keiner der Befunde unten fällt einem Test auf** — genau deshalb stehen sie hier.

**Verfahren:** 26 Finder über alle Subsysteme des Diffs, danach jeder Befund von unabhängigen Prüfern
mit dem ausdrücklichen Auftrag, ihn zu **widerlegen**. 74 Befunde erhoben, davon **18 widerlegt**
(unten in einem eigenen Abschnitt, damit sie nicht ein zweites Mal geprüft werden). Die schwersten habe
ich zusätzlich selbst am Code nachvollzogen; das ist je Befund vermerkt.

**Konfidenz-Kennzeichnung je Befund:**
`[✓✓]` von zwei unabhängigen Prüfern bestätigt · `[✓]` von einem Prüfer bestätigt ·
`[?]` erhoben, aber nicht unabhängig nachgeprüft · `[ich]` von mir selbst am Code nachvollzogen.

**Reihenfolge:** **P0** (App startet nicht, Datenverlust, Geld, Sicherheit) → **P1** (tut nachweislich
das Falsche im Normalbetrieb) → **P2** (Randfall, Last, stiller Teilausfall) → **P3** (Inkonsistenz, Doku).

> Diese Datei **ersetzt** den vorherigen Review-Stand (in der git-Historie unter `938e535`). Befunde
> daraus, die noch offen sind, stehen hier neu erhoben und neu belegt drin — mit Verweis auf die alte Nummer.

---

## Stand der Behebung

**Behoben: 41 von 42** (35 aus dieser Liste, dazu sechs Nachträge unten). Build grün (0 Fehler; die
Produktions-Warnung `CS8604` ist weg), `dotnet test` → **7558 Tests, 0 Fehlschläge**.
**Keine Migration nötig.** Sieben Editor-Fixes sind zusätzlich **im Browser gegen das echte Quill-Bundle
nachgewiesen** (Tabelle weiter unten).

Dazu sechs Nachträge aus einem zweiten Review (37–42), alle von mir am Code nachgeprüft und behoben —
siehe die Abschnitte unten. **Einzige verbleibende Entscheidung: die Rotation der
Data-Protection-Schlüssel**, weil sie alle abmeldet.

Aus demselben zweiten Review **widerlegt**: „Der Testlauf steht aus" (er lief, dreimal) und „Worktree
dirty" (war zwischenzeitlich richtig, ist inzwischen committet).

**Was der Testlauf nicht abdeckt:** `richtext.js`, `app.css` und die `.razor`-Änderungen — dieses Projekt
hat kein bUnit, und JS/CSS laufen in keinem Test. Konkret ungetestet und nur gelesen: Word-Einfügen (2, 8),
Auswahl-Blase (6), Trennlinie (7), Druckfarbe (4), Listen-Nummerierung (9), Suchleiste (18), die
Handbuch-Rail (5), die Hinweiskarte (21) und der Entwurfs-Rundlauf (17, 29). **Das sind die Punkte, die
einen Blick im Browser brauchen.**

| # | Was geändert wurde |
|---|---|
| **1** | Alle drei Einfügepfade des Seeders prüfen vor dem Schreiben, wer den Slug bzw. den Begriff schon hält — der Umbenennungspfad ebenso. Zusätzlich läuft **jeder** Inhalts-Seeder in `Program.cs` jetzt in seinem eigenen `try/catch` mit `ChangeTracker.Clear()`: ein Inhaltsfehler kostet seinen Inhalt, nicht den Start. |
| **2** | `wandleWordListen` entfernt nur noch den Marker-Span (`[style*="mso-list"]`). Die übrigen `mso-`-Eigenschaften sind Inline-CSS, das die Allowlist ohnehin verwirft — das ganze **Element** zu löschen nahm Word-Tabellen mit. |
| **3** | `RichTextAnchorFields` kennt jetzt alle 13 Träger mit vollem Editor (Presse, Warnung, Lagebericht, Öffentliche Seite, FAQ, Fraktionsprofil, Fahndung, die drei Vorlagen, Beförderungsantrag). Der bestehende Stolperdraht in `RichTextHtmlInterceptorTests` hält jeden Eintrag gegen das Modell. |
| **4** | `.dokument-html, .dokument-html *` steht im Druck-Block; Kästen und Zitate bekommen zusätzlich einen hellen Grund. |
| **5** | Die Rail wird für Redakteure aus den Voll-Listen gebaut; ausgeblendete Kapitel und Artikel erscheinen mit Kennzeichen „Ausgeblendet" und behalten ihren Bearbeiten-Knopf. |
| **6** | Der Klick-Handler der Auswahl-Blase liest `getFormat(index, laenge)` — dieselbe Quelle wie die Anzeige. |
| **7** | `leseHtml` zählt `<hr>` als Inhalt. |
| **8** | Word-Listen werten `level(\d+)` als `ql-indent-N` aus, und das Marker-Zeichen entscheidet zwischen `<ol>` und `<ul>`. |
| **9** | Die `counter-reset`/`counter-increment`-Mechanik aus `quill.snow.css` ist nach `.dokument-html` gespiegelt (Ebene 1–8, dezimal/alphabetisch/römisch). |
| **10** | Ein gestreiftes Schloss je Agent umschließt Prüfung, Modellaufruf und Abbuchung. |
| **11** | Der Interceptor merkt sich den Spaltenwert **vorher und nachher** und entfernt **nach** dem Commit, auf einem eigenen Kontext, genau die Bilder, die diese Spalte vorher nannte und jetzt nicht mehr — Zeile und Datei. Ein fehlgeschlagener Speichervorgang verwirft die Notiz (`SaveChangesFailed`). Zwei neue Tests halten das fest, einer davon der Grund für den Vorher-Vergleich: ein Kommentar legt sein Bild unter der **Trägerakte** ab (`CommentPanel` reicht deren Typ/Id durch), ein Aufräumen „alles, was der Dokumenttext nicht nennt" hätte genau diese Bilder gelöscht. |
| **12** | `HtmlCleanup.Clean` wirft Überschriften-`id`s weg; nur die neue `CleanWithAnchors` behält sie, und die ruft ausschließlich der Interceptor — direkt nachdem `RichTextAnchors` sie geschrieben hat. `id` ist auch aus dem `Profile` für den Browser-Reiniger raus. |
| **13** | Der Typ-Check sitzt jetzt in `DesiredCompromises` und deckt damit Anlegen, Bearbeiten und Nachtragen ab. |
| **14** | `OnParametersSet` liest `?suche` und `?begriff` bei jeder Navigation neu, mit Guard gegen Wiederholung. |
| **15** | `Permission.RequirePromotionDecide` prüft den Schreibzugriff vor dem Rang; neue Policy `PromotionDecideWrite` an beiden Knopf-Stellen. |
| **16** | Neue Sammel-Überladung `GetAllArticlesAsync(IReadOnlyCollection<string>)`; die Kapitelschleife ist weg. |
| **17** | Aufbewahrung von 30 auf **7 Tage**, und die Abmelde-Formulare leeren die Entwurfsdatenbank (`window.nooseEntwuerfeLoeschen` in `App.razor`). |
| **18** | `flex-wrap` auf der Zeile, `max-width` auf dem Panel, mitwachsende Felder. |
| **19** | `ExpandedAsync` speichert den bisher offenen Punkt im Öffnen-Zweig und schließt im Schließen-Zweig nur noch das, was wirklich offen ist — in jeder Ereignisreihenfolge. |
| **20** | `RequireFreeNavKeyAsync` auf beiden Artikel-Schreibpfaden. |
| **21** | Die Hinweiskarte hört auf `LocationChanged` und verabschiedet sich, sobald `/neuerungen` offen ist. |
| **22** | Das Memo in `RichHtml` schlüsselt zusätzlich auf den gelesenen Präferenzwert. |
| **23** | Beide Slug-Felder speisen sich aus `HandbookService.MaxSlugLength`. |
| **24** | `LinkPanel` übergibt `_manual` **und** `_colleagues` als Ausschlussliste. |
| **25** | `?begriff` wird nach dem Auflösen aus der Adresse entfernt. |
| **26** | `Edge` unterscheidet „nichts gefunden" von „harte Grenze" über ein `hardStop`-Flag. |
| **27** | Die Leer-Prüfung läuft ein zweites Mal, nachdem die Texte geladen sind. |
| **28** | `SaveAsync`-Helfer in beiden Diensten übersetzt eine Unique-Verletzung in dieselbe deutsche Meldung. |
| **29** | `destroyRichText` führt den fälligen Schreibvorgang aus, bevor es den Timer abräumt. |
| **30** | `ValidateBadgeNumberAsync` bekommt `ownValueIsPending`; auf dem Genehmigungspfad zählt nur die **aktuell gehaltene** Nummer als eigene. |
| **31** | `NooseiAnswer` trägt jetzt `Model`; der Kurzbrief stempelt, was die Antwort gemeldet hat. |
| **32** | Der Anbieter wird einmal je Turn aufgelöst und in die Kontingent-Prüfung hineingereicht. |
| **34** | `LinkService.CreateAsync` nimmt `allowedTargetTypes` und prüft serverseitig; alle drei Panels speisen Picker und Dienst aus derselben Liste. |
| **35** | Drei Delegations-Tests für die neuen Papierkorb-Quellen, `index = 0` aus der „außerhalb"-Theorie heraus und als eigener Fall hinein, die drei fehlenden `AgentStatus`-Werte ergänzt. |
| **36** | `char.IsAsciiDigit`, Fassungs-Sortierung folgt der ausgelieferten Liste, kein Onboarding-Stempel auf einem 404, `CS8604` weg. Der Anker-Rundlauf ist durch Befund 12 mit erledigt: die Adressen werden vor dem Interceptor abgeräumt, also vergibt `ToStored` sie immer in Dokumentreihenfolge — genau so, wie `BuildToc` nummeriert. |

**Nebenbefund beim Aufräumen:** Zwei Wegwerf-Tests (`SCRATCH_ToEditor_MultiImageFigure`,
`TEMP_ToEditor_LinkedFigureProbe`) waren von Prüf-Agenten während des Reviews in
`RichTextFigureTests.cs` geschrieben worden — einer davon rot. Beide sind entfernt.

**27 Changelog-Zeilen** (`2.1.33` bis `2.1.59`) für alles, was ein Agent von außen bemerkt.

### Nachtrag: ein P0, den dieser Review übersehen hatte

**37. Einfügen aus der Zwischenablage ersetzte das ganze Dokument** — `NOOSE-Website/wwwroot/js/richtext.js:580`

`editor.clipboard.dangerouslyPasteHTML(sauber, 'user')`. Die Methode hat im vendorten Quill 1.3.7 zwei
Formen, und die mit dem **HTML als erstem Argument** ruft intern `setContents`:

```js
if ("string" == typeof t) this.quill.setContents(this.convert(t), e)
```

Das ersetzt alles, was der Editor hält. Einen Satz in einen fertigen Lagebericht einfügen löschte den
Lagebericht. Betroffen war **jedes** Einfügen von HTML (Word, Webseite, anderer Editor) in **jedem**
Textfeld der Seite. Die Stelle stammt aus `7e2a19e` und wurde nie im Browser gesehen.

**Behoben:** die Index-Form (`dangerouslyPasteHTML(index, html, source)`) an der Schreibmarke, die Auswahl
wird vorher gelöscht — so, wie sich ein Einfügen verhält. `?v=` auf **19** an beiden Importstellen.
`richtext.js:2216` bleibt unverändert: das ist der NOOSEI-Korrekturpfad, wo `index < 0` ausdrücklich
Vollersatz bedeutet.

**Warum mein Review ihn nicht fand:** der Finder auf dem Einfügepfad hat die Bereinigung gelesen
(`wandleWordListen`, Befund 2 und 8), nicht die Signatur des Aufrufs dahinter. Eine fremde
Bibliotheks-API mit zwei Formen fällt bei reiner Codelesung durch, wenn man sie nicht nachschlägt.

### Nachtrag: Rechte-Cluster in der Agentenverwaltung

Aus einem zweiten Review, von mir am Code nachgeprüft und behoben:

| # | Befund | Stand vorher |
|---|---|---|
| **38** | `AgentManagementService.ReleaseAsync` — **gar keine Rechteprüfung.** Ein Konto freigeben vergibt einen Dienstgrad bis hinauf zu Director; die einzige Schranke war die Seite. | ohne Guard |
| **39** | `RejectAsync` — **gar keine Rechteprüfung.** Sperrt ein Konto. | ohne Guard |
| **40** | `MasterDataChangeAsync` — schreibt Codename, Klarname und Dienstnummer eines **fremden** Kontos direkt durch, am Antragsweg vorbei. Hatte nur `RequireWriteAccess`, also keine Führungsprüfung. | nur Schreibrecht |
| **41** | `ReleaseAsPartnerAsync`, `PromoteApplicantToAgentAsync`, `BlockAsync` — nur Rang, kein Schreibrecht. | halber Guard |
| **42** | `Permission.RequireWriteAccess` prüfte `IsOnlyReader() \|\| IsPartner()`, `MayWrite()` zusätzlich `IsDemo()`. Die Oberfläche verbarg den Knopf, der Dienst dahinter ließ den Demo-Besucher durch — ein ganzer Schreibpfad lief, bis die Sperre beim Speichern verweigerte. | Divergenz |

**Behoben:** 38–41 tragen jetzt `RequireWriteAccess` **vor** `RequireLeadership`. 42 ist auf `!actor.MayWrite()`
umgestellt, damit Guard und Prädikat dieselbe Regel sprechen. Der Test `RequireWriteAccess_demoVisitor_passes`
hielt genau das alte Verhalten fest — ohne Begründung, nur beschreibend — und ist zu
`…_demoVisitor_throws` gedreht. **Das Demo-Seeding ist nicht betroffen:** `DemoAutoSetup.BuildActor` setzt
den Demo-Claim gar nicht, sondern Admin und Director.

### Nachtrag: Data-Protection-Schlüssel

`NOOSE-Website/App_Data/keys/key-*.xml` lagen in der Versionsverwaltung — wer das Repo hat, kann
Anmelde-Cookies fälschen. **Aus dem Index genommen und in `.gitignore` aufgenommen**, die Dateien bleiben
lokal liegen (niemand wird abgemeldet). **Das ist nur der halbe Fix:** die Schlüssel stehen weiterhin in der
git-Historie. Wirksam ist erst eine **Rotation**, und die meldet alle ab — deine Entscheidung, deshalb nicht
von mir gemacht.

### Im Browser nachgewiesen

Die App selbst konnte ich nicht starten: der `DatabaseConnectionResolver` bevorzugt `ProductionConnection`
und fällt nur zurück, wenn sie *unerreichbar* ist — ein lokaler Start hätte `MigrateAsync` und alle Seeder
gegen die **Live-Datenbank** laufen lassen. Stattdessen habe ich `richtext.js` über einen statischen Server
mit dem echten Quill-Bundle geladen und die Pfade mit synthetischen Zwischenablage-Ereignissen gefahren:

| Befund | Nachweis |
|---|---|
| **37** Einfügen | `"ErsteEINGEFUEGTr Absatz…"` — an Position 5, Ausgangstext vollständig erhalten |
| **2** Word-Raster | Zellentext da, Ausgangstext da |
| **8** Word-Listen | wird `<ol>`, Ebene 2 trägt `ql-indent-1`, Marker „1." entfernt |
| **7** Trennlinie | `getHtml` liefert `"<hr><p><br></p>"` statt leer |
| **6** Auswahl-Blase | zeigt „Fett" bei gemischter Auswahl korrekt als inaktiv; ein Klick **setzt** die Fettung (`<strong>Hallo Welt</strong>`) |
| **9** Listen-Nummern | gerendert als `1. / a. / b. / 2.` |
| **18** Suchleiste | `flex-wrap: wrap`, `max-width: calc(100% - 16px)` |

**Weiterhin nur gelesen, nicht gesehen:** die Druckfarbe (Befund 4) — dafür braucht es eine echte
Druckvorschau — sowie die Handbuch-Rail (5), die Hinweiskarte (21) und der Entwurfs-Rundlauf (17, 29),
weil die eine angemeldete Sitzung voraussetzen.

### Bewusst nicht behoben

**Befund 33 — Unique-Index auf die Dienstnummer.** Der Fix wäre eine Migration, die beim Start automatisch
läuft. Existiert in der Produktionsdatenbank auch nur **eine** doppelte Dienstnummer aus der Zeit vor der
Römisch-Regel, schlägt sie fehl, `MigrateAsync` wirft, und die Seite startet nicht mehr — genau die
Fehlerklasse, die Befund 1 gerade beseitigt hat. Der Befund selbst nennt die Lage „derzeit ungefährlich":
die Eindeutigkeit hängt am prozessweiten `BadgeNumberGate`, und es gibt architektonisch nur einen Prozess
je Datenbank (die Demo-Instanz hat ihre eigene).

Vor einem Nachziehen also erst prüfen:

```sql
SELECT Dienstnummer, COUNT(*) FROM AspNetUsers WHERE Dienstnummer IS NOT NULL GROUP BY Dienstnummer HAVING COUNT(*) > 1;
```

Kommt da nichts zurück, ist die Migration gefahrlos.

---

## Was dieser Review NICHT abgedeckt hat

Ehrlichkeitshalber, damit du weißt, wo du selbst hinschauen solltest:

- Die geplante **zweite Runde** (sieben Tiefen-Finder: `richtext.js` komplett von vorn, Handbuch-Zustands-
  übergänge, LLM-Geldpfade, Blazor-Lebenszyklus, Nebenläufigkeit, HTML-Sicherheit, Vollständigkeits-Kritiker)
  wurde **abgebrochen** und hat **kein** Ergebnis geliefert. Besonders `richtext.js` (1829 geänderte Zeilen)
  ist damit nur einmal gelesen worden.
- Vier Befunde blieben **ohne unabhängige Nachprüfung** (unten mit `[?]`).
- **Kein Laufzeittest.** Alles unten ist Codelesung plus Testlauf; nichts davon habe ich im Browser gesehen.
  Die drei CSS-Befunde und der Tagesordnungs-Befund hängen an Darstellungs- bzw. Ereignisreihenfolge,
  die man eigentlich am laufenden Programm prüfen muss.
- **Migrationen wurden nicht gegen eine Bestandsdatenbank angewandt.**

---

## P0 — Kritisch

### 1. `[ich]` Der Handbuch-Seeder kann die App beim Start unrettbar abbrechen lassen
`NOOSE-Website/Infrastructure/Handbook/HandbookSeeder.cs:62`, `:136`, `:266`

Drei Einfügepfade des Seeders schreiben einen Wert in eine **unique** indizierte Spalte, ohne vorher zu
prüfen, ob eine redaktionell angelegte Zeile ihn schon belegt:

| Stelle | schreibt | Unique-Index |
|---|---|---|
| `SeedChaptersAsync` (~Zeile 62) | `HandbookChapter.Slug` | `b.HasIndex(c => c.Slug).IsUnique()` |
| `SeedArticlesAsync` (~Zeile 136) | `HandbookArticle.Slug` | `b.HasIndex(a => a.Slug).IsUnique()` |
| `SeedTermsAsync` (~Zeile 266) | `GlossaryTerm.Term` | `b.HasIndex(t => t.Term).IsUnique()` |

Erkannt wird eine Zeile ausschließlich am `SeedKey`. Eine von Hand angelegte Zeile hat `SeedKey = null`,
ist für den Seeder also unsichtbar — belegt den Namen aber trotzdem. Dasselbe gilt für den
**Umbenennungspfad** (`row.Slug = shipped.Slug` bzw. `row.Term = shipped.Term` beim Revisions-Bump).

`Program.cs:551` ruft `HandbookSeeder.SeedAsync(db)` **ohne `try/catch`** im Startup-Block auf. Eine
`DbUpdateException` beendet damit den Start — und zwar vor `WarnhinweisSeeder`, `PublicTemplateSeeder`,
dem Enum-Label-Warmup und `DemoAutoSetup`. systemd startet den Dienst neu, er stirbt wieder.

**Ablauf:** HRB legt im Handbuch den Glossarbegriff „Kopfgeld" an (`SeedKey = null`). Ein späteres Release
liefert denselben Begriff im Erstbestand aus. Beim ersten Start nach dem Deploy fügt `SeedTermsAsync` ihn
ein → Verletzung von `IX_HandbuchBegriffe_Begriff` → **noose.info ist offline**, bis jemand die Zeile per
SQL entfernt. Der Slug-Fall trifft zusätzlich **soft-gelöschte** Kapitel und Artikel: die halten ihren Slug
weiterhin im Index (genau darauf weist `HandbookService.RequireFreeArticleSlugAsync` beim UI-Pfad selbst
hin) — der Seeder hat diese Schranke nicht.

**Richtung:** Vor jedem Insert gegen `IgnoreQueryFilters()` prüfen, ob Slug bzw. Begriff schon belegt ist,
und dann überspringen statt einfügen — so macht es `ChangelogSeeder.SeedReleasesAsync` bereits korrekt
(`if (known.Contains(r.Version, StringComparer.Ordinal)) { continue; }`). Zusätzlich den Seeding-Block in
`Program.cs` so absichern, dass ein Inhalts-Seeder den Start nicht killen kann.

---

## P1 — Tut nachweislich das Falsche

### 2. `[✓✓][ich]` Einfügen aus Word löscht Absätze und ganze Tabellen statt der Aufzählungs-Marker
`NOOSE-Website/wwwroot/js/richtext.js:512`

```js
for (const rest of Array.from(wurzel.querySelectorAll('[style*="mso-"]'))) {
    rest.remove();
}
```

Gemeint war laut Kommentar der unsichtbare Span, in dem Word das Aufzählungszeichen ablegt
(`<span style='mso-list:Ignore'>·</span>`). Der Selektor trifft aber **jedes** Element im eingefügten
Fragment, dessen `style` irgendwo `mso-` enthält — und entfernt es **samt gesamtem Inhalt**.

Word schreibt `mso-`-Eigenschaften inline auf so gut wie alles:
`<p style='...mso-pagination:widow-orphan'>`, `<table style='...mso-yfti-tbllook:1184'>`,
`<tr style='mso-yfti-irow:0'>`, `<td style='...mso-border-alt:solid windowtext .5pt'>`.

**Ablauf:** Eine Tabelle in Word markieren, kopieren, in einen NOOSE-Editor einfügen. Das `<table>`-Element
trägt `mso-yfti-tbllook` → wird entfernt → **die Tabelle ist samt Inhalt weg**, ohne jede Meldung.
Dasselbe für normale Absätze mit inline-`mso-`-Eigenschaften.

**Richtung:** Nur `span[style*="mso-list"]` entfernen (den Marker-Span) und bei allen anderen Elementen
das **Attribut** wegwerfen statt des Elements. `saeubere()` räumt die Styles danach ohnehin auf.
`?v=` auf 18 hochzählen (beide Importstellen in `RichTextEditor.razor`).

### 3. `[✓✓][ich]` Der Inhaltsverzeichnis-Knopf steht in jedem Editor, Anker bekommen aber nur acht Träger
`NOOSE-Website/Services/RichTextAnchorFields.cs:16` · `NOOSE-Website/wwwroot/js/richtext.js:2032`

Der Fix des alten Befunds 4/5 hat `RichTextAnchorFields` nur um `HandbookArticle` und `GlossaryTerm`
erweitert. Anker vergeben werden damit für genau acht Träger (sechs aus `RichTextImageFields` + diese zwei).
Der Toolbar-Knopf `noose-toc` und der Schrägstrich-Befehl `/inhaltsverzeichnis` erscheinen dagegen
weiterhin in **jedem** nicht-kompakten Editor (`if (!kompakt) { ... toolbarGruppen.push([... 'noose-toc']) }`).

`OnTocRequested` baut über `RichTextAnchors.BuildToc` Links der Form `<a href="#slug">`. Beim Speichern
läuft `RichTextAnchors.ToStored` aber nur, wenn der Typ in `RichTextAnchorFields` steht — sonst tragen die
Überschriften **keine `id`**, und jeder Eintrag des eingefügten Verzeichnisses zeigt ins Leere.

**Betroffen (nachgezählt):** Pressemitteilung, Öffentliche Warnung, Lagebericht, Öffentliche Seite,
FAQ-Eintrag, Fraktions-Öffentlichkeitsprofil, Öffentliche Fahndung, die drei Vorlagen-Editoren
(Dokument/Aktivität/Personal), die Bewerbungs-Vorlage und der Beförderungsantrag — also **mehr Editorstellen
als die, in denen es funktioniert.**

**Richtung:** Entweder `RichTextAnchorFields.Extra` um diese Typen erweitern (ein Anker ist nur eine `id`,
die Bild-Argumente aus `RichTextImageFields` gelten hier nicht), **oder** dem `RichTextEditor` einen
`AnchorsEnabled`-Parameter geben und den Knopf nur dort einblenden. Ein Stolperdraht-Test, der beide Listen
gegeneinander hält, fehlt in beiden Fällen.

### 4. `[✓]` Gedruckter Fließtext ist im Dark-Theme praktisch unsichtbar
`NOOSE-Website/wwwroot/app.css:558` und `:863`

`.dokument-html { color: var(--mud-palette-text-primary); }` — im einzigen ausgelieferten Theme ist das
nahezu Weiß. Der `@media print`-Block setzt `color:#000 !important` auf `html, body, .mud-layout,
.mud-main-content, .mud-paper` sowie auf `.mud-typography, .mud-text-secondary, .mud-link, .mud-chip,
.mud-alert, .mud-table, .mud-table *`. **`.dokument-html` steht in keiner dieser Listen**, und eine eigene
`color`-Deklaration am Element schlägt jede geerbte — auch eine mit `!important` am Vorfahren.

**Ablauf:** `/dokumente/{id}`, `/handbuch/{slug}`, eine Besprechung oder eine Personalnotiz über Strg+P
drucken. Rahmen, Überschriften und MudBlazor-Text kommen schwarz, **der eigentliche Fließtext bleibt weiß
auf Weiß**.

**Nicht neu:** Die `color`-Zeile stammt nicht aus diesem Änderungssatz. Sie fällt jetzt aber stärker auf,
weil `/handbuch/{slug}` und die neuen Struktur-Bausteine gedruckt werden sollen.

**Richtung:** `.dokument-html, .dokument-html *` in die Farbliste des Druck-Blocks aufnehmen.

### 5. `[✓]` Ein ausgeblendetes Handbuch-Kapitel oder ein ausgeblendeter Artikel ist nicht mehr erreichbar
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:93` (alter Befund 11, die zweite Hälfte)

Die Rail iteriert über `_chapters`, und `_chapters` kommt ausschließlich aus
`HandbookService.GetChaptersAsync()` — das hart auf `c.IsVisible` filtert und nur sichtbare Artikel
sichtbarer Kapitel liefert. Für Glossarbegriffe wurde das behoben (`GetAllTermsAsync`, ausgeblendete werden
markiert angezeigt); für Kapitel und Artikel nicht.

**Ablauf:** HRB schaltet im Editor-Dialog „Sichtbar" aus und speichert. Beim nächsten Laden von `/handbuch`
fehlt die Zeile komplett — es gibt keinen Bearbeiten-Knopf mehr, keinen Papierkorb (die Zeile ist ja nicht
gelöscht) und keinen Weg zurück außer über die Datenbank. Der Slug bleibt dabei belegt, ein Neuanlegen unter
demselben Namen scheitert also ebenfalls.

**Richtung:** Für Redakteure (`_mayEdit`) die Rail aus `_allChapters`/`_allArticles` speisen und
ausgeblendete markiert anzeigen — genau so, wie es der Glossar-Fix schon macht.

---

## P2 — Randfall, Last, stiller Teilausfall

### 6. `[✓✓]` Die Auswahl-Blase schaltet Formatierung im falschen Indexraum — bis hin zur Gegenrichtung
`NOOSE-Website/wwwroot/js/richtext.js:1918`

`aktualisieren()` liest den Aktiv-Zustand korrekt über die **ganze** Auswahl
(`editor.getFormat(bereich.index, bereich.length)`; Quill liefert bei gemischter Formatierung `false`).
Der Klick-Handler entscheidet den neuen Wert dagegen aus dem Format **am Anfang** der Auswahl.

**Ablauf:** Text „**Hallo** Welt", das erste Wort fett. Alles markieren — die Blase zeigt „Fett" korrekt
als *nicht* aktiv. Ein Klick auf „Fett" sollte alles fett machen; weil der Anfang aber schon fett ist, wird
`false` gesetzt und **die vorhandene Fettung entfernt**.

**Richtung:** Im Klick-Handler `editor.getFormat(zustand.index, zustand.laenge)[format]` verwenden —
dieselbe Quelle wie die Anzeige.

### 7. `[✓✓]` Ein Dokument, das nur aus einer Trennlinie besteht, wird beim Speichern als leer behandelt
`NOOSE-Website/wwwroot/js/richtext.js:2211`

`leseHtml` liefert `''`, wenn weder Text noch Tabelle, Bild oder Erwähnung vorhanden ist. Die in diesem
Änderungssatz neu eingeführte `trenner`-Block-Embed (`<hr>`) trägt keinen Text und steht in keiner der drei
Ausnahmen.

**Ablauf:** In einem beliebigen Feld nur `/trenner` einfügen und speichern → gespeichert wird ein leerer
String, die Trennlinie ist weg. Trifft auch den Fall „Bild plus Trennlinie, Bild wieder gelöscht".

**Richtung:** `editor.root.querySelector('hr')` (allgemeiner: jedes textlose Block-Embed) in die
Leer-Prüfung aufnehmen, analog zu Tabelle, Bild und Erwähnung.

### 8. `[✓✓][ich]` Word-Listen werden beim Einfügen immer zu flachen, unnummerierten Aufzählungen
`NOOSE-Website/wwwroot/js/richtext.js:491`

`wandleWordListen()` erkennt eine Word-Liste nur über `/mso-list/i` und baut ausnahmslos ein `<ul>`
(`createElement('ul')`). Weder die Ebene (Word kodiert sie als `mso-list:l0 level2 lfo1`) noch der Typ
(nummeriert gegenüber Aufzählung) werden ausgewertet.

**Ablauf:** Eine zweistufige nummerierte Liste in Word kopieren und einfügen → eine flache Bullet-Liste ohne
Nummern und ohne Einzug.

**Richtung:** `level(\d+)` aus dem `mso-list`-Wert ziehen und als `ql-indent-N` setzen; am Marker-Span
(bzw. am `mso-list`-Wert) zwischen `<ul>` und `<ol>` unterscheiden.

### 9. `[✓][ich]` Verschachtelte nummerierte Listen sind in Lese- und Druckansicht falsch nummeriert
`NOOSE-Website/wwwroot/app.css:773`

Die neuen Regeln `.dokument-html .ql-indent-1` … `.ql-indent-8` übernehmen aus Quill nur das
`padding-left`, nicht die `counter-reset`/`counter-increment`-Mechanik. Im ganzen `app.css` kommt **kein
einziges** `counter-reset` oder `counter-increment` vor (nachgezählt).

Quill 1.x rendert eine eingerückte Liste **nicht** als verschachteltes `<ol>`, sondern als ein einziges
flaches `<ol>` mit `ql-indent-N`-Klassen. Ohne Counter zählt der Browser also einfach durch.

**Ablauf:** Im Editor „1. Erstens / a. Unterpunkt / b. Unterpunkt / 2. Zweitens" schreiben. Der Editor zeigt
es korrekt (er lädt `quill.snow.css`). Die Leseansicht (`RichHtml` → `.dokument-html`) und jeder Ausdruck
zeigen **1. 2. 3. 4.**

**Richtung:** Die Counter-Regeln aus `quill.snow.css` nach `.dokument-html` spiegeln — es ist die dritte der
drei Stellen, die CLAUDE.md für ein neues Blockformat nennt (Toolbar, Sanitizer, Lese-CSS).

### 10. `[✓✓]` Kontingent-Prüfung und Abbuchung sind nicht atomar
`NOOSE-Website/Services/Llm/NooseiGateway.cs:91`

`AskAsync` ruft zuerst `quota.EnsureAvailableAsync` und erst **nach** dem kompletten, mehrere Sekunden
dauernden LLM-Aufruf `quota.TryChargeAsync`. Dazwischen liegt keine Sperre und keine Transaktion.

**Ablauf:** Ein Agent mit 5.000 Token Rest stellt in zwei Browser-Tabs fast gleichzeitig je eine
4.000-Token-Frage. Beide sehen „Rest > 0" und laufen durch; abgebucht werden 8.000. Mit der Zahl der Tabs
skaliert das linear — und es geht um echtes Geld beim KI-Eigner.

**Richtung:** Eine reservierende Buchung vor dem Aufruf (mit Korrektur nach der Antwort), oder mindestens
ein `SemaphoreSlim` je Agent um Prüfung, Aufruf und Buchung — wie es `NavPreferencesService` mit seinem
gestreiften Schloss bereits vormacht.

### 11. `[✓✓]` Textbild-Dateien und -Zeilen werden nie aufgeräumt
`NOOSE-Website/Infrastructure/RichTextHtmlInterceptor.cs:171`

Es gibt im gesamten Code **keinen einzigen** Aufruf von `ITextImageStorageService.Delete(...)`. Weder
`TextImageService.SaveAsync` (Direkt-Paste) noch `RichTextHtmlInterceptor.StoreFileAsync` (Base64-Hebung)
entfernen die vorherige Datei, wenn ein Bild im selben Feld ersetzt oder gelöscht wird.

**Ablauf:** Bild in ein Dokument einfügen, speichern (Zeile A + Datei A). Bild löschen, neues einfügen,
erneut speichern (Zeile B + Datei B). A bleibt für immer in `App_Data/uploads/textbilder` und in der Tabelle
`Textbilder` liegen — unerreichbar, aber vorhanden, und bei jedem Deploy mitkopiert. Dasselbe beim Löschen
der Trägerakte.

**Richtung:** Im Interceptor die nicht mehr referenzierten `TextImage`-Zeilen des Feldes ermitteln und samt
Datei löschen, oder einen periodischen Aufräum-Worker (Muster: `ThreatScoreSweepWorker`) einführen.
Kein Datenverlust — aber unbegrenztes Wachstum, und die Dateien überleben das Löschen der Akte.

### 12. `[✓]` Die Sanitizer-Ausnahme für `id` gilt für jedes Rich-Text-Feld, nicht nur für Anker-Träger
`NOOSE-Website/Services/HtmlCleanup.cs:162`

Der `RemovingAttribute`-Hook behält `id` auf `h1`–`h6` allein anhand des Tag-Namens — unabhängig davon, zu
welcher Entität oder Spalte das HTML gehört. `RichTextAnchors.ToStored` normalisiert Anker dagegen nur für
die acht registrierten Träger. Jedes andere Rich-Text-Feld (Kommentar, Ticket-Nachricht, Bewerberchat,
öffentliche Seite) darf damit einen **frei gewählten, seitenweit gültigen Anker-Namen** speichern.

Die Kommentare im Code behaupten ausdrücklich das Gegenteil: „an id is a document-wide name — in a comment,
a ticket or a public page an author could otherwise shadow an id the page itself uses".

**Ablauf:** In ein Feld ohne Anker-Registrierung `<h2 id="login-formular">…</h2>` einfügen und speichern.
Ein `href="#login-formular"` derselben Seite landet danach auf dieser Überschrift. Kein XSS, aber
DOM-Clobbering und kaputte In-Page-Links bzw. `aria`-Bezüge.

**Nebenbefund `[ich]`:** Der Hook nennt `H4`/`H5`/`H6`, die gar nicht in `AllowedTagNames` stehen —
toter Zweig.

**Richtung:** Die Ausnahme trägerabhängig machen (Feldname bzw. Typ in den Cleaner reichen) oder `id` erst
gar nicht im `Profile` an den Browser-Paste-Reiniger geben.

### 13. `[✓][ich]` Der Typ-Filter der Kompromittierungen fehlt auf zwei von drei Schreibpfaden
`NOOSE-Website/Services/AbductionService.cs:146` und `:186` (alter Befund 9, nur zur Hälfte behoben)

`AddCompromiseAsync` (Zeile 258) hat den Fix bekommen:
`if (!LinkService.KnownTypes.Contains(targetType, StringComparer.Ordinal)) throw …` — mit dem Kommentar
„filtering the picker is not enough, the socket takes whatever it is sent". Dieselbe Tabelle
`AbductionCompromises` wird aber auch von `CreateAsync` (Zeile 146) und `UpdateAsync` (Zeile 186) über
`DesiredCompromises(input)` befüllt — **beide ohne diesen Check**. Nachgezählt: der Diff dieser Datei
umfasst genau die sechs Zeilen in `AddCompromiseAsync`.

**Ablauf:** Über den SignalR-Kanal — exakt das Bedrohungsmodell, das der eigene Kommentar benennt — eine
Entführung mit einem `TargetType` speichern, den `LinkService.KnownTypes` nicht kennt. Die Zeile wird
geschrieben und danach bei jedem Laden als „gelöschte Akte" angezeigt.

**Richtung:** Den Check nach `Validate(input)` bzw. in `DesiredCompromises` ziehen, damit alle drei Pfade
ihn tragen.

### 14. `[✓]` Handbuch-Suchfeld und `?begriff=` reagieren nach einer Navigation auf dieselbe Route nicht mehr
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:175`

`_search` (aus `?suche=`) und die Auflösung von `?begriff=<Id>` (`OpenTermFromQuery()`) laufen **nur** in
`OnInitializedAsync`. `/handbuch` hat keine Routen-Parameter — Blazor recycelt die Instanz bei einer
Navigation auf dieselbe Route mit anderem Query-String und ruft nur `OnParametersSetAsync` auf.

CLAUDE.md schreibt genau diese Regel für `*Editor.razor` vor („Laden gehört deshalb in
`OnParametersSetAsync` mit `_loadedId`-Guard, nie in `OnInitializedAsync`"); hier gilt sie aus demselben
Grund, nur getrieben vom Query-Parameter statt von der Route.

**Ablauf:** Auf `/handbuch` stehen und per Strg+K einen Glossartreffer öffnen (`/handbuch?begriff=<Id>`) —
es passiert **nichts**. Der Begriff wird nicht geöffnet, der alte Filterzustand bleibt stehen.

**Richtung:** `OnParametersSetAsync` mit einem Guard auf den zuletzt verarbeiteten Query-Stand ergänzen.

### 15. `[✓]` Beförderungen genehmigen und ablehnen prüft nur den Rang, nicht das Schreibrecht
`NOOSE-Website/Services/AgentManagementService.cs:559`

`PromotionDecideAsync` ruft `Permission.RequirePromotionDecide(actor)` = `IsAdmin() || Rang ≥ DeputyDirector`
— **ohne** `MayWrite()`. Die Knöpfe in `PromotionPanel.razor:53` hängen an
`<AuthorizeView Policy="@Policies.PromotionDecide">`, das ebenfalls nur den Rang prüft.

**Ablauf:** Der Demo-Besucher (trägt Director) oder eine Nur-Lese-Aufsicht mit Rang ≥ Deputy Director öffnet
einen offenen Antrag, sieht „Genehmigen"/„Ablehnen", bestätigt — und bekommt erst vom
`ReadOnlyBarrierInterceptor` beim `SaveChanges` eine Absage. Der Rang wurde im Speicher schon gesetzt, der
Audit-Eintrag schon angehängt.

**Nicht neu:** Die Methode selbst ist in diesem Änderungssatz nicht angefasst worden. Sie steht hier, weil
dieser Änderungssatz mit `RequireHrbOrLeadershipWrite` und `RequireChangelogWrite` genau das Gegenmuster
eingeführt hat — die Beförderungsseite ist jetzt die Ausnahme.

### 16. `[✓✓]` `/handbuch` lädt für jeden Redakteur alle Artikelrümpfe einzeln nach
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:206` · `Services/Handbook/HandbookService.cs:219`
(alter Befund 18, nur zur Hälfte behoben)

Umgesetzt wurde nur die erste Hälfte („nur noch für Redakteure"). Die zweite („die Kapitelschleife durch
eine `WHERE ChapterId IN (…)`-Abfrage ersetzen, wie `GetChaptersAsync` es macht") fehlt:

```csharp
_allChapters = await HandbookService.GetAllChaptersAsync();
foreach (var c in _allChapters)
{
    _allArticles.AddRange(await HandbookService.GetAllArticlesAsync(c.Id));
}
```

`GetAllArticlesAsync` projiziert **nicht** — es lädt die volle Entität samt `ContentHtml`/`RoleplayHtml`
(`longtext`). Bei 7 Kapiteln und 81 Artikeln sind das 8 Rundreisen mit allen Rümpfen, durch Prerendering
verdoppelt, bei **jedem** Besuch und nach **jeder** Redaktionsaktion (`LoadAsync` läuft in `GuardAsync`
erneut).

`GetChaptersAsync` macht es zwei Methoden weiter oben richtig — mit einem Kommentar, der genau diese Kosten
benennt: „reading eighty longtext columns to render a list of titles".

**Richtung:** Eine Sammel-Überladung `GetAllArticlesAsync(IEnumerable<string> chapterIds)` ergänzen und
`Handbook.razor` darauf umstellen.

### 17. `[?]` Entwürfe bleiben nach dem Abmelden 30 Tage unverschlüsselt im Browser
`NOOSE-Website/wwwroot/js/richtext.js:1237` (alter Befund 23, weiterhin offen)

`ENTWURF_ALTER_TAGE = 30`, Schlüssel = Agent-Id + `DraftKey`, Inhalt = das volle HTML im Klartext in der
IndexedDB `noose-rte`. Gelöscht wird nur durch `MarkSavedAsync`/`DiscardDraftAsync`, den „Verwerfen"-Klick
oder den Sweep, der erst beim nächsten Öffnen eines Editors läuft. Der Abmelde-Pfad rührt die Datenbank
nicht an.

**Ablauf:** Agent A tippt an einer gemeinsam genutzten Station in einem VS-Dokument oder einer
Personalakten-Notiz, schließt den Tab ohne zu speichern und meldet sich ab. Agent B öffnet dort ein
beliebiges Feld — der Sweep läuft, löscht aber nichts, weil der Eintrag keine 30 Tage alt ist. Die Daten
liegen im Browserprofil und sind über die Entwicklerkonsole lesbar.

**Richtung:** Ein exportiertes `entwuerfeLoeschen()` (`indexedDB.deleteDatabase`) im Abmelde-Pfad auslösen;
Aufbewahrung deutlich senken.

### 18. `[✓]` Die Suchen/Ersetzen-Leiste des Editors läuft auf schmalen Fenstern über den Rand
`NOOSE-Website/wwwroot/app.css:355`

`.noose-suchen` ist `position:absolute; right:8px` ohne `max-width` und ohne `overflow`;
`.noose-suchen-zeile` ist ein `display:flex` **ohne `flex-wrap`** und enthält Suchfeld, Ersetzen-Feld und
Trefferzähler nebeneinander — zusammen rund 350–370 px.

**Ablauf:** Editor auf einem schmalen Fenster (unter 400 px, Handy oder Splitscreen) öffnen und Strg+F
drücken. Die Leiste ragt über den rechten Rand; die Ersetzen-Felder sind nicht mehr erreichbar.

**Richtung:** `flex-wrap: wrap` auf `.noose-suchen-zeile` und `max-width: calc(100% - 16px)`
auf `.noose-suchen`.

### 19. `[?]` Der Wechsel des offenen Tagesordnungspunkts kann eine ungespeicherte Notiz verlieren
`NOOSE-Website/Components/Pages/Meetings/Shared/MeetingAgendaPanel.razor:162`

`ExpandedAsync(itemId, false)` speichert zuerst (`NoteSaveIfDirtyAsync`) und setzt dann `_expandedId = null`
— beides **ohne zu prüfen, ob `_expandedId` überhaupt noch auf `itemId` zeigt**. `NoteSaveIfDirtyAsync`
steigt seinerseits sofort aus, wenn `_expandedId != itemId`.

Feuert MudBlazor beim direkten Wechsel von Punkt A zu Punkt B das Öffnen von B **vor** dem Schließen von A,
dann passiert beides: die Notiz von A wird nicht gespeichert (`_expandedId` ist schon B), und
`_expandedId = null` schließt den gerade geöffneten Punkt B wieder.

**Nicht nachgeprüft:** Welche Reihenfolge MudBlazor 9 tatsächlich verwendet, konnte ich am Code allein nicht
entscheiden — mein Lesen von `NotifyPanelClickedAsync` spricht eher für „erst schließen". Der Code ist gegen
die ungünstige Reihenfolge aber **nicht abgesichert**, und `PublicFaqPanel.EntryToggledAsync` macht es an
derselben Stelle defensiv richtig.

**Richtung:** Im `open == true`-Zweig den bisherigen `_expandedId` sichern und **darauf**
`NoteSaveIfDirtyAsync` aufrufen, bevor `_expandedId` neu gesetzt wird; im `false`-Zweig
`if (_expandedId != itemId) return;` voranstellen.

---

## P3 — Inkonsistenz, Kosmetik mit Funktionsbezug, Doku

### 20. `[✓✓]` `NavKey` eines Handbuch-Artikels wird nicht auf Eindeutigkeit geprüft
`NOOSE-Website/Services/Handbook/HandbookService.cs:322` und `:355`

`CreateArticleAsync`/`RefreshArticleAsync` übernehmen den `NavKey` ungeprüft; der Index in `AppDbContext.cs`
ist **nicht** unique. `LoadArticleForNavKeyAsync` nimmt bei zwei Ansprüchen den mit der kleineren
`SortOrder` — der „?"-Knopf zeigt dann still auf den falschen Artikel. Der Erstbestand hat dafür einen
Stolperdraht (`Every_shipped_nav_key_is_claimed_only_once`), der Redaktionspfad nicht.

### 21. `[✓✓]` Die Neuerungs-Hinweiskarte bleibt stehen, wenn man `/neuerungen` über das Menü öffnet
`NOOSE-Website/Components/Pages/Changelog/Shared/ChangelogHintCard.razor:60`

Die Karte hängt in `MainLayout.razor:103` und lädt `_flash` nur in `OnInitializedAsync`. Bei Blazor Server
lebt das Layout über den ganzen Circuit. `Changelog.razor` stempelt zwar `NeuerungenLastSeenUtc`, aber über
`SetNeuerungenLastSeenAsync(..., notify: false)` — die Karte erfährt nichts und zeigt bis zum nächsten
Vollreload weiter „2 Neuerungen seit deinem letzten Besuch".

### 22. `[✓]` Der Schalter „Fachwörter erklären" wirkt erst nach einem Seitenwechsel
`NOOSE-Website/Components/Common/Shared/RichHtml.razor:34` (alter Befund 40, weiterhin offen)

Das Memo schlüsselt auf `Html`, `Plain` und `Glossary` — nicht auf den Präferenzwert, den `BubblesOnAsync`
liest. Wird der Schalter im `NavCustomizeDialog` umgelegt (kein Routenwechsel), bleibt jede schon
gerenderte Stelle unverändert.

### 23. `[✓]` Das Kapitel-Slug-Feld erlaubt 80 Zeichen, gekürzt wird bei 64
`NOOSE-Website/Components/Pages/Handbook/Shared/HandbookChapterDialog.razor:9`

Der Artikel-Dialog steht auf 64, der Kapitel-Dialog noch auf 80. `HandbookService.Slug()` kürzt beim
Schreiben still auf `MaxSlugLength = 64` — der Redakteur sieht danach eine andere Adresse, als er eingetippt
hat. Beide Felder sollten aus derselben Konstante gespeist werden.

### 24. `[✓]` `LinkPanel` schlägt in der Personenakte weiterhin bereits Verknüpftes vor
`NOOSE-Website/Components/Common/Shared/LinkPanel.razor:233` (alter Befund 33, weiterhin offen)

`LinkDialog.Exclusions(selfType: nameof(Person), selfId: PersonId)` — ohne die Liste der schon bestehenden
Verknüpfungen. Die drei Schwester-Panels (`OrgRelationsPanel`, `OperationInvolvedPanel`,
`TaskforceRelationsPanel`) übergeben sie alle (nachgezählt). Ergebnis: Dublette anbieten, rote Meldung
kassieren.

### 25. `[✓]` `?begriff=` bleibt nach dem Öffnen dauerhaft in der Adresse stehen
`NOOSE-Website/Components/Pages/Handbook/Handbook.razor:225` (alter Befund 39, weiterhin offen)

Der Parameter wird gelesen, aber nie entfernt. Ein Neuladen oder ein geteilter Link öffnet den Begriff
erneut — auch wenn der Nutzer inzwischen nach etwas anderem filtert.

### 26. `[✓✓]` `GlossaryHtml.Edge` verschluckt eine fällige Erklärblase an einem Bild oder `<br>`
`NOOSE-Website/Services/Handbook/GlossaryHtml.cs:200`

`Neighbour()` behandelt ein nicht-inline Geschwisterelement korrekt als Wortende (`return null`). `Edge()`,
das **innerhalb** eines Inline-Elements nach dem ersten bzw. letzten sichtbaren Zeichen sucht, tut das nicht
symmetrisch: ein `<img>` oder `<br>` als Kind wird übersprungen statt als harte Grenze gewertet.
`Fahndung<b><img …>sliste</b>` verliert dadurch die Blase an „Fahndung".
Das ist die **konservative** Richtung (eine fehlende statt einer falschen Erklärung) — deshalb P3.

### 27. `[✓✓]` `schlage_nach` kann eine leere statt der ehrlichen „nichts gefunden"-Antwort liefern
`NOOSE-Website/Services/Llm/Tools/HandbookLookupTool.cs:87`

Der Leer-Treffer-Schutz prüft nur den Zustand direkt nach dem Score-Durchlauf. Wird ein Artikel zwischen
Score-Pass und Textabruf ausgeblendet, fallen alle Kandidaten weg und das Werkzeug liefert eine leere
Antwort statt „Dazu steht nichts im Handbuch".
**Richtung:** Die Leer-Prüfung vor das finale `return` ziehen.

### 28. `[✓✓]` / `[✓]` Kollisionen beim Anlegen zeigen rohe Datenbankfehler statt der deutschen Meldung
`NOOSE-Website/Services/Changelog/ChangelogService.cs:129` · `Services/Handbook/HandbookService.cs:502`

Fassungsnummer, Kapitel-Slug, Artikel-Slug und Glossarbegriff werden per `AnyAsync` geprüft und danach in
einem separaten `SaveChangesAsync` geschrieben. Bei einer echten Kollision (zwei Redakteure gleichzeitig)
gewinnt der Unique-Index, und der zweite sieht eine `DbUpdateException` statt „Die Fassung 2.2.00 gibt es
bereits". **Richtung:** Die Unique-Verletzung abfangen und in dieselbe `InvalidOperationException` übersetzen.

### 29. `[✓]` Der Entwurfs-Timer wird beim Wegnavigieren ersatzlos abgeräumt
`NOOSE-Website/wwwroot/js/richtext.js:2301`

`destroyRichText` setzt `entwurf.tot = true` und ruft `clearTimeout` — ohne den fälligen Schreibvorgang noch
auszuführen. Wer innerhalb der 800 ms Debounce-Zeit nach dem letzten Tastendruck abbricht oder wegnavigiert,
verliert genau den letzten Absatz, den die Entwurfsfunktion retten sollte.

### 30. `[✓]` Die zweite Eindeutigkeitsprüfung beim Genehmigen einer Namensänderung ist wirkungslos
`NOOSE-Website/Services/AgentManagementService.cs:287`

`ValidateBadgeNumberAsync(agent, agent.PendingBadgeNumber, allowLegacyValue: true)` — innerhalb der Methode
wird `isLegacyValue` unter anderem über `Normalize(agent.PendingBadgeNumber) == badgeNumber` bestimmt, was
hier per Definition immer wahr ist. Die Methode steigt damit vor der Konflikt-Abfrage aus. Kein aktueller
Schaden (das prozessweite `BadgeNumberGate` und die Prüfung beim Antrag fangen es ab), aber das
Sicherheitsnetz ist tot und liest sich wie ein funktionierendes.

### 31. `[?]` `DossierSummaryService` protokolliert bei einem Anbieterwechsel das falsche Modell
`NOOSE-Website/Services/Llm/DossierSummaryService.cs:119`

`NooseiGateway.AskAsync` löst den Anbieter einmal je Turn auf und zieht ihn sauber durch. Der
Kurzbrief-Dienst liest ihn danach ein **zweites** Mal für das Anzeige-Label. Unter der 10-Sekunden-Cache-Race
steht dort ein anderer Name als der, mit dem tatsächlich gerechnet wurde. Reines Anzeigefeld — die
Abrechnung läuft korrekt über das Anfrageprotokoll.

### 32. `[?]` Kontingent-Boost und tatsächlich genutzter Anbieter werden zweimal unabhängig aufgelöst
`NOOSE-Website/Services/Llm/NooseiGateway.cs:91`

`EnsureAvailableAsync` liest `providerService.GetStateAsync()` intern selbst, `AskAsync` gleich danach noch
einmal. Fällt der 10-Sekunden-Cache dazwischen, prüft die Freigabe gegen einen anderen Boost, als die Anfrage
dann wirklich verbraucht. **Richtung:** Einmal auflösen und das Ergebnis durchreichen.

### 33. `[✓]` Dienstnummer-Eindeutigkeit gibt es nur im Anwendungscode
`NOOSE-Website/Data/AppDbContext.cs:284` (alter Befund 38, weiterhin offen)

`BadgeNumber` hat keinen Unique-Index. Die Eindeutigkeit hängt allein am statischen `BadgeNumberGate` und
gilt damit nur innerhalb **eines** Prozesses. Die Demo-Instanz läuft als zweiter Dienst — gegen eine andere
Datenbank, also derzeit ungefährlich; ein zweiter Prozess gegen dieselbe Datenbank wäre es nicht.
Braucht eine Migration.

### 34. `[✓]` Die `AllowedTypes`-Einschränkung des `LinkDialog` wirkt nur im Picker
`NOOSE-Website/Components/Common/Shared/LinkDialog.razor:434`

`OrgRelationsPanel`, `TaskforceRelationsPanel` und `OperationInvolvedPanel` schränken auf
{Person, Faction, PersonGroup, Party} ein — clientseitig. `LinkService.CreateAsync` prüft nur gegen
`KnownTypes`. Über den Circuit ließe sich damit z. B. ein Bürgerhinweis an eine Taskforce hängen.
Gleiches Muster wie Befund 13, aber ohne erkennbaren Folgeschaden.

### 35. `[✓]` Drei Lücken in den neuen Tests

- `NOOSE-Website.Tests/Services/TrashServiceTests.cs:19` — die drei neuen Papierkorb-Quellen
  (`neuerungen`, `handbuch-kapitel`, `handbuch-artikel`) haben keinen Delegations-Test. Ein Vertauschen von
  `RestoreChapterAsync` und `RestoreArticleAsync` in den zwei fast identischen, direkt benachbarten
  `Source(...)`-Zeilen fiele keinem Test auf.
- `GlossaryHtmlTests.cs:267` — `An_index_outside_the_text_answers_null` führt `0` als „außerhalb" mit,
  obwohl das die erste gültige Position im Text ist. Der Fall besteht nur zufällig.
- `PermissionTests.cs:722` — die Theorie zu `RequireCitizenSubmission` lässt genau die überraschenden
  Zustände `Pending`, `Blocked` und `Terminated` aus.

### 36. `[ich]` Kleinigkeiten, die ich beim Mitlesen gefunden habe

- `HandbookService.Slug()` benutzt `char.IsDigit` statt `char.IsAsciiDigit` — arabisch-indische oder
  Devanagari-Ziffern überleben damit in einen Slug, der laut eigenem Kommentar gerade nicht
  prozent-kodiert in Links stehen soll.
- `RichTextAnchors.ToStored` reserviert vorhandene eigene Anker **vor** der Vergabe, `BuildToc` nummeriert
  dagegen streng in Dokumentreihenfolge. Stehen zwei gleichlautende Überschriften im Dokument und trägt die
  **spätere** bereits einen Anker, zeigen die beiden Verzeichnis-Links vertauscht. Nur vor dem ersten
  Speichern erreichbar.
- `ChangelogSeeder.SeedReleasesAsync` vergibt `SortOrder = i` nur beim Einfügen. Eine später **mitten** in
  `ChangelogContent.Releases` eingefügte Fassung bekommt damit dieselbe `SortOrder` wie die, die sie
  verdrängt — relevant nur, wenn beide dasselbe Datum tragen. Für Artikel und Kapitel wurde genau dieser
  Fall bereits behoben, für Fassungen nicht.
- `HandbookArticlePage.StampAsync` setzt den Onboarding-Schritt „Handbuch geöffnet" auch dann, wenn der
  Slug gar nicht existiert (404-Zweig).
- `RichTextHtmlInterceptor` schreibt die Bilddatei, bevor das `SaveChanges` committet ist. Schlägt das
  Speichern danach fehl, bleibt eine verwaiste Datei liegen (verstärkt Befund 11).

---

## Sieht aus wie ein Fehler, ist keiner

Diese 18 Befunde wurden erhoben und danach **widerlegt**. Sie stehen hier, damit sie nicht ein zweites Mal
geprüft werden.

| Behauptung | Warum es keiner ist |
|---|---|
| **P0** Demo-Besucher schreibt dauerhaft geteilten Zustand über Nav-Präferenzen | `ExecuteUpdateAsync` umgeht den `ReadOnlyBarrierInterceptor` hier **bewusst** und im Code so kommentiert („pure UI prefs"). Kein Rechteloch, kein Aktenmaterial. Einziger Rest: auf der Demo-Instanz teilen sich alle Besucher ein Konto, also auch die Liste „zuletzt besucht" — dort steht ausschließlich Demo-Material. *(von mir widerlegt)* |
| Besprechungsprotokoll-Bild umgeht die 2h-Sperre von `MeetingVisibility` | Der Bildabruf geht über `Visibility.IsRecordVisibleAsync`, das die Sperre mitprüft. |
| `RichTextFigure.ToEditor` verliert alle Bilder außer dem ersten | `ToStored` faltet seit dem letzten Fix nur eine Zeile mit **genau einem** Bild — der Mehrbild-Fall entsteht gar nicht erst. |
| `RichTextFigure.ToEditor` entfernt den Link um ein Bild | Derselbe Grund: eine verlinkte Bildzeile wird nicht gefaltet. |
| `RichTextAnchors.ToStored` entdoppelt gleichlautende Überschriften-Ids nicht | Eine eingefügte `id` überlebt den Sanitizer auf einem Anker-Träger nicht — der behauptete Weg ist nicht erreichbar. |
| Changelog-Fassungskopf nach dem ersten Seed unkorrigierbar (alter Befund 32) | Der Seeder ist rein additiv, aber `RefreshReleaseAsync` erlaubt Datum und Titel im Redaktionspanel zu ändern. Kein Sackgassen-Fall. |
| `MarkOnboardingStepAsync` schreibt bei jedem Aufruf den ganzen Blob neu | Stimmt, ist aber ein winziger, idempotenter Schreibvorgang hinter einem Schloss — genau so entworfen und im Code begründet. |
| Abgeschlossene Wochen frieren das ungeboostete Grundkontingent ein | Bewusst konservativ und dokumentiert: kann nur zu wenig gewähren, nie zu viel. Exakt wäre eine Boost-Spalte auf der Periodenzeile (Migration). |
| `GetGlossaryMatcherAsync` cached die `CancellationToken` des ersten Aufrufers | Ein Fehlschlag entfernt den Schlüssel wieder, `RichHtml` fängt breit ab. Selbstheilend. |
| Check-then-Insert bei Slug und Begriff ist nicht race-sicher | Der Unique-Index schützt. Bleibt als Befund 28 in Form der unschönen Fehlermeldung. |
| `GlossaryTermInput.ArticleId` wird nicht auf Existenz geprüft | Die Id kommt ausschließlich aus dem Picker, und ein toter Link fällt in `LoadTermsAsync` einfach weg. |
| Kapitel-Sortierung ohne Lücken-Puffer | Der Seeder zieht die Reihenfolge ohnehin bei jedem Start nach. |
| `WantedHub`: zwei konkurrierende `HeadContent`-Blöcke bei ausgeschaltetem Modul | `PublicModuleGate` rendert im Aus-Zweig kein `HeadContent`. |
| `InfoPage`/`FaqHub` markieren die Entwurfsvorschau nicht als `noindex` | Der Entwurfszweig ist nur für Redakteure erreichbar; der anonyme Abruf bekommt `null` und damit `NoIndex`. |
| Strg+Shift+V unterdrückt den nativen Fallback ohne Clipboard-Rechte | In allen unterstützten Browsern ist `readText` im Nutzerkontext verfügbar; der Fall tritt nicht auf. |
| Entwurfs-Wiederherstellung und Auto-Save überholen sich beim Start | Es gibt eine Schranke, die der Finder übersehen hat. |
| Interceptor blockiert im Sync-Pfad auf Async-Arbeit | Der synchrone `SaveChanges`-Pfad wird im Produktionscode nicht benutzt. |
| Keine Changelog-Zeile für die NOOSEI-Anbieterumschaltung | Doch — `3daa507` hat sie im selben Commit mitgeliefert. |

---

## Außerhalb dieses Änderungssatzes

Zwei Punkte betreffen Dateien, die der Diff **nicht** anfasst. Sie stehen hier, weil sie dasselbe Muster
haben wie Befund 15 und weil dieser Änderungssatz mit `RequireHrbOrLeadershipWrite` das Gegenmuster
etabliert hat:

- `NOOSE-Website/Services/PersonnelFileService.cs:37` und `:98` — `NoteCreateAsync` prüft nur
  `RequireLeadership`, `NoteDeleteAsync` nur die Inline-Bedingung; beide ohne `MayWrite()`. Der
  Demo-Besucher sieht das volle Formular und die Löschen-Knöpfe.
- Dieselbe Familie: alle `RequireLeadership`-Aufrufe ohne Schreibprüfung. Ein Durchgang über
  `Services/Permission.cs` mit der Frage „welcher Guard prüft den Rang, aber nicht das Schreibrecht?"
  wäre eine eigene, gut abgrenzbare Aufgabe.

---

## Empfohlene Reihenfolge

1. **Befund 1** zuerst — er kann die Seite offline nehmen, und der Fix ist klein (Existenzprüfung vor jedem
   Insert, plus `try/catch` um die Inhalts-Seeder in `Program.cs`).
2. **Befunde 2, 3, 7, 8, 9** — die fünf, die ein Agent im Alltag sofort merkt (Word-Einfügen, totes
   Inhaltsverzeichnis, verschwindende Trennlinie, Listen). Alle im Texteditor; danach `?v=` auf 18 an
   **beiden** Importstellen.
3. **Befund 4** — ein Ausdruck, den man nicht lesen kann, ist kein Ausdruck. Eine CSS-Zeile.
4. **Befunde 5, 14, 16** — das Handbuch selbst: Redaktion, Navigation, Ladepfad.
5. **Befunde 10, 11** — Geld und Plattenplatz; kein Zeitdruck, aber beide wachsen mit der Nutzung.
6. Der Rest nach Gelegenheit.
