# Öffentlicher Bereich — Grundlagen

> **Lies das, bevor du irgendetwas unter `Components/Pages/Public/`, `Components/Pages/Portal/`
> oder `Services/Public/` anfasst.** Hier stehen die Regeln, die für *alle* öffentlichen Module
> gelten: Bürgerkonto, Modul-Schalter, Not-Aus, `PublicVisibility`, `PublicRoutes`,
> redaktionelle Seiten und die Migrations-Namenskonvention.
> Modul-spezifisches Detailwissen liegt in den Geschwisterdateien (`oeffentlich-*.md`).

## Stand & Umfang

Gebaut ist der öffentliche Bereich vollständig, Phase 1–16 aus `PublicPlan.md`: Bürgerkonten, das Schaltergerüst, die
redaktionellen Seiten, die öffentliche Fahndung, ihr Ausbau (Warnhinweise, Gefasst-Archiv, Poster, Ablauf,
Aufrufzähler, Discord-Push), das Kopfgeld, die Bürgerhinweise (Formular, Eingang, Rückfrage, Verfolgung,
Triage, Übernahme), die Belohnung (Auszahlung über die Kasse, Beleg für den Bürger), der Ticket-Chat an die
Führungsebene, die Vorlagen für Bürger-Nachrichten, die Organisationsprofile samt beider Gefahrenlisten,
die Sachfahndung (gesuchte Fahrzeuge und Waffen), der Bürger-Einspruch gegen eine Ausschreibung, die
Pressemitteilungen, die amtlichen Warnungen und die freigegebenen Gesetzesauszüge sowie die freigegebenen
Monatstexte, die Gefahrenlage-Ampel, die öffentlichen Zahlen samt umgebauter Startseite sowie die
Suchanbindung nach innen und außen mit den internen Kennzahlen.

- **Ein Bürger ist ein `Agent` mit `Status = Civilian`**, nicht mit Rechten (`IsCitizen()`). Der Klarname
  liegt in `BuergerProfil`, **nie** in `Agent.RealName` — das ist der behördliche Klarname hinter einem
  Führungs-Gate. `AgentSelection` schließt `Civilian` überall aus.
- **Zugang und Status sind zwei Fragen.** Den Bürgerbereich betreten darf **jedes angemeldete Konto**
  (`MayUseCitizenPortal()`, `Policies.CitizenPortal`, `Permission.RequireCitizenPortal`) — Agent, Partner,
  Nur-Lese-Aufsicht und Bewerber haben auch eine Zivil-Identität. `IsCitizen()` beantwortet nur noch, ob das
  Konto ein Profil haben **muss**: `BuergerLayout` erzwingt Vor-/Nachname zentral in `OnParametersSetAsync`
  (nicht `OnInitializedAsync`: die Layout-Instanz überlebt die Navigation zwischen Bürgerseiten) — aber **nur
  für `IsCitizen()`**. Sonst liefe eine Nur-Lese-Aufsicht in eine Umleitungsschleife auf ein Profil, das sie
  gar nicht anlegen darf: `SaveOwnAsync` ist ein Schreibpfad und hält zusätzlich `RequireWriteAccess`.
  Wer kein Profil hat, sieht die Seiten ohne eigene Zeilen; jeder Einreichungspfad geht über
  `RequireSubmittingCitizenAsync` (vollständig + nicht gesperrt).
- **Welche Module es gibt, steht ausschließlich in `Services/Public/PublicModules.cs`** — eine Zeile je
  Modul, auch für noch nicht gebaute. `PublicModuleSeeder` legt fehlende Zeilen beim Start an und
  überschreibt **nie** eine gespeicherte Wahl; ein Modul geht deshalb nie durch ein Deploy online.
  `Available = false` heißt „Seiten fehlen noch"; nur ein gebautes Modul darf `DefaultEnabled` sein
  (`PublicModulesCatalogTests` hält das fest).
- **Modul-Aus wirkt im Service, nicht in der UI.** `IPublicModuleService.RequireEnabledAsync` wirft; die
  Seite wickelt ihren Inhalt in `PublicModuleGate` (Offline-Text statt Inhalt, weil eine bekannte URL die
  Route trotzdem erreicht); und der **Login-Endpoint prüft mit** — `source=bewerbung`/`source=buerger`
  fragen ihr Modul, bevor ein Konto entsteht. Eine versteckte Schaltfläche lässt den POST offen.
