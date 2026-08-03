# AGENTS.md — NOOSE-Website

Central records/intelligence database for **NOOSE** (National Office of Security Enforcement),
a fictional agency on a FiveM/GTA-RP server. The site replaces scattered Discord threads with a
central, searchable, bidirectionally linked case-file database: one file per person and per
faction, where everything converges. Live: https://noose.info (demo: https://demo.noose.info).

**Language split:** identifiers and code comments are English; domain vocabulary, UI text,
routes, DB column names and planning docs are German. Deeper detail lives in `CLAUDE.md`;
this file covers the rules most likely to trip up an agent.

## Tech stack

- **.NET 10** (`net10.0`), Blazor Web App with **Interactive Server only** (SignalR) — no WebAssembly/Auto.
- **MudBlazor 9.5**, dark mode only ("anthracite + cyan").
- **EF Core 9** via **Pomelo.EntityFrameworkCore.MySql 9.0** → local MariaDB/XAMPP, prod MySQL 8.0.
- **ASP.NET Core Identity** (user entity = `Agent`) + **Discord OAuth** (`AspNet.Security.OAuth.Discord`).
- Self-hosted frontend libs under `wwwroot/lib`: Quill 1.3.7 (rich text), vis-network (graph), FullCalendar.
- Solution file is `NOOSE-Website.slnx` (XML format), two projects: `NOOSE-Website/` and `NOOSE-Website.Tests/`.

## Critical constraints

- **EF/Identity stays on 9.x** — Pomelo.EntityFrameworkCore.MySql 9.0.0 is incompatible with EF Core 10. The `net10.0` runtime runs EF Core 9 packages (`9.0.16` line). Never upgrade Identity/EF packages to 10.x.
- **EF tools are local** — `dotnet tool restore` before every `dotnet ef` invocation. Tool version pinned at 9.0.17 in root `dotnet-tools.json`.
- **Tests exist** — `dotnet test NOOSE-Website.slnx` (xunit + NSubstitute, SQLite integration helpers in `NOOSE-Website.Tests/Infrastructure/`). No bUnit: keep testable logic in the service layer.

## Commands (run from repo root)

```bash
dotnet build NOOSE-Website.slnx
dotnet run   --project NOOSE-Website/NOOSE-Website.csproj   # → http://localhost:5174
dotnet watch --project NOOSE-Website/NOOSE-Website.csproj run
dotnet test  NOOSE-Website.slnx

# EF Migration (stop dev server first — bin lock)
dotnet tool restore   # required before every dotnet ef
dotnet ef migrations add Phase<N>_<Name> --project NOOSE-Website/NOOSE-Website.csproj
# 'dotnet ef database update' is unnecessary — MigrateAsync() runs at app start

# Deploy to prod (64-bit PowerShell only)
.\deploy.ps1
# DB backup (mysqldump on server + scp download)
.\backup-db.ps1
```

## Project layout

Layers inside `NOOSE-Website/`:

| Folder | Contents |
|--------|----------|
| `Components/` | Razor pages + UI (thin). `Pages/<Feature>/`, `Layout/`, `Common/Shared/`, `Account/` |
| `Navigation/` | `NavCatalog`, `NavEntry`/`NavSection` (policy axis), `NavArea` (display axis), `LegacyRoutes`, `MergedPageSections`, `NavPersonalization` |
| `Data/` | `AppDbContext`, `Entities/<Domain>/`, `Migrations/` (Phase-prefixed, currently ~Phase 44) |
| `Models/` | DTOs/view-models, `Enums/`, `Abstractions/` (marker interfaces) |
| `Services/` | Business logic **+ the real authorization enforcement**; subfolders `Graph/`, `Statistics/`, `Threat/` |
| `Authorization/` | Policies, requirements, handlers, `ClaimsPrincipal` extensions |
| `Infrastructure/` | Interceptors, file storage, audit, background workers, CurrentUser, broadcasters, demo mode |
| `Theme/` | `NooseTheme.cs` (dark palette, admin-tunable accent colours) |

`Program.cs` is the composition root: all DI registrations inline. `App.razor` sets the render
mode per page (`InteractiveServer`, except `[ExcludeFromInteractiveRouting]` pages: Error,
NotFound, Login, Pending, Blocked, Legal). Culture is pinned to de-DE. SignalR hub has
`MaximumReceiveMessageSize = 5 MB` (the rich-text editor streams full HTML — do not lower).
Background workers (`AddHostedService`): `FollowupDueWorker`, `ThreatScoreSweepWorker`,
`SituationReportWorker` — single host instance per DB only. Health check at `/health`.

