# CLAUDE.md — NOOSE-Website

Zentrale Akten-/Intelligence-Datenbank für die **NOOSE** (National Office of Security Enforcement),
eine fiktive Behörde auf einem FiveM/GTA-RP-Server. Die Seite ersetzt verstreute Discord-Threads
durch eine zentrale, durchsuchbare, bidirektional verlinkte Akten-Datenbank: pro Person und pro
Fraktion eine Akte, in der alles zusammenläuft. **Codebase ist anglisiert (englische Identifier),
aber Domänen-Vokabular, UI-Texte, Kommentare und Planungsdocs sind Deutsch.** Live: https://noose.info

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

- **Kein Test-Projekt** im Repo — `dotnet test` existiert nicht.
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
| `Data/` | `AppDbContext`, `Entities/<Domain>/`, `Migrations/` (~51, Phase-Präfix) |
| `Models/` | DTOs/View-Models, `Enums/`, `Abstractions/` (Marker-Interfaces) |
| `Services/` | Business-Logik **+ die echte Authorization-Durchsetzung**; Subordner `Graph/`, `Statistics/`, `Threat/` |
| `Authorization/` | Policies, Requirements, Handler, `ClaimsPrincipal`-Extensions |
| `Infrastructure/` | Interceptors, File-Storage, Audit, Background-Worker, CurrentUser, Broadcaster |

- **`Program.cs`** ist Composition-Root (Top-Level-Statements): alle DI-Registrierungen inline, nach Build-„Phase" gruppiert/kommentiert.
- **Render-Mode** wird pro Seite in `App.razor` gesetzt: `InteractiveServer`, außer `[ExcludeFromInteractiveRouting]` (Error, NotFound, Login, Pending, Blocked, Legal) → statisch.
- **Culture global auf de-DE** fixiert (`UseRequestLocalization` + `CultureInfo.DefaultThread*`).
- **Middleware-Reihenfolge** (load-bearing): `UseForwardedHeaders` (zuerst, vertraut nur Loopback/nginx) → `RequestLocalization` → (nur Prod) `ExceptionHandler`+`HSTS` → `StatusCodePagesWithReExecute("/not-found")` → `HttpsRedirection` → `Authentication` → `Authorization` → `RateLimiter` → `Antiforgery` → `MapStaticAssets` → `/health` → `MapRazorComponents<App>` → `Map*Endpoints`-Gruppen.
- **SignalR Hub:** `MaximumReceiveMessageSize = 5 MB` (für den RichTextEditor, der volles HTML über SignalR streamt — nicht zurücksetzen).
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
- **Statische Helfer in `Services/`** (NICHT DI-registriert): `Permission`, `Visibility`, `ClassificationHelper`, `TextSimilarity`, `RecordsReference`, `MentionParser`, `HtmlCleanup`. Geteilte Logik dorthin extrahieren statt kopieren.
- **Globale Suche** (`SearchService`) deckt alle Record-Typen + Inhalte ab; nutzt In-Memory-Levenshtein (`TextSimilarity`), weil MySQL/Pomelo keine Edit-Distance übersetzt.
- **Maintenance/Banner/Theme/Logo:** `SystemSettingService` über Key/Value-Tabelle, 10s `IMemoryCache`. Logo/Uploads liegen **außerhalb wwwroot** unter `App_Data/uploads`, ausgeliefert über autorisierte Minimal-API-Endpoints.

## Authorization, Ränge & Rollen

Drei orthogonale Achsen: **(1) Rang** (`Models/Enums/Rank.cs`, int-backed `JuniorAgent=1 … Director=6`), **(2) Boolean-Flags** auf `Agent` (`IsAdmin`, `IsTRU`, `IsHRB`, `IsTeamLead`), **(3) Policies** (kombinieren Rang+Flags).

- **Berechtigungslogik existiert an genau zwei Stellen** — nirgends sonst rohe Claim-Checks:
  - `Authorization/AgentPrincipalExtensions.cs` — `ClaimsPrincipal`-Extensions (`IsAdmin`, `IsLeadership`, `IsOnlyReader`, `MayWrite`, `MayRealNameSee`, `MayHighestClassification`, …) für UI/Policies/Read-Gates.
  - `Services/Permission.cs` — statische `Require*`-Guards für Service-Writes.