- **Not-Aus** (`SystemSettingKeys.PublicAreaKillSwitch`) schlägt jedes Einzelmodul, **ohne** eine
  gespeicherte Wahl zu verändern — sonst veröffentlicht das Wiedereinschalten einen anderen Stand als den
  vorherigen. Der interne **Wartungsmodus wirkt nach außen bewusst nicht**; dafür ist dieser Schalter da.
  Er lässt `/buerger` bewusst offen: das ist der private Kontobereich eines angemeldeten Bürgers, nicht
  öffentlicher Inhalt. Er hinterlässt **zwei** Protokollzeilen — die rohe `SystemSetting`-Zeile des
  Interceptors (`SystemSetting` **ist** `IAuditable`) und eine `ManualAudit`-Zeile, die die Aktion benennt,
  weil „SystemSetting/OeffentlicherBereichNotAus" im Protokoll niemandem hilft. Muster der vier bestehenden
  Config-Services — deren Kommentar „SystemSetting is not auditable on its own" ist sachlich falsch.
- **Ein Modul ohne Seiten bekommt keinen Nav-Tab**, auch eingeschaltet nicht (`NavEntries()` filtert
  `Available`): vorab einschalten ist erlaubt, ein Tab auf eine 404-Route nicht. Die gespeicherte Wahl bleibt,
  der Tab erscheint von selbst, sobald die bauende Phase `Available` umstellt.
- **Ein neuer Audit-`EntityType` braucht eine Zeile in `AuditEntityDisplay`** (Label **und** Route), sonst
  steht der rohe CLR-Name in der deutschen UI von `/nachweis`. Kein Test erzwingt das — `BuergerProfil` fiel
  deshalb bis Phase 2 durch.
- Snapshot ist **10 s gecacht** (`IMemoryCache`) — nach einem Umschalten kann eine öffentliche Seite bis zu
  10 Sekunden alt sein. Bei unerreichbarer DB fällt der Service auf die Katalog-Standards zurück: fast alles
  aus, wie bei einer frischen Installation.
- **Icon-Overrides sind eine Allowlist**, gespeichert wird der Name (`PublicModules.IconChoices`). MudBlazor
  rendert einen Icon-Wert als Markup — ein freies SVG in der Spalte liefe bei jedem anonymen Besucher.
- **`Services/Public/PublicVisibility.cs` ist die eigene Achse „was darf nach draußen"**, Vorbild
  `SearchCatalog`. `PublicVisibilityCoverageTests` reflektiert über alle `DbSet`s: jede Entität steht in
  `Publishable` (mit **was** rausgeht) oder in `NeverPublic` (mit **warum nicht**). Eine Akte selbst ist nie
  publizierbar — nach außen geht ab Phase 4 ein Publikations-Snapshot.
- **`Services/Public/PublicRoutes.cs` ist die einzige Wahrheit der öffentlichen Pfade**; die Nav-Routen
  werden aus dem Katalog **abgeleitet**, nicht wiederholt — eine Route, die ein Modul schon nennt, gehört
  **nicht** zusätzlich in `ExtraPrefixes` (`/info` stand dort, bis das Modul sie übernahm). Danach richten sich `wwwroot/robots.txt`
  (geprüft von `PublicRoutesTests`) und `PublicIndexingMiddleware`. **`/buerger` ist bewusst nicht dabei:**
  das Konto eines Bürgers ist privat, nicht öffentlich.