## DB & EF

- **DbContext must use factory pattern** — always `IDbContextFactory<AppDbContext>` + `await using var db = await dbFactory.CreateDbContextAsync(ct)`. A scoped/shared DbContext causes "A second operation was started on this context" in Blazor Server.
- **One** `AppDbContext : IdentityDbContext<Agent>`; all Fluent config in `OnModelCreating` (no `IEntityTypeConfiguration` classes). Annotations only for `[Table]`/`[Column]` and `[NotMapped]`.
- **Interceptor registration order matters** in `OnModelCreating`: ReadOnlyBarrier → Audit → Watchlist.
- **Cross-cutting via marker interfaces** (`Models/Abstractions/`): `IAuditable` (Created/Modified stamped by the interceptor) and `ISoftDelete` (global query filter `!IsDeleted` applied by reflection). New entity → just implement the interface.
- Soft-delete (`ISoftDelete`) is the norm; trash queries use `IgnoreQueryFilters().Where(x => x.IsDeleted)`.
- DB columns are German, C# members are English (`Person.CaseNumber` → column `Aktenzeichen`, table `Personen`). FK relationships use `DeleteBehavior.Restrict` (no cascade; never cascade into the Identity `Agent` table).
- Polymorphic associations (sources, comments, tags, links, followups) use `(EntityType, EntityId)` string pairs — no real FK; the composite index is the fast path.
- Metadata scores (`ThreatScore`) use `ExecuteUpdateAsync` to bypass the audit interceptor. **Bulk/raw SQL bypasses all interceptors** → call `Permission.RequireWriteAccess` explicitly there.
- Case numbers (`NOOSE-P-2026-0001`) are race-safe via `CaseNumberCounter` in a transaction.
- The design-time factory (`AppDbContextDesignTimeFactory`) forces EF tools onto the local `DefaultConnection` → migrations can never hit production.

## Services layer

- **Interface-first:** every DI service is `I<Name>Service` + `<Name>Service`, `AddScoped`, primary constructors, trailing `CancellationToken cancellationToken = default` on every public async method.
- **Authorization is enforced in the Services layer**, not just the UI. Write methods take `ClaimsPrincipal actor` and call a static `Permission.Require*` guard as first statement. Visibility is centralized in static `Visibility`/`*Visibility`/`RecordsReference`.
- **Live updates via singleton broadcasters:** scoped service writes the row, then calls the singleton (`NotificationBroadcaster`, `TaskforceChatBroadcaster`, `SharesBroadcaster`, `AcknowledgmentBroadcaster`, `WatchlistDispatcher`) to push to connected circuits.
- **Static helpers in `Services/`** (NOT DI-registered): `Permission`, `Visibility`, `ClassificationHelper`, `TextSimilarity`, `RecordsReference`, `MentionParser`, `HtmlCleanup`, `TrashProjection`. Extract shared logic there instead of copying.
- **Three separate token systems, never mix:** `PlaceholderService` (`{{Name}}` … document/activity/personnel templates) · `BewerbungTemplateRenderer` (bare `NAME`/`BEWERBER`/…, redacts the agent name — never "normalize" these to `{{...}}`) · `MentionParser` (`@{Type:GUID}` → `<MentionText>`). Placeholders expand only when a template is *applied*, never on save.
- `ITrashService` fans the global trash out over all record services (same `GetTrashAsync`/`RestoreAsync` signature); registering a new deletable type = one line in `TrashService` + one `TrashProjection` method.

## Authorization

Three orthogonal axes: **rank** (`Models/Enums/Rank.cs`, int-backed `JuniorAgent=1 … Director=6`),
**boolean flags** on `Agent` (`IsAdmin`, `IsTRU`, `IsHRB`, `IsTeamLead`), **policies**
(combine rank + flags).

- Permission logic exists in exactly two places — nowhere else raw claim checks:
  - `Authorization/AgentPrincipalExtensions.cs` — `ClaimsPrincipal` extensions for UI/policies/read-gates.
  - `Services/Permission.cs` — static `Require*` guards for service writes.