- **Führung (Leadership)** = Rang ≥ `SupervisorySpecialAgent(4)` **oder** Admin. **`HöchsteEinstufung`** ≥ `SeniorSpecialAgent(3)`, **`BeförderungEntscheiden`** ≥ `DeputyDirector(5)`.
- **Admin = Boolean-Flag** (`Agent.IsAdmin` / Claim `noose:admin`), **nicht** der Rang und **nicht** die geseedete Identity-Rolle „Admin" (die ist ungenutzt). Admin short-circuited jedes `RankRequirement`.
- **Nur-Lese-Aufsicht (`OnlyReader`)** = `IsTeamLead && !IsAdmin` (abgeleitet, kein Flag): liest alles (inkl. VS), schreibt **nichts** (vom `ReadOnlyBarrierInterceptor` hart vetoed), sieht **nie** Klarnamen. `IsTeamLead` allein gewährt sonst keine Rechte; TeamLeads sind RP-weit unsichtbar.
- **Claims werden beim Login** in den Cookie geschrieben (`AgentClaimsPrincipalFactory`) → keine DB-Hits pro Request. Rang-/Rollen-/Status-Änderung rotiert den `SecurityStamp` (`Save(agent, newStamp: true)`) → erzwingt Re-Login (`SecurityStampValidator` revalidiert alle 30s).
- **Neue Policy anlegen:** Konstante in `Policies.cs` → registrieren in `AuthorizationRegistration.AddNooseAuthorization` (`RankRequirement` für Rang-Gate **oder** `RequireAssertion(ctx => ctx.User.SomeExtension())`) → ggf. Extension in `AgentPrincipalExtensions.cs`. **Policy-Strings nie hardcoden** — immer `Policies.*`.
- **Account-Flow:** Discord-Login → `Agent` mit `Status=Pending` → Freigabe durch Führung/Admin (`AgentManagementService.ReleaseAsync`) setzt `Active` + Rang + Flags. Bootstrap-Admins via `Bootstrap:AdminDiscordId(s)`.
- **Zwei VS-Achsen:** `Classification` (Einstufung Person/Fraktion: `ReviewCase`/Prüffall → `SuspicionCase`/Verdachtsfall → `SecuredStateThreatening`/Gesichert staatsgefährdend) **und** `DocumentClassification` (Bibliotheks-VS-Stufe: `None`/`Leadership`/`Tru`/`Hrb`). VS-Sichtbarkeit wird **server-seitig** über `DocumentViewerScope.CanSee` durchgesetzt, nicht über die `Classified`-Policy (reserviert/ungenutzt).
- **Keine DoJ/LSPD/LSMD-Accounts/-Ränge** — jeder User ist ein NOOSE-`Agent`. Partner-Lesezugriff (Phase 9) ist noch nicht gebaut.

## UI / Blazor-Komponenten

