# AGENTS.md — NOOSE-Website

Full detail in `CLAUDE.md`. This file covers the rules most likely to trip up an agent.

## Critical constraints
- **EF/Identity stays on 9.x** — Pomelo.EntityFrameworkCore.MySql 9.0.0 is incompatible with EF Core 10. The `net10.0` runtime runs EF Core 9 packages (`9.0.16` line). Never upgrade Identity/EF packages to 10.x.
- **EF tools are local** — `dotnet tool restore` before every `dotnet ef` invocation. Tool version pinned at 9.0.17 in root `dotnet-tools.json`.
- **No tests exist** — `dotnet test` will fail; do not add test projects unless asked.

## Commands (run from repo root)
```bash
dotnet build NOOSE-Website.slnx
dotnet run   --project NOOSE-Website/NOOSE-Website.csproj
dotnet watch --project NOOSE-Website/NOOSE-Website.csproj run

# EF Migration (stop dev server first)
dotnet tool restore   # required before every dotnet ef
dotnet ef migrations add Phase23_<Name> --project NOOSE-Website/NOOSE-Website.csproj
# 'dotnet ef database update' is unnecessary — MigrateAsync() runs at app start

# Deploy (64-bit PowerShell only)
.\deploy.ps1
```

## Secrets
- `appsettings.json` has empty placeholders only. Secrets come from User-Secrets (`dotnet user-secrets`) locally or env vars (`/etc/noose/noose.env`) in prod.
- Prod env vars use double-underscore separators: `ConnectionStrings__ProductionConnection`.

## DB & EF
- **DbContext must use factory pattern** — always `IDbContextFactory<AppDbContext>` + `using var db = await dbFactory.CreateDbContextAsync(ct)`. A scoped/shared DbContext causes "A second operation was started on this context" in Blazor Server.
- **Interceptor registration order matters** in `OnModelCreating`: ReadOnlyBarrier → Audit → Watchlist.
- Soft-delete (`ISoftDelete`) is the norm; trash queries use `IgnoreQueryFilters().Where(x => x.IsDeleted)`.
- DB columns are German, C# members are English. FK relationships use `DeleteBehavior.Restrict` (no cascade).
- Metadata scores (`ThreatScore`) use `ExecuteUpdateAsync` to bypass the audit interceptor.

## Authorization
- Authorization is enforced in the **Services layer**, not just the UI. Write methods take `ClaimsPrincipal actor` and call `Permission.Require*` as first statement.
- All `.razor` files import `[Authorize(Policy = Policies.ActiveAgent)]` globally via `_Imports.razor`. Pages needing anonymous access must explicitly add `[AllowAnonymous]`.
- Admin is a boolean flag (`Agent.IsAdmin`), not a rank or Identity role. The seeded "Admin" role is unused.
- `OnlyReader` = `IsTeamLead && !IsAdmin` — can read classified content but is hard-vetoed from writes by `ReadOnlyBarrierInterceptor`.

## UI conventions
- **No code-behind files** (`*.razor.cs`) — all logic in `@code` blocks. Private fields are `_camelCase`.
- German routes (`/personen`, `/fraktionen`, etc.) and German UI text, but English identifiers and code comments.
- Comments must be **English only**, inline `//`, 2–3 words.
- JS modules use `?v=` cache busters — bump them on JS edits.

## Deploy gotchas
- `App_Data/` must never be deleted — contains uploads and Data Protection keys (loss logs out all users).
- Use `tar` for packaging, never `Compress-Archive` (produces 0-byte files).
- Server requires `TZ=Europe/Berlin` in env — Blazor Server timestamps use `ToLocalTime()`.
- Discord OAuth redirect must be `https://noose.info/signin-discord`.