- All `.razor` files import `[Authorize(Policy = Policies.ActiveAgent)]` globally via `_Imports.razor`. Pages needing anonymous access must explicitly add `[AllowAnonymous]`.
- Leadership = rank ≥ `SupervisorySpecialAgent(4)` **or** admin. Admin is a boolean flag (`Agent.IsAdmin` / claim `noose:admin`), not a rank or Identity role. The seeded "Admin" role is unused.
- `OnlyReader` = `IsTeamLead && !IsAdmin` — can read classified content but is hard-vetoed from writes by `ReadOnlyBarrierInterceptor`, and never sees real names. `*Page` policies deliberately let the OnlyReader in — do not "simplify" them into rank requirements.
- Claims are written into the cookie at login; rank/flag changes rotate the `SecurityStamp` to force re-login.
- New policy: constant in `Policies.cs` → register in `AuthorizationRegistration.AddNooseAuthorization` → maybe an extension in `AgentPrincipalExtensions.cs`. **Never hardcode policy strings** — always `Policies.*`.
- Account flow: Discord login → `Agent` with `Status=Pending` → release by leadership/admin sets `Active` + rank + flags. Bootstrap admins via `Bootstrap:AdminDiscordId(s)`.

## Value lists (Wertelisten)

- Auto-learned suggestion catalogs (`ProfileSuggestion`/table `SteckbriefVorschlaege`, 9 `SuggestionType`s) are editable under Settings → Wertelisten (`BaseDataPanel.razor`). Renames/deletes propagate to all records via `ExecuteUpdate`/`ExecuteDelete` in `ProfileSuggestionService` — bypasses audit and watchlist on purpose, and also the SaveChanges read-only barrier, so these methods call `Permission.RequireWriteAccess` explicitly. `AgentActivity.Kind` is a distinct-based list without a catalog, managed the same way.
- Code-defined enum labels (ranks, classifications, …) are overridable in DB (`EnumLabelOverride`/table `WertelistenLabels`) via the static `EnumLabelText` store, warmed in `Program.cs` after `MigrateAsync` and refreshed by `ValueListLabelService`. Display classes expose `DefaultName` (code) and `Name` (override-aware). Enum values themselves stay code-owned — ranks drive authorization by ordinal.

## UI conventions

- **No code-behind files** (`*.razor.cs`) — all logic in `@code` blocks. Private fields are `_camelCase`.
- German routes (`/personen`, `/fraktionen`, etc.) and German UI text, but English identifiers and code comments.
- New/Edit live in ONE file (`*Editor.razor` with two `@page` directives). Blazor recycles the instance on route switch → load data in `OnParametersSetAsync` with a `_loadedId` guard, never in `OnInitializedAsync`.
- Since V1.5 many pages are merged into six hub pages with `RecordSectionRail` (`/einstellungen`, `/papierkorb`, `/fahndung`, `/abmeldungen`, `/bewerbungen`, `/statistik`). Removed routes live on in ONE shell: `Components/Common/Navigation/LegacyRouteRedirect.razor` + `Navigation/LegacyRoutes.cs`. **When removing a route: add the `@page` to the shell only when the old file is deleted in the same step** — two components on one route throw at *runtime*, not compile time. After route changes, actually start the app, don't just build.
- Reusable building blocks in `Components/Common/Shared/` (`PageHeader`, `EmptyState`, `StatTile`, `RecordSectionRail` + `RecordSection`, `QueryState`) — check there before building a new page.
- No `MudTabs` with URL sync inside a `RecordSectionRail` (`TabUrlState.ParameterName` is hardcoded `"tab"` → query collision); flatten sections instead.
- Dark mode hardcoded (`IsDarkMode="true"`); accent colours at runtime via `/einstellungen?tab=system`.
- JS interop is self-hosted + lazy-loaded per page with `?v=` cache busters (`graph.js`, `kalender.js`, `richtext.js`; `app.js` is the only global module). Interop components: `IAsyncDisposable`, import in `OnAfterRenderAsync(firstRender)`, `[JSInvokable]` callbacks, all in `try/catch` against `JSDisconnectedException`. **Bump the `?v=` on JS edits** — dynamic ES imports bypass Blazor's asset fingerprinting.
- `graph.js` JSON keys are English CLR type names (`nameof`), not German display names; C# and JS maps must stay in sync.

## Code style

- Comments **English only**, inline `//`, 2–3 words, describe the *why*. `catch { }` blocks get `/* best effort */` or `/* ignore */`. XML `/// <summary>` one short line. No block comments, no "Phase X" references in comments.
- Services: interface + implementation, `AddScoped`, primary constructors (see Services layer above).
- Match surrounding conventions; DB columns German, C# members English.

## Testing