- **Ein Feature-Ordner je Bereich** unter `Components/Pages/` (Account, Admin, Board, Calendar, Cases, Factions, Graph, Groups, Jobs, Laws, Operations, OrgChart, Parties, People, Personnel, Search, Statistics, Taskforces, Watchlist). Pro Feature: `*List`/`*New`/`*Edit`/`*Detail`/`*Print`/`*Trash` + `Shared/`. Cross-Feature → `Components/Common/Shared/`.
- **Deutsche Routen:** `/personen`, `/fraktionen`, `/vorgaenge`, `/aufgaben`, `/operationen`, `/parteien`, `/personengruppen`, `/taskforces`, `/kalender`, `/organigramm`, `/statistik`, `/brett`, `/gesetze`, `/suche`, `/graph`. CRUD-Subroutes `/{feature}/neu`, `/{Id}`, `/{Id}/bearbeiten`, `/{Id}/druck`, `/{feature}/papierkorb`.
- **Globales `[Authorize(Policy = Policies.ActiveAgent)]` in `_Imports.razor`** → jede neue Seite ist standardmäßig auth-pflichtig. Öffentliche Seiten brauchen explizit `[AllowAnonymous]` (Login, Pending, Blocked, Error, NotFound, Legal).
- **Strengere Seiten:** `@attribute [Authorize(Policy = Policies.LeadershipPage|AdminPage|HighestClassificationPage)]`. Feingranular per `<AuthorizeView Policy="@Policies.X" Context="...">` (explizite `Context`-Namen bei Verschachtelung). `*Page`-Policies lassen bewusst auch den `OnlyReader` rein — nicht zu Rang-Requirements „vereinfachen".
- **Kein Code-Behind** (`*.razor.cs` existiert nicht) — Logik in inline `@code`. Private Felder `_camelCase`.
- **Dark-Mode hardcoded** (`IsDarkMode="true"`, nur `PaletteDark` in `Theme/NooseTheme.cs`). Admin-Akzentfarben (Phase 7) zur Laufzeit über `/admin/system` (`NooseTheme.WithColours(...)`).
- **JS-Interop** ist self-hosted + lazy-loaded je Seite mit `?v=`-Cache-Buster: `graph.js` (vis-network), `kalender.js` (FullCalendar), `richtext.js` (Quill 1.3.7); `app.js` (Strg+K Command-Palette) ist das einzige global geladene Modul. Interop-Komponenten: `IAsyncDisposable`, Import in `OnAfterRenderAsync(firstRender)`, `[JSInvokable]`-Callbacks, alles in `try/catch` gegen `JSDisconnectedException`.

## Wichtige Gotchas

- **`dotnet tool restore` vor jedem `dotnet ef`** — `dotnet-ef` ist lokal-gepinnt (9.0.17), nicht global.
- **EF/Identity nicht auf 10.x** (Pomelo-9-Kollision).
- **Vor `dotnet ef migrations add` den Dev-Server stoppen** (bin-Lock), dann neu bauen.
- **`App_Data` beim Deploy nie löschen** — enthält Uploads **und** Data-Protection-Keys (`App_Data/keys`); Verlust loggt alle User bei jedem Restart aus. `deploy.ps1` schließt `App_Data` explizit vom Löschen aus.
- **Deploy nutzt `tar`, nie `Compress-Archive`** (packte früher 0-Byte-Dateien → kaputtes MudBlazor-CSS).
- **`TZ=Europe/Berlin` in `/etc/noose/noose.env`** nötig — Blazor Server rechnet `ToLocalTime()` in der Server-TZ; ohne TZ sind alle Zeiten (inkl. 20-Min-„Tot"-Fenster) verschoben. `TimeZoneInfo.Local` ist prozess-gecached → Restart nach Änderung.
- **`?v=` bumpen bei JS-Modul-Edits** (`graph.js?v=8`, `kalender.js?v=6`, `richtext.js?v=4`, `app.js?v=2`) — dynamische ES-Imports umgehen Blazors Asset-Fingerprinting.
- **`graph.js`-JSON-Keys = englische CLR-Typnamen** (`nameof`), nicht die deutschen Display-Namen; C#- und JS-Map müssen synchron bleiben.
- **Connection-Strings nie in `appsettings.json`** — nur User-Secrets/Env.
- **Discord-Redirect** muss im Developer-Portal als `https://noose.info/signin-discord` registriert sein.
- **Score-Writes gehen via `ExecuteUpdateAsync`**, um den Audit-Interceptor zu umgehen (sonst stempelt jeder Recompute `GeaendertAm` → bricht die Aktualitäts-Ampel). **Bulk-/Raw-SQL umgeht generell die Interceptors** → `Permission.RequireWriteAccess` dann explizit aufrufen.
- **Stale Docs:** `Authorization/README.md` und `Infrastructure/README.md` sind veraltete „Phase 0"-Stubs; viele `<see cref>`-Tags zeigen auf alte deutsche Typnamen. Quelle ist der Code, nicht die READMEs.

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
- `DEPLOYMENT.md` — Server-Setup (nginx → Kestrel `127.0.0.1:5000` → MariaDB), systemd, Troubleshooting
- `GoalOfTheSite.txt` — Original-Spec (Ränge, Feldlisten, Einstufungs-Stufen)
- `CODE_REVIEW_TODO.md` — bekannte Tech-Debt-/Review-Findings
