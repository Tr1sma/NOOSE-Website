# CLAUDE.md — NOOSE-Website

Zentrale Akten-/Intelligence-Datenbank für die **NOOSE** (National Office of Security Enforcement),
eine fiktive Behörde auf einem FiveM/GTA-RP-Server. Die Seite ersetzt verstreute Discord-Threads
durch eine zentrale, durchsuchbare, bidirektional verlinkte Akten-Datenbank: pro Person und pro
Fraktion eine Akte, in der alles zusammenläuft. **Codebase ist anglisiert (englische Identifier),
aber Domänen-Vokabular, UI-Texte, Kommentare und Planungsdocs sind Deutsch.** Live: https://noose.info

## Detailwissen (`claude-memory/`)

Diese Datei hält, was für **jede** Änderung gilt. Bereichswissen — vor allem die Begründungen
hinter den Regeln des öffentlichen Bereichs — liegt in `claude-memory/`. **Lies die passende
Datei, bevor du im jeweiligen Bereich etwas änderst**; sie erklärt, warum eine Regel existiert,
und genau das verhindert die Fehler, die dort schon einmal passiert sind.

| Datei | Lies sie, wenn du … |
|---|---|
| [`oeffentlich-grundlagen.md`](claude-memory/oeffentlich-grundlagen.md) | **irgendetwas** unter `Components/Pages/Public\|Portal/` oder `Services/Public/` anfasst: Bürgerkonto, Modul-Schalter, Not-Aus, `PublicVisibility`, `PublicRoutes`, redaktionelle Seiten, Migrationsnamen |
| [`oeffentlich-fahndung.md`](claude-memory/oeffentlich-fahndung.md) | an `OeffentlicheFahndung`, `PublicWantedService`, `/gesucht`, `/gefasst`, Poster, Foto-Endpoint, Sachfahndung oder Einspruch arbeitest |
| [`oeffentlich-geld.md`](claude-memory/oeffentlich-geld.md) | Kopfgeld oder Belohnung anfasst (`FahndungKopfgeldAnteile`, `IBountyService`, `IRewardService`, Kassenbuchung, Beleg) |
| [`oeffentlich-buergerkontakt.md`](claude-memory/oeffentlich-buergerkontakt.md) | an Hinweisen, Triage, Übernahme, Ticket-Chat oder Bürger-Vorlagen arbeitest — **inkl. der Anonymitätszusage** |
| [`oeffentlich-redaktion.md`](claude-memory/oeffentlich-redaktion.md) | Organisationsprofile, Presse, Warnungen, Gesetzesfreigabe, Lageberichte, Gefahrenlage-Ampel oder die öffentlichen Zahlen anfasst |
| [`oeffentlich-suche.md`](claude-memory/oeffentlich-suche.md) | einen öffentlichen Suchprovider, `PublicSearchService`, `/suche-oeffentlich` oder die Außen-KPIs anfasst |
| [`noosei.md`](claude-memory/noosei.md) | am Gateway, an einem NOOSEI-Werkzeug, an Kontingenten oder an den KI-Panels arbeitest |
| [`services-details.md`](claude-memory/services-details.md) | `AgentSelection` „aufräumen" willst, an der Bestenliste arbeitest oder eine Suchkategorie hinzufügst |
| [`ui-details.md`](claude-memory/ui-details.md) | eine Route entfernst/verschiebst (Legacy-Redirects) oder `NavMenu.razor` anfasst |

## Rules for comments
- **English only** — no German, ever
- **Inline `//`** — 2–3 words; describe the *why*, not the what
- **`catch { }` blocks** — `/* best effort */` or `/* ignore */`
- **XML `/// <summary>`** — one short English line: `/// <summary>Set classification on target.</summary>`
- **No block comments** — collapse multi-line explanations to a single short line or delete them
- **No "Phase X" references** in comments — just describe what the code does

## Tech-Stack