- Test project `NOOSE-Website.Tests` (xunit + NSubstitute, `net10.0`): `dotnet test NOOSE-Website.slnx`.
- Helpers in `NOOSE-Website.Tests/Infrastructure/`: `SqliteTestContext` (in-memory SQLite + full schema, exposes `IDbContextFactory<AppDbContext>`), `Seed.*` (entity factories), `ClaimsPrincipalBuilder` (rank/flags/claims), `TestParallelization` (cross-collection parallelism disabled — shared in-memory connections are not parallel-safe).
- Service integration tests in `Services/Integration/` run against `SqliteTestContext` with NSubstitute collaborators (`ICaseNumberService` is stubbed — the real one uses MySQL-only SQL).
- **No bUnit** — `.razor` components are not testable; keep testable logic in the service layer.
- Coverage uses the built-in Microsoft Code Coverage engine (coverlet cannot instrument .NET 10 assemblies): `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --collect "Code Coverage;Format=cobertura"`, summarize with `NOOSE-Website.Tests/coverage/coverage-summary.py`.

## Secrets & config

- `appsettings.json` has empty placeholders only. Secrets come from User-Secrets (`dotnet user-secrets`, UserSecretsId `d41f8a93-2c7b-4e16-9a55-0b3e7c1f6d28`) locally or env vars (`/etc/noose/noose.env`) in prod.
- Required keys: `ConnectionStrings:DefaultConnection` (or `:ProductionConnection`), `Authentication:Discord:ClientId` + `:ClientSecret`, `Bootstrap:AdminDiscordId`.
- Prod env vars use double-underscore separators: `ConnectionStrings__ProductionConnection`.
- `DatabaseConnectionResolver` probes `ProductionConnection` (5 s reachability), falls back to `DefaultConnection` → same build runs locally and on the server.
- **Never put connection strings or secrets in `appsettings.json`.**

## Deployment

- `deploy.ps1` from **64-bit Windows PowerShell** (otherwise OpenSSH gets WOW64-redirected): publish → `tar` → `scp` → service swap (keeps `App_Data`) → `/health` check. Flags: `-SkipPublish`, `-NoPause`. Target: `root@195.20.225.12`, systemd service `noose`, `/var/www/noose` (nginx → Kestrel `127.0.0.1:5000` → MariaDB; see `DEPLOYMENT.md`).
- `backup-db.ps1`: consistent `mysqldump` on the server under `/root/backups` + download to the PC; server copies pruned by `-RetentionDays`, PC copies kept.
- **Deploy gotchas:**
  - `App_Data/` must never be deleted — contains uploads **and** Data Protection keys (`App_Data/keys`); loss logs out all users at every restart. `deploy.ps1` excludes it explicitly.
  - Use `tar` for packaging, never `Compress-Archive` (produces 0-byte files).
  - Server requires `TZ=Europe/Berlin` in env — Blazor Server timestamps use `ToLocalTime()`. `TimeZoneInfo.Local` is process-cached → restart after changes.
  - Discord OAuth redirect must be `https://noose.info/signin-discord` (plus `https://demo.noose.info/signin-discord` for the demo).

### Demo instance

- Second, **read-only** instance on the same server: `demo.noose.info`, port 5001, DB `noose_demo`, service `noose-demo`, app dir `/var/www/noose-demo`, env `/etc/noose-demo/noose-demo.env`. Setup guide: `DEPLOYMENT-DEMO.md`, helper script `setup-demo.ps1`.
- Same binary — difference is only DB + port + domain. Demo mode is a DB flag (`SystemSetting.DemoModeActive`) or forced via config `Demo:AutoSetup`; `DemoModeMiddleware` presents anonymous visitors as the read-only demo agent so the whole app is browsable without login (login/framework/health paths excluded).
- `deploy.ps1` has prod protection: refuses to deploy to a demo-flagged target unless told to; demo deploys use `-Server/-AppDir/-Service` overrides.

## Misc gotchas

- `NOOSE-Website/BuildNumber.txt` auto-increments on every real build (MSBuild target in the `.csproj`; design-time builds excluded) → shows up in `git status` after every local build, **must be committed along**.
- Stale docs: `Authorization/README.md` and `Infrastructure/README.md` are outdated "Phase 0" stubs; many `<see cref>` tags point at old German type names. Source of truth is the code, not the READMEs.

## Further docs

- `CLAUDE.md` — full conventions, architecture, domain glossary
- `Plan.md` — phase plan (status, data model, permission matrix)
- `AlgoPlan.md` — threat-score spec (S1–S4 faction, P1–P5 person)
- `DEPLOYMENT.md` / `DEPLOYMENT-DEMO.md` — server setup, prod & demo
- `GoalOfTheSite.txt` — original spec