- **Redaktionelle Seiten (`OeffentlicheSeite`, `/info/{Slug}`) trennen zwei HTML-Spalten mit je einer
  Bedeutung:** `EntwurfHtml` ist die Arbeitskopie, `InhaltHtml` wird **nur** von `PublishAsync` geschrieben.
  Speichern ist deshalb keine Publikation. `Status` entscheidet über die öffentliche Sichtbarkeit, `ImMenue`
  nur über die Verlinkung — eine veröffentlichte Seite darf bewusst nur per Direktlink erreichbar sein.
  `RetractAsync` behält `InhaltHtml` (die Sichtbarkeit hängt an `Status`), `RestoreAsync` bringt eine Seite
  aus dem Papierkorb **als Entwurf** zurück, damit ein Rückgängig nichts nebenbei wieder veröffentlicht.
  `HtmlCleanup.Clean` läuft beim Speichern **und** beim Veröffentlichen — Letzteres ist der Moment, in dem
  das Markup anonym erreichbar wird.
  - **Der `Slug` ist absichtlich nicht unique indexiert:** bei Soft-Delete würde der Index die Adresse für
    immer blockieren. Eindeutigkeit prüft `PublicPageService` über die lebenden Zeilen — mit ausgeschriebenen
    Zweigen, weil `Id != input.Id` mit `null` zu SQL-`NULL` übersetzt und stumm nichts finden würde.
    Erlaubte Form steht ausschließlich in `Services/Public/PublicPageSlug.cs`.
    **Deshalb prüft auch `RestoreAsync` die Adresse:** eine gelöschte Seite behält ihren Slug, die Adresse darf
    inzwischen neu belegt sein, und Wiederherstellen darüber hinweg ergäbe zwei lebende Seiten auf einer Adresse.
    Der Lesepfad dedupliziert zusätzlich (`GroupBy` statt `ToDictionary`) — sonst wirft der Aufbau des Snapshots,
    der `catch` verschluckt es, und **jede** redaktionelle Seite verschwindet statt nur der strittigen.
  - **`PublicPageInput.DraftHtml`: `null` heißt „unverändert", `""` heißt „leeren".** Ohne diese Trennung würde
    ein Aufruf, der nur den Titel ändert, den Text still löschen. **`PublicPageEdit` trägt kein HTML** — Bilder
    liegen als base64 im Body, eine Liste mit Entwürfen wäre Megabyte pro Render; der Editor holt den einen
    Entwurf über `GetDraftAsync`. Aus demselben Grund projiziert `GetAllAsync` auf den Codename statt den
    Identity-User zu laden: dessen `RealName` hat in einem Panel nichts zu suchen, das die Aufsicht rendert.
  - **„Leer" heißt: weder Text noch Bild.** Eine Seite, die nur aus einem Organigramm besteht, ist Inhalt;
    `HtmlCleanup.PlainText` allein hätte sie als leeren Entwurf abgelehnt.
  - **Eine fehlende Seite trägt `noindex`** (per `<HeadContent>`): `/info` ist indexierbar, und die Route
    antwortet für jeden erfundenen Slug mit 200 — ohne das Meta-Tag indexiert ein Crawler beliebig viele
    Soft-404s.
  - **Öffentlicher Inhalt wird als rohes `MarkupString` gerendert, nie über `RichHtml`** (das löst
    `@{Typ:GUID}` auf und würde interne Aktennamen ausliefern), und der `RichTextEditor` läuft dort **ohne
    `Ai` und ohne `Mentions`**.
  - **Ein Query-Parameter einer öffentlichen Route wird als `string` gebunden, nicht als `bool`/`int`.**
    Blazor antwortet auf einen Wert, den es nicht parsen kann, mit HTTP 500 — und an eine öffentliche URL
    hängt jeder eine Query. `?vorschau=1` war genau so ein 500.
    Und er wird als **public** Property deklariert — Hauskonvention, damit die Regex-Wächter ihn sehen
    (`PublicPageScanTests`, `CitizenSurfaceScanTests`). Technisch **bindet** eine private Deklaration durchaus
    (`SupplyParameterFromQueryAttribute` leitet von `CascadingParameterAttributeBase` ab, und dieselbe Codebasis
    nutzt private `[CascadingParameter]` erfolgreich) — sie ist nur für keinen Scan sichtbar, weshalb zwei
    private auf `/Account/Login` jahrelang ungeprüft blieben. `CitizenSurfaceScanTests` lehnt die private Form
    jetzt ausdrücklich ab statt sie zu überspringen.
  - Ein Tab je Seite gibt es nicht: `Infoseiten` hat **einen** Tab auf den Hub `/info`, damit die Nav weiter
    allein aus `PublicModules` kommt.

## Migrationen

- **Migrationen des öffentlichen Bereichs heißen `Oeffentlich<Planphase>_<Name>`**, nicht `PhaseNN_` — die
  interne Zählung steht schon bei `Phase69` und hätte sich sechsfach überschnitten. Einzige Ausnahme:
  `Phase61_BuergerKonto` (Phase 1) war beim Auffallen bereits angewendet.