- **.NET 10** (`net10.0`), Single-Project-Solution (`NOOSE-Website.slnx` → `NOOSE-Website/NOOSE-Website.csproj`)
- **Blazor Web App, nur Interactive Server** (SignalR) — kein WebAssembly/Auto
- **MudBlazor 9.5** (Dark-Theme „Anthrazit + Cyan", **nur Dark-Mode**)
- **EF Core 9** via **Pomelo.EntityFrameworkCore.MySql 9.0** → lokal MariaDB/XAMPP, Prod MySQL 8.0
- **ASP.NET Core Identity** (User-Entity = `Agent`) + **Discord-OAuth** (`AspNet.Security.OAuth.Discord`)
- Weiteres: HtmlSanitizer, Quill 1.3.7 (RichText), vis-network (Graph), FullCalendar (Kalender) — alle self-hosted unter `wwwroot/lib`

> ⚠️ **EF/Identity NICHT auf 10.x heben.** Pomelo 9 unterstützt nur EF Core 9; 10.0.x würde EF Core 10 ziehen und kollidieren. Bewusst auf der 9.0.x-Linie auf .NET-10-Runtime bleiben (siehe csproj-Kommentar).

## Befehle

Alle Befehle aus dem **Repo-Root** ausführen.

```bash
# Build
dotnet build NOOSE-Website.slnx

# Lokal starten  →  http://localhost:5174  (Profil "https": https://localhost:7063)
dotnet run   --project NOOSE-Website/NOOSE-Website.csproj
dotnet watch --project NOOSE-Website/NOOSE-Website.csproj run   # Hot Reload

# EF-Migrationen (dotnet-ef ist LOKALES Tool, gepinnt auf 9.0.17)
dotnet tool restore                                             # EINMALIG, vor jedem 'dotnet ef'
dotnet ef migrations add Phase23_<Name> --project NOOSE-Website/NOOSE-Website.csproj
# 'dotnet ef database update' ist i.d.R. UNNÖTIG — Migrationen werden beim App-Start
# automatisch via db.Database.MigrateAsync() angewendet (Program.cs).

# Deploy nach Produktion (root@195.20.225.12, systemd-Service 'noose', /var/www/noose)
.\deploy.ps1                # publish → tar → scp → service-swap (behält App_Data) → /health-check
.\deploy.ps1 -SkipPublish   # vorhandenen ./publish-Ordner wiederverwenden
.\deploy.ps1 -NoPause       # ohne "Enter zum Schließen" (CI/Terminal)
```

- **Test-Projekt `NOOSE-Website.Tests`** (xunit, ~3.5k Tests): `dotnet test NOOSE-Website.slnx`. Helfer in `Tests/Infrastructure/`: `SqliteTestContext` (In-Memory-SQLite + `IDbContextFactory`), `Seed.*` (Entity-Fabriken), `ClaimsPrincipalBuilder` (Rang/Flags/Claims). **Kein bUnit** → `.razor`-Komponenten sind nicht testbar; testbare Logik gehört in den Service-Layer.
- `deploy.ps1` aus **64-bit Windows PowerShell** starten (sonst wird OpenSSH WOW64-redirected). Nutzt `tar` + `scp`/`ssh`.

### Secrets & Config

`appsettings.json` enthält **nur leere Platzhalter**. Echte Werte kommen aus der Source-Control heraus:

- **Lokal:** User-Secrets (`UserSecretsId d41f8a93-2c7b-4e16-9a55-0b3e7c1f6d28`)
- **Produktion:** Env-Vars in `/etc/noose/noose.env` (Doppel-Unterstrich: `ConnectionStrings__ProductionConnection`, `Authentication__Discord__ClientId`)

Benötigte Keys: `ConnectionStrings:DefaultConnection` (oder `:ProductionConnection`), `Authentication:Discord:ClientId` + `:ClientSecret`, `Bootstrap:AdminDiscordId`.
Ohne erreichbaren DB-Connection-String wirft die App beim Start. `DatabaseConnectionResolver` bevorzugt `ProductionConnection` (5s-Reachability-Probe), sonst Fallback auf `DefaultConnection` → derselbe Build läuft lokal und auf dem Server ohne Edit.

## Architektur

Schichten innerhalb von `NOOSE-Website/`:

| Ordner | Inhalt |
|--------|--------|
| `Components/` | Razor-Pages + UI (dünn). `Pages/<Feature>/`, `Layout/`, `Common/Shared/`, `Account/` |
| `Navigation/` | `NavCatalog` (Einträge), `NavEntry`/`NavSection` (Policy-Achse), `NavArea` (Darstellungs-Achse), `LegacyRoutes`, `MergedPageSections` |
| `Data/` | `AppDbContext`, `Entities/<Domain>/`, `Migrations/` (~51, Phase-Präfix) |
| `Models/` | DTOs/View-Models, `Enums/`, `Abstractions/` (Marker-Interfaces) |
| `Services/` | Business-Logik **+ die echte Authorization-Durchsetzung**; Subordner `Graph/`, `Statistics/`, `Threat/` |
| `Authorization/` | Policies, Requirements, Handler, `ClaimsPrincipal`-Extensions |
| `Infrastructure/` | Interceptors, File-Storage, Audit, Background-Worker, CurrentUser, Broadcaster |

- **`Program.cs`** ist Composition-Root (Top-Level-Statements): alle DI-Registrierungen inline, nach Build-„Phase" gruppiert/kommentiert.
- **Render-Mode** wird pro Seite in `App.razor` gesetzt: `InteractiveServer`, außer `[ExcludeFromInteractiveRouting]` (Error, NotFound, Login, Pending, Blocked, Legal) → statisch.
- **Culture global auf de-DE** fixiert (`UseRequestLocalization` + `CultureInfo.DefaultThread*`).
- **Middleware-Reihenfolge** (load-bearing): `UseForwardedHeaders` (zuerst, vertraut nur Loopback/nginx) → `RequestLocalization` → `PublicIndexingMiddleware` (noindex außerhalb der öffentlichen Routen; **vor** dem ExceptionHandler, damit re-executete Fehlerseiten den Header behalten) → (nur Prod) `ExceptionHandler`+`HSTS` → `StatusCodePagesWithReExecute("/not-found")` → `HttpsRedirection` → `Authentication` → `DemoModeMiddleware` → `Authorization` → `Antiforgery` → `RateLimiter` (**nach** Antiforgery: ein POST ohne Token darf kein Permit verbrauchen, sonst hält ein anonymer Besucher die Anmeldung dauerhaft auf 429) → `MapStaticAssets` → `/health` → `MapRazorComponents<App>` → `Map*Endpoints`-Gruppen.
- **SignalR Hub:** `MaximumReceiveMessageSize = 25 MB` (für den RichTextEditor, der volles HTML inkl. base64-Bildern über SignalR streamt — nicht zurücksetzen).
- **Background-Worker** (`AddHostedService`): `FollowupDueWorker` (Wiedervorlagen), `ThreatScoreSweepWorker` (tägl. Score-Decay, seedet Fraktionen beim ersten Start), `SituationReportWorker` (monatl. Lageberichte). Laufen pro Host-Instanz → keine Multi-Instanz gegen eine DB.
- **Health-Check** `/health` (`AddDbContextCheck`) — von Deploy-Skript und Status-Seite genutzt.

## Datenmodell (EF Core)

- **Ein** `AppDbContext : IdentityDbContext<Agent>` (~60 DbSets); **alle** Fluent-Configs in `OnModelCreating` (keine `IEntityTypeConfiguration`-Klassen). Annotations nur für `[Table]`/`[Column]` (deutsche Namen) und `[NotMapped]`.
- **DbContext-Factory:** Immer `IDbContextFactory<AppDbContext>` injizieren und pro Operation einen kurzlebigen Context erzeugen (`await using var db = await dbFactory.CreateDbContextAsync(ct)`). Ein zirkuit-langer scoped Context wirft in Blazor Server *„A second operation was started on this context"*.
- **3 SaveChanges-Interceptors, Reihenfolge zählt:** `ReadOnlyBarrierInterceptor` (zuerst!) → `AuditSaveChangesInterceptor` → `WatchlistChangeInterceptor`.
- **PKs:** meist `string` GUID (`Id = Guid.NewGuid().ToString()`); `AuditLog` nutzt `long`; `Agent` erbt Identity-`string`-Key.
- **Cross-Cutting via Marker-Interfaces** (`Models/Abstractions/`): `IAuditable` (CreatedAt/By, ModifiedAt/By — vom Interceptor gestempelt) und `ISoftDelete` (globaler Query-Filter `!IsDeleted`, automatisch per Reflection angewandt). Neue Entität → einfach Interface implementieren.
- **Soft-Delete ist Norm:** Löschen über EF rewritet `Deleted` → `Modified`. Papierkorb-Queries: `IgnoreQueryFilters().Where(x => x.IsDeleted)`.
- **DB-Spalten Deutsch, C#-Member Englisch:** z. B. `Person.CaseNumber` → Spalte `Aktenzeichen`, Tabelle `Personen`, `IsDeleted` → `IstGeloescht`.
- **Polymorphe Assoziationen** (Quellen, Kommentare, Tags, Links, Followups, ClassificationHistory, …) über `(EntityType string via nameof(T), EntityId string)` — **kein echter FK**, schneller Pfad ist der Composite-Index.
- **`DeleteBehavior.Restrict`** (statt Cascade) bei `PersonRelation` und `*Member`-Tabellen, um MySQL-„multiple cascade paths" zu vermeiden; FKs auf die Identity-`Agent`-Tabelle nie Cascade.
- **`longtext`** für HTML/JSON-Spalten (`Document.ContentHtml`, `*Json`, `SystemSetting.Value`) — kein `HasMaxLength` darauf.
- **Aktenzeichen** (z. B. `NOOSE-P-2026-0001`) race-safe über `CaseNumberCounter` (Composite-Key `Prefix`,`Year`) in einer Transaktion.
- **Design-Time** (`AppDbContextDesignTimeFactory`) zwingt EF-Tools immer auf lokale `DefaultConnection` → Migrationen können **nie** Produktion treffen.

## Services-Layer

- **Interface-first:** jeder DI-Service ist `I<Name>Service` + `<Name>Service`, `AddScoped`. Implementierungen nutzen **Primary Constructors**. Jede public-async-Methode hat ein trailing `CancellationToken cancellationToken = default`.
- **Live-Updates per Singleton-Broadcaster/Dispatcher:** scoped Service schreibt die Row, ruft dann den Singleton (`NotificationBroadcaster`, `TaskforceChatBroadcaster`, `SharesBroadcaster`, `AcknowledgmentBroadcaster`, `WatchlistDispatcher`) zum Push an verbundene Circuits.
- **Authorization wird IM Service-Layer durchgesetzt**, nicht nur in der UI: statische Guards `Permission.Require*` (werfen `UnauthorizedAccessException`), Sichtbarkeit zentral in statischem `Visibility`/`*Visibility`/`RecordsReference`. Write-Methoden nehmen `ClaimsPrincipal actor` und rufen den Guard als erste Anweisung.
- **Statische Helfer in `Services/`** (NICHT DI-registriert): `Permission`, `Visibility`, `AgentSelection`, `ClassificationHelper`, `TextSimilarity`, `RecordsReference`, `MentionParser`, `HtmlCleanup`, `TrashProjection`, `Public/PublicModules`, `Public/PublicVisibility`, `Public/PublicRoutes`. Geteilte Logik dorthin extrahieren statt kopieren.
- **Wer in eine Agenten-Auswahlliste darf, entscheidet ausschließlich `Services/AgentSelection.cs`.**
  `db.Users.OnlySelectable()` = `Active && !IsTeamLead && PartnerAgency == null` für **jeden** Picker,
  Dropdown, Roster und Roster-Fan-out (also überall, wo Empfänger *aus dem Gesamtbestand* gewählt werden);
  `OnlyListable()` = `Codename != "" && !IsTeamLead && PartnerAgency == null` **nur** für die Log-Filter
  (`AgentDirectory`), die Gekündigte/Gesperrte bewusst behalten. `IsSelectable(agent)` ist der
  In-Memory-Zwilling. Nie `db.Users` von Hand nach Status/Flags filtern — und den **Schreibpfad mit
  demselben Prädikat absichern** (`OnlySelectable().AnyAsync(...)`), sonst bleibt der SignalR-Pfad offen.
  Einzige Ausweitung: der Audit-Viewer (`/nachweis`, `AuditLogQueryService.GetFilterOptionsAsync`) nutzt
  `OnlyAuditFilterable()` = `Codename != ""` (via `AgentDirectory.AllForAuditAsync`) und listet damit
  bewusst auch Teamleitungen und Partner — mit Marker-Suffix im Dropdown —, damit deren Log-Zeilen
  filterbar sind.
- **Bewusste Ausnahmen von `AgentSelection`** (MentionService, Admin-Roster, Partner-Inverse,
  ID→Codename-Wörterbücher, Fan-out über übergebene Empfängerlisten, …) stehen in
  [`claude-memory/services-details.md`](claude-memory/services-details.md) — **nicht „aufräumen"**.
- **Bestenliste: der Rang-Boden liegt NICHT in `AgentSelection`** (`GamificationService.LeadershipFloor`
  partitioniert eine schon autorisierte Menge auf einer zweiten Achse). Ihn zu verschieben leert jeden
  Picker → [`services-details.md`](claude-memory/services-details.md).
- **Ein Picker, der eine gespeicherte Agenten-ID auflöst, muss auf `FindAsync` zurückfallen**, wenn die ID
  nicht mehr auswählbar ist (`FollowupDialog`, `ObservationDialog`). Sonst bleibt das Objekt `null` und der
  Speichern-Pfad *löscht die Zuordnung still* bzw. schreibt sie auf den Bearbeiter um. Auflösen **außerhalb**
  der Angebotsliste, damit niemand die Person neu auswählen kann.
- **`ITrashService`** fächert den globalen Papierkorb über alle 13 Record-Dienste auf. Alle haben dieselbe
  Signatur (`GetTrashAsync(ct)` / `RestoreAsync(id, actor, ct)`), also bindet Restore als **Methodengruppe** —
  kein generischer EF-Pfad, kein Bulk-SQL, Permission-Guard und Audit-Interceptor laufen weiter mit.
  Ein neuer löschbarer Typ wird in `TrashService` als eine Zeile registriert + eine `TrashProjection`-Methode.
- **Globale Suche:** `SearchService` ist nur noch **Orchestrator**. Je Kategorie ein `ISearchProvider`
  (`Services/Search/Providers/`, ~67), registriert über `AddSearchProviders()`. Der **`SearchCatalog`
  (`Services/Search/SearchCatalog.cs`) ist die einzige Wahrheit** für Label, Icon, Route, Trefferform, Traits,
  Facetten-Reihenfolge und das `suche_akten`-Enum — eine neue Kategorie ist **eine Zeile dort + ein Provider**.
  In-Memory-Levenshtein (`TextSimilarity`) bleibt, weil MySQL/Pomelo keine Edit-Distance übersetzt.
  Die sechs Regeln dahinter (Sichtbarkeits-Prädikate benennen, zwei Wellen unter Wanduhr-Budget,
  Trefferzahlen, keine Vorfilter, `SearchParentResolver`, Partner-Deckel) →
  [`services-details.md`](claude-memory/services-details.md).
- **Maintenance/Banner/Theme/Logo:** `SystemSettingService` über Key/Value-Tabelle, 10s `IMemoryCache`. Logo/Uploads liegen **außerhalb wwwroot** unter `App_Data/uploads`, ausgeliefert über autorisierte Minimal-API-Endpoints.
- **Vier getrennte Token-Systeme, nie vermischen:** `PlaceholderService` (`{{Name}}`, `{{Aktenzeichen}}`, `{{Datum}}`, `{{Uhrzeit}}`, `{{Agent}}`, `{{Dienstgrad}}` — Dokument-/Aktivitäts-/Personal-Vorlagen) · `BewerbungTemplateRenderer` (bare `NAME`/`BEWERBER`/`DATUM`/`UHRZEIT`/`DIENSTGRAD`, nur Bewerbungs-Anschreiben) · `MentionParser` (`@{Typ:GUID}`, aufgelöst über `MentionService.ResolveManyAsync` → `<MentionText>`) ·
  `PublicTemplateRenderer` (bare `BUERGER`/`AKTENZEICHEN`/`DATUM`/`UHRZEIT`/`NAME`, nur Bürger-Nachrichten,
  **ohne** HTML-Encoding — siehe Phase 11 in [`claude-memory/oeffentlich-buergerkontakt.md`](claude-memory/oeffentlich-buergerkontakt.md)).
- **Platzhalter werden NUR beim Anwenden einer Vorlage expandiert**, nicht beim Speichern: Vorlagen-*Editoren* und der Edit-Modus gespeicherter Records lassen Tokens bewusst roh stehen (dort sind sie der Payload).

## Authorization, Ränge & Rollen

Drei orthogonale Achsen: **(1) Rang** (`Models/Enums/Rank.cs`, int-backed `JuniorAgent=1 … Director=6`), **(2) Boolean-Flags** auf `Agent` (`IsAdmin`, `IsTRU`, `IsHRB`, `IsTeamLead`), **(3) Policies** (kombinieren Rang+Flags).

- **Berechtigungslogik existiert an genau zwei Stellen** — nirgends sonst rohe Claim-Checks:
  - `Authorization/AgentPrincipalExtensions.cs` — `ClaimsPrincipal`-Extensions (`IsAdmin`, `IsLeadership`, `IsOnlyReader`, `MayWrite`, `MayRealNameSee`, `MayHighestClassification`, …) für UI/Policies/Read-Gates.
  - `Services/Permission.cs` — statische `Require*`-Guards für Service-Writes.
- **Führung (Leadership)** = Rang ≥ `SupervisorySpecialAgent(4)` **oder** Admin. **`HöchsteEinstufung`** ≥ `SeniorSpecialAgent(3)`, **`BeförderungEntscheiden`** ≥ `DeputyDirector(5)`.
- **Admin = Boolean-Flag** (`Agent.IsAdmin` / Claim `noose:admin`), **nicht** der Rang und **nicht** die geseedete Identity-Rolle „Admin" (die ist ungenutzt). Admin short-circuited jedes `RankRequirement`.
- **Nur-Lese-Aufsicht (`OnlyReader`)** = `IsTeamLead && !IsAdmin` (abgeleitet, kein Flag): liest alles (inkl. VS), schreibt **nichts** (vom `ReadOnlyBarrierInterceptor` hart vetoed), sieht **nie** Klarnamen. `IsTeamLead` allein gewährt sonst keine Rechte; TeamLeads sind RP-weit unsichtbar.
  - **`IsTeamLead` entfernt den Account aus jeder Auswahlliste** (`AgentSelection`), auch mit `IsAdmin` obendrauf. Deshalb zeigt die **Einsichtsliste eines VS-Dokuments die Aufsicht nicht**, obwohl `DocumentViewerScope.CanSee` ihr den Lesezugriff weiterhin gewährt — die Liste ist absichtlich unvollständig, sonst würde sie die Existenz der Aufsicht verraten. Nicht „reparieren".
- **Claims werden beim Login** in den Cookie geschrieben (`AgentClaimsPrincipalFactory`) → keine DB-Hits pro Request. Rang-/Rollen-/Status-Änderung rotiert den `SecurityStamp` (`Save(agent, newStamp: true)`) → erzwingt Re-Login (`SecurityStampValidator` revalidiert alle 30s).
- **Neue Policy anlegen:** Konstante in `Policies.cs` → registrieren in `AuthorizationRegistration.AddNooseAuthorization` (`RankRequirement` für Rang-Gate **oder** `RequireAssertion(ctx => ctx.User.SomeExtension())`) → ggf. Extension in `AgentPrincipalExtensions.cs`. **Policy-Strings nie hardcoden** — immer `Policies.*`.
- **Account-Flow:** Discord-Login → `Agent` mit `Status=Pending` → Freigabe durch Führung/Admin (`AgentManagementService.ReleaseAsync`) setzt `Active` + Rang + Flags. Bootstrap-Admins via `Bootstrap:AdminDiscordId(s)`.
- **Zwei VS-Achsen:** `Classification` (Einstufung Person/Fraktion: `ReviewCase`/Prüffall → `SuspicionCase`/Verdachtsfall → `SecuredStateThreatening`/Gesichert staatsgefährdend) **und** `DocumentClassification` (Bibliotheks-VS-Stufe: `None`/`Leadership`/`Tru`/`Hrb`). VS-Sichtbarkeit wird **server-seitig** über `DocumentViewerScope.CanSee` durchgesetzt, nicht über die `Classified`-Policy (reserviert/ungenutzt).
- **Keine DoJ/LSPD/LSMD-Accounts/-Ränge** — jeder User ist ein NOOSE-`Agent`. Partner-Lesezugriff (Phase 9) ist noch nicht gebaut.

## UI / Blazor-Komponenten

- **Ein Feature-Ordner je Bereich** unter `Components/Pages/` (Account, Admin, Board, Calendar, Cases, Factions, Graph, Groups, Jobs, Laws, Operations, OrgChart, Parties, People, Personnel, Search, Statistics, Taskforces, Tips, Wanted, Watchlist). Pro Feature: `*List`/`*Editor`/`*Detail`/`*Print` + `Shared/`. Cross-Feature → `Components/Common/Shared/`.
- **Deutsche Routen:** `/personen`, `/fraktionen`, `/vorgaenge`, `/aufgaben`, `/operationen`, `/parteien`, `/personengruppen`, `/taskforces`, `/kalender`, `/organigramm`, `/statistik`, `/brett`, `/gesetze`, `/suche`, `/graph`, `/fahndung`, `/hinweise`. CRUD-Subroutes `/{feature}/neu`, `/{Id}`, `/{Id}/bearbeiten`, `/{Id}/druck`.
- **Neu-/Bearbeiten liegen in EINER Datei** (`*Editor.razor` mit zwei `@page`-Direktiven). Beide Routen binden
  denselben Komponententyp → Blazor recycelt die Instanz beim Wechsel. **Laden gehört deshalb in
  `OnParametersSetAsync` mit `_loadedId`-Guard, nie in `OnInitializedAsync`** (läuft sonst nicht erneut).

### V1.5: zusammengefasste Seiten

Seit V1.5 gibt es statt vieler Einzelseiten sieben Sammelseiten mit `RecordSectionRail`:

| Route | ersetzt |
|---|---|
| `/einstellungen` | 13 Admin-Seiten (System, Discord, Status, Tags, Custom-Felder, Aktualität, Bedrohungs-Score, Vorlagen ×4, Module, Einladungen, Partner, Basisdaten) |
| `/nachweis` | `/chronik`, `/einstellungen?tab=protokoll` (Änderungen + Zugriffe), `?tab=gegenaufklaerung`, `?tab=gegenaufklaerung-regeln` |
| `/papierkorb` | 12 `*Trash.razor`-Seiten, getrieben von `ITrashService` |
| `/fahndung` | `/observationen`, `/doks` |
| `/abmeldungen` | `/abmeldungen/uebersicht`, `/abmeldungen/papierkorb` |
| `/bewerbungen` | `/bewerbungen/sperren`, `/bewerbungs-vorlagen`, `/bewerbungs-tests` |
| `/statistik` | `/lageberichte` |

- **Alte Routen leben weiter** in **einer** Shell (`Components/Common/Navigation/LegacyRouteRedirect.razor`
  + `Navigation/LegacyRoutes.cs`); `MergedPageSections.cs` hält die Slug-Listen; strengere Abschnitte
  werden in `<AuthorizeView>` gewickelt. Mechanik und Fallstricke →
  [`claude-memory/ui-details.md`](claude-memory/ui-details.md).
- **Globales `[Authorize(Policy = Policies.ActiveAgent)]` in `_Imports.razor`** → jede neue Seite ist standardmäßig auth-pflichtig. Öffentliche Seiten brauchen explizit `[AllowAnonymous]` (Login, Pending, Blocked, Error, NotFound, Legal).
- **Strengere Seiten:** `@attribute [Authorize(Policy = Policies.LeadershipPage|AdminPage|HighestClassificationPage)]`. Feingranular per `<AuthorizeView Policy="@Policies.X" Context="...">` (explizite `Context`-Namen bei Verschachtelung). `*Page`-Policies lassen bewusst auch den `OnlyReader` rein — nicht zu Rang-Requirements „vereinfachen".
- **Kein Code-Behind** (`*.razor.cs` existiert nicht) — Logik in inline `@code`. Private Felder `_camelCase`.
- **Dark-Mode hardcoded** (`IsDarkMode="true"`, nur `PaletteDark` in `Theme/NooseTheme.cs`). Admin-Akzentfarben zur Laufzeit über `/einstellungen?tab=system` (`NooseTheme.WithColours(...)`).
- **JS-Interop** ist self-hosted + lazy-loaded je Seite mit `?v=`-Cache-Buster: `graph.js` (vis-network), `kalender.js` (FullCalendar), `richtext.js` (Quill 1.3.7); `app.js` (Strg+K Command-Palette) ist das einzige global geladene Modul. Interop-Komponenten: `IAsyncDisposable`, Import in `OnAfterRenderAsync(firstRender)`, `[JSInvokable]`-Callbacks, alles in `try/catch` gegen `JSDisconnectedException`.

### Gemeinsame UI-Bausteine (`Components/Common/Shared/`)

Vor dem Bauen einer neuen Seite hier nachsehen — diese Bausteine sind bereits überall im Einsatz:

| Komponente | Zweck |
|---|---|
| `PageHeader` | Kopfzeile jeder Seite (Icon, Titel, Untertitel, `BackHref`, `Actions`) |
| `EmptyState` | Leerzustand für `NoRecordsContent` |
| `StatTile` / `HazardList` | KPI-Kachel (klickbar über `Href`) und Gefährdungsliste des Dashboards |
| `RecordSectionRail` + `RecordSection` | vertikale, gruppierte Abschnittsleiste; hält den Abschnitt in `?tab=` |
| `QueryState` | Listenfilter in der URL halten (`Read`/`ReadEnum`/`ReadFlag`/`WriteAsync`) |

- `QueryState.WriteAsync` und `TabUrlState` schreiben über `window.nooseReplaceState` (in `App.razor`) —
  **`replaceState` löst kein `LocationChanged` aus**, deshalb aktualisiert der Rail `_activeSlug` selbst.
- `TabUrlState.ParameterName` ist ein hartkodiertes `"tab"` → **kein `MudTabs` mit URL-Sync innerhalb
  eines `RecordSectionRail`** (Query-Kollision). Abschnitte stattdessen flach ziehen.

### Drawer: Icon-Leiste + Panel

`Components/Layout/NavMenu.razor` ist zweispaltig; ein Icon-Klick **navigiert nicht**, er wechselt
nur das Panel. Die sieben Regeln dazu (zwei orthogonale Achsen `NavSection`/`NavArea`, `MudNavMenu`-Scope,
handgebaute Leiste, `aria-current`, Policy-Snapshot, tote `CollapsedGroups`) →
[`claude-memory/ui-details.md`](claude-memory/ui-details.md).

## Wichtige Gotchas

- **Nach Route-Änderungen die App wirklich starten, nicht nur bauen.** Zwei Komponenten auf derselben
  `@page` sind kein Compilerfehler — sie werfen erst beim Aufbau der Routing-Tabelle zur Laufzeit.
- **`dotnet tool restore` vor jedem `dotnet ef`** — `dotnet-ef` ist lokal-gepinnt (9.0.17), nicht global.
- **EF/Identity nicht auf 10.x** (Pomelo-9-Kollision).
- **Vor `dotnet ef migrations add` den Dev-Server stoppen** (bin-Lock), dann neu bauen.
- **`App_Data` beim Deploy nie löschen** — enthält Uploads **und** Data-Protection-Keys (`App_Data/keys`); Verlust loggt alle User bei jedem Restart aus. `deploy.ps1` schließt `App_Data` explizit vom Löschen aus.
- **Deploy nutzt `tar`, nie `Compress-Archive`** (packte früher 0-Byte-Dateien → kaputtes MudBlazor-CSS).
- **`TZ=Europe/Berlin` in `/etc/noose/noose.env`** nötig — Blazor Server rechnet `ToLocalTime()` in der Server-TZ; ohne TZ sind alle Zeiten (inkl. 20-Min-„Tot"-Fenster) verschoben. `TimeZoneInfo.Local` ist prozess-gecached → Restart nach Änderung.
- **`?v=` bumpen bei JS-Modul-Edits** (`graph.js?v=8`, `kalender.js?v=7`, `richtext.js?v=10`, `app.js?v=3`) — dynamische ES-Imports umgehen Blazors Asset-Fingerprinting. **Alle** Importstellen eines Moduls mitziehen: `app.js` wird von `CommandPalette.razor` **und** `FinancingCatalogPanel.razor` geladen, und zwei verschiedene `?v=` holen zwei Kopien.
- **Ablehnen, Schließen und eine nicht bestandene Sicherheitsüberprüfung sperren 14 Tage.** Die Dauer, das
  Aktiv-Prädikat (`IstBlacklist || GesperrtBis > jetzt`, es gibt keine `IstAktiv`-Spalte) und die
  Lokal→UTC-Umrechnung des `MudDatePicker` liegen zusammen in `Services/BewerbungssperreRules.cs`. Die Sperre
  hängt am **Discord-Konto**, nicht am Namen. Es gibt keinen Interceptor dafür: ein **neuer Abschlusspfad muss
  `IBewerbungssperreService.BanAsync` selbst rufen** — und zwar vor `broadcaster.Report`, sonst liest das
  Sperr-Panel den Zustand davor. Entscheidungs-Methoden tragen `Permission.RequireRecruitingDecision`
  (Schreib- **vor** Rang-Prüfung), weil die Sperre ein Folge-Write ist, dessen Fehlschlag nur geloggt wird.
- **Bewerbungs-Platzhalter sind groß-/kleinschreibungsabhängig.** `BewerbungTemplateRenderer` matcht `\bNAME\b`
  case-sensitiv; aus `NAME` ein `Name` zu machen schaltet die `███████`-Schwärzung für jede daraus gebaute
  Nachricht still ab. `TextAssistService` lehnt eine NOOSEI-Korrektur deshalb hart ab, wenn Anzahl **oder**
  Schreibweise dieser Tokens abweicht (Kontext `RecruitingTemplate`).
- **`NOOSE-Website/BuildNumber.txt` erhöht sich automatisch bei jedem echten Build** (`dotnet build`/`watch`/`publish`, MSBuild-Target in der `.csproj`; IDE-Design-Time-Builds sind ausgenommen) und wird als `1.0.<Zahl>` auf `/einstellungen?tab=status` angezeigt. Datei ist **gitignored** (`.gitignore` Zeile 386) → taucht nie in `git status` auf und wird nicht mitcommittet; die Prod-Nummer wächst allein über `deploy.ps1`.
- **`graph.js`-JSON-Keys = englische CLR-Typnamen** (`nameof`), nicht die deutschen Display-Namen; C#- und JS-Map müssen synchron bleiben.
- **Connection-Strings nie in `appsettings.json`** — nur User-Secrets/Env.
- **Discord-Redirect** muss im Developer-Portal als `https://noose.info/signin-discord` registriert sein.
- **Score-Writes gehen via `ExecuteUpdateAsync`**, um den Audit-Interceptor zu umgehen (sonst stempelt jeder Recompute `GeaendertAm` → bricht die Aktualitäts-Ampel). **Bulk-/Raw-SQL umgeht generell die Interceptors** → `Permission.RequireWriteAccess` dann explizit aufrufen. Dokumentierte Ausnahmen von dieser Guard-Pflicht: `FactionRecency.StampAsync`, `PublicWantedService.CountViewAsync`, `TipPriorityService` und `RecomputeConfirmedTipsAsync` — abgeleitete Werte hinter einem schon abgesicherten Schreibpfad.
- **Fraktions-Aktualität hängt NICHT an `GeaendertAm`**, sondern an vier eigenen Stempeln auf `Fraktionen`
  (`MitgliederAktualisiertAm`, `BestaendeAktualisiertAm`, `AktivitaetenAktualisiertAm`, `DoksAktualisiertAm`).
  Der **älteste** davon bestimmt die Ampel; Stammdaten-Edits setzen sie nicht zurück. Alles zentral in
  `Services/FactionRecency.cs` (`Reference`/`Oldest`/`Facets`/`ReferenceBefore`/`StampAsync`) — Lesepfade
  (Liste, Karte, Druck, Dashboard, Statistik, `LeadService`) gehen ausschließlich darüber. Neuer Schreibpfad
  auf Mitglieder/Bestände/Aktivitäten/Doks ⇒ `FactionRecency.StampAsync` **nach** dem `SaveChanges` aufrufen
  (Raw-Update, damit der Stempel selbst kein `GeaendertAm`/Audit-Eintrag erzeugt).
- **Nachvollziehbarkeit:** Ein Schreibpfad, der den Interceptor umgeht (`ExecuteUpdate/Delete`/Raw-SQL) **oder** eine nicht-`IAuditable`-Zuordnung ändert (z. B. `TagMapping`), muss selbst eine Zeile via `ManualAudit.Row(entityType, entityId, …)` schreiben — gegen die **Akte** geloggt (⇒ Zeitstrahl + Chronik + Protokoll), bei reinen Config-Aktionen gegen einen Config-Typ (nur Protokoll). `ChangesJson` folgt der `{Feld:[alt,neu]}`-Form (`ManualAudit.Change`), sonst rendert `AuditDisplay.Parse` nichts.
- **Neue Kind-/Anhang-Tabelle einer Akte** (auditiert, aber unsichtbar auf dem Zeitstrahl) → Fall in `TimelineService.AuditSourceAsync` (Fan-out per FK bzw. polymorph über `EntityType/EntityId`) **und** einen Titel in `TimelineDisplay.MapAudit` ergänzen — sonst erscheint sie generisch als „Akte geändert" oder gar nicht.
- **Bewerbungs-Anschreiben nie auf `{{...}}` „normalisieren"** — `BewerbungTemplateRenderer` schwärzt `\bNAME\b` zu `███████`, damit der Agent gegenüber Bewerbern anonym bleibt; `{{Agent}}` würde stattdessen den Codename ausliefern. `DocumentTemplates` ist dieselbe Tabelle für Bibliothek **und** Bewerbung → Consumer müssen nach `Category` (`RecruitingSeeder.TemplateCategory`) filtern.
- **`SearchNavigation.For` gibt `null` statt zu raten.** Der alte `_ => "/personen/{id}"`-Fallback öffnete für einen
  Kommentar an einer Fraktion eine *Personenakte mit der Fraktions-Id* — eine falsche Akte, lautlos. Ein Treffer ohne
  Route ist nicht klickbar; wer eine Route braucht, prüft `SearchCatalog.IsRoutable`.
- **Neue Suchkategorie = eine `SearchCatalog`-Zeile + ein `ISearchProvider` + eine Registrierungszeile.** Fehlt eins,
  schlägt `SearchCatalogTests`/`SearchCoverageTests` fehl. Der `Assistant`-Trait wird **zusammen mit dem Provider**
  gesetzt; `NooseiRecordTypes` leitet Name, Plural und die durchsuchbare Menge daraus ab, eine zweite Tabelle gibt
  es nicht mehr. Alle Kategorien tragen den Trait — er entscheidet nur, ob das Modell **eingrenzen** darf;
  Treffer daraus kamen bei einer unbeschränkten `suche_akten` ohnehin an, nur ohne `id=` zum Weiterverfolgen.
- **`SearchIndexBackfillWorker.Version` hochzählen**, wenn `SearchIndexProjection` einen Typ dazubekommt. Sonst
  bekommen Bestandsinstallationen **null** Index-Zeilen für den neuen Typ, und die phonetische Suche wirkt „flaky".
- **Suchtests konstruieren mit `MaxConcurrency = 1`** (`SearchTestHost`): `SqliteTestContext` gibt jedem Context
  dieselbe offene `SqliteConnection`, und zwei gleichzeitige Kommandos darauf sind undefiniert.
- **Stale Docs:** `Authorization/README.md` und `Infrastructure/README.md` sind veraltete „Phase 0"-Stubs; viele `<see cref>`-Tags zeigen auf alte deutsche Typnamen. Quelle ist der Code, nicht die READMEs.

## NOOSEI (KI-Integration)

**Ein einziger Weg zum Modell:** `INooseiGateway.AskAsync` prüft Recht und Kontingent, führt aus,
bucht **genau einmal** ab und protokolliert. `ILlmService` ist reiner Transport und bleibt DB-frei.
Werkzeuge (`Services/Llm/Tools/`) lesen nur, filtern **jeder für sich** über den `ViewerScope`, und
`NooseiToolResult.NotFound()` ist für „existiert nicht" und „darfst du nicht sehen" absichtlich
identisch. **Echtes Geld sieht ausschließlich der KI-Eigner**, alle anderen rechnen in Kontingent-Token.

→ Werkzeug-Register, Sichtbarkeits-Gates, Kontingent-Mathematik, Betriebsspalten und Editor-Korrektur:
**[`claude-memory/noosei.md`](claude-memory/noosei.md)** — lies das, bevor du dort etwas änderst.

## Öffentlicher Bereich

Vollständig gebaut, Phase 1–18 aus `PublicPlan.md`: Bürgerkonten, Modulgerüst, redaktionelle Seiten,
Fahndung samt Ausbau und Sachfahndung, Kopfgeld, Bürgerhinweise mit Triage/Übernahme, Belohnung,
Ticket-Chat, Bürger-Vorlagen, Organisationsprofile, Einspruch, Presse, Warnungen, Gesetzesauszüge,
Lageberichte, Gefahrenlage-Ampel, öffentliche Zahlen, die Suchanbindung, Führungsprofile und die
**Ergreifungsmeldung** (ein Bürger meldet, dass er eine gesuchte Person selbst gestellt hat).

**Vier Regeln gelten überall dort — der Rest steht in `claude-memory/oeffentlich-*.md`:**

- **Ein Bürger ist ein `Agent` mit `Status = Civilian`**, nicht mit Rechten. Zugang (`MayUseCitizenPortal()`)
  und Status (`IsCitizen()`) sind zwei verschiedene Fragen.
- **Modul-Aus wirkt im Service, nicht in der UI** (`RequireEnabledAsync` wirft); der Not-Aus schlägt jedes
  Einzelmodul, ohne eine gespeicherte Wahl zu verändern. Publizieren braucht ein lebendes Modul,
  ***De*publizieren nie.**
- **`IgnoreQueryFilters()` gilt für die ganze Kompilierung, nicht für den Operanden.** Der
  Unterdrückungsgürtel ist deshalb immer eine **zweite Abfrage**, nie eine Unterabfrage — sonst geht eine
  soft-gelöschte Akte anonym live (nachgemessen, nicht vermutet).
- **Schreib-Guard vor Rang-Guard.** Sonst prägen Nur-Lese-Aufsicht und Demo-Principal Aktenzeichen und
  Foto-Kopien, bevor der `ReadOnlyBarrierInterceptor` das Speichern verweigert.

→ Einstieg immer über **[`claude-memory/oeffentlich-grundlagen.md`](claude-memory/oeffentlich-grundlagen.md)**,
dann die Themendatei aus der Tabelle oben.

## Domänen-Glossar

| Begriff | Bedeutung |
|---------|-----------|
| **NOOSE** | National Office of Security Enforcement — fiktive Geheimdienst-Behörde |
| **Personenakte / Person** | Zentrale Akte je Person (Tabelle `Personen`) |
| **Personen-Dok** | Verhör-/Maßnahmen-Protokoll an einer Person; Ausgang Spritze/offiziell/erschossen/laufen |
| **Steckbrief** | Erweiterte Person-Daten (Aliase, Telefon, Fahrzeuge, Waffen) |
| **Fraktion / Partei / Personengruppe** | Gruppierungs-Akten mit eigenen Mitgliedern/Rängen/Konflikten |
| **Einstufung / Classification** | Prüffall → Verdachtsfall → Gesichert staatsgefährdend |
| **Verschlusssache (VS) / `IsClassified`** | Führungs-only-Sichtbarkeit; VS-Stufen für Doks: None/Leadership/TRU/HRB |
| **Personalakte** | Dienstgrad-Verlauf, Notizen, Beförderungen, Ausbildungsmodule je Agent |
| **Beförderung** | Antrags-/Entscheidungs-Workflow (`AgentPromotionRequest`) |
| **Taskforce** | Einheit mit Genehmigung; Scope innerbehördlich/überbehördlich |
| **EHK-Score / Bedrohungs-Score** | Automatischer Gefährdungswert (0–100) je Fraktion/Person, siehe `AlgoPlan.md` |
| **Aktenzeichen** | Menschenlesbare ID, z. B. `NOOSE-P-2026-0001` |
| **Wartungsmodus** | In `MainLayout.razor` erzwungen (keine Middleware); Admins behalten Zugriff |
| **Klarname / Codename** | Realname (führungs-/nicht-OnlyReader-only) vs. Dienst-Codename |
| **TRU / HRB** | Tactical Response Unit / Human Resources Branch — rangunabhängige Flags + VS-Stufen |

## Weiterführende Docs

- `Plan.md` — Phasenplan (Status, Datenmodell, Rechte-Matrix, Glossar)
- `Features.md` — kompakte Funktionsübersicht
- `AlgoPlan.md` — Spezifikation des EHK-/Bedrohungs-Scores (S1–S4 Fraktion, P1–P5 Person)
- `PublicPlan.md` — Öffentlicher Bereich (Fahndung/Kopfgeld/Hinweise/Ticket-Chat/CMS), 16 Phasen; **alle gebaut**
- `DEPLOYMENT.md` — Server-Setup (nginx → Kestrel `127.0.0.1:5000` → MariaDB), systemd, Troubleshooting
- `GoalOfTheSite.txt` — Original-Spec (Ränge, Feldlisten, Einstufungs-Stufen)
- `CODE_REVIEW_TODO.md` — bekannte Tech-Debt-/Review-Findings
- `claude-memory/` — Detailwissen je Bereich (Tabelle oben). **Warum** eine Regel existiert, nicht nur dass sie gilt.
