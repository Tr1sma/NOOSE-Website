# CLAUDE.md — NOOSE-Website

Zentrale Akten-/Intelligence-Datenbank für die **NOOSE** (National Office of Security Enforcement),
eine fiktive Behörde auf einem FiveM/GTA-RP-Server. Die Seite ersetzt verstreute Discord-Threads
durch eine zentrale, durchsuchbare, bidirektional verlinkte Akten-Datenbank: pro Person und pro
Fraktion eine Akte, in der alles zusammenläuft. **Codebase ist anglisiert (englische Identifier),
aber Domänen-Vokabular, UI-Texte, Kommentare und Planungsdocs sind Deutsch.** Live: https://noose.info

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
- **Middleware-Reihenfolge** (load-bearing): `UseForwardedHeaders` (zuerst, vertraut nur Loopback/nginx) → `RequestLocalization` → `PublicIndexingMiddleware` (noindex außerhalb der öffentlichen Routen; **vor** dem ExceptionHandler, damit re-executete Fehlerseiten den Header behalten) → (nur Prod) `ExceptionHandler`+`HSTS` → `StatusCodePagesWithReExecute("/not-found")` → `HttpsRedirection` → `Authentication` → `Authorization` → `RateLimiter` → `Antiforgery` → `MapStaticAssets` → `/health` → `MapRazorComponents<App>` → `Map*Endpoints`-Gruppen.
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
- **Bewusste Ausnahmen von `AgentSelection`** — nicht „aufräumen":
  `MentionService` (Partner bleiben erwähnbar) · `GetAllAsync`/`GetPendingAsync` (Admin-Roster mit eigenen
  Tabs für TL/Partner/Gekündigte) · `PartnerShareService.GetSelectablePartnersAsync` (die Inverse) · alle
  ID→Codename-Wörterbücher (müssen gekündigte Akteure in historischen Zeilen auflösen) ·
  `NotificationService.NotifyManyAsync`, `FollowupDueWorker`, `WatchlistFanout` (filtern eine **übergebene**
  Empfängerliste, kein Roster — Partner müssen Freigabe-/Chat-Benachrichtigungen weiter erhalten) ·
  `AnnouncementService` Bestätigungs-Zähler (ohne Status-Klausel, sonst fällt die Zeile eines gekündigten
  Agenten aus `TotalCount`) · `CounterIntelEventLoader` (braucht jeden User, er *erkennt* TL-Zugriffe).
- **Ein Picker, der eine gespeicherte Agenten-ID auflöst, muss auf `FindAsync` zurückfallen**, wenn die ID
  nicht mehr auswählbar ist (`FollowupDialog`, `ObservationDialog`). Sonst bleibt das Objekt `null` und der
  Speichern-Pfad *löscht die Zuordnung still* bzw. schreibt sie auf den Bearbeiter um. Auflösen **außerhalb**
  der Angebotsliste, damit niemand die Person neu auswählen kann.
- **`ITrashService`** fächert den globalen Papierkorb über alle 13 Record-Dienste auf. Alle haben dieselbe
  Signatur (`GetTrashAsync(ct)` / `RestoreAsync(id, actor, ct)`), also bindet Restore als **Methodengruppe** —
  kein generischer EF-Pfad, kein Bulk-SQL, Permission-Guard und Audit-Interceptor laufen weiter mit.
  Ein neuer löschbarer Typ wird in `TrashService` als eine Zeile registriert + eine `TrashProjection`-Methode.
- **Globale Suche:** `SearchService` ist nur noch **Orchestrator**. Je Kategorie ein `ISearchProvider`
  (`Services/Search/Providers/`, ~58), registriert über `AddSearchProviders()`. Der **`SearchCatalog`
  (`Services/Search/SearchCatalog.cs`) ist die einzige Wahrheit** für Label, Icon, Route, Trefferform, Traits,
  Facetten-Reihenfolge und das `suche_akten`-Enum — eine neue Kategorie ist **eine Zeile dort + ein Provider**.
  In-Memory-Levenshtein (`TextSimilarity`) bleibt, weil MySQL/Pomelo keine Edit-Distance übersetzt.
  - **Ein Provider schreibt nie ein Sichtbarkeits-Prädikat, er benennt eines** (`RecordVisibility.OnlyVisible`,
    `DocumentVisibility`, `TaskforceVisibility`, `InformantVisibility`, `MeetingVisibility.OpenIdsAsync`, …).
    Jede eigene Kopie driftet — genau so hat der alte Seitenindex-Pfad Partner- und Tag-Filter verloren.
  - **Zwei Wellen unter einem Wanduhr-Budget** (`SearchOptions`, Standard 8 s): erst die billigen Kategorien, dann
    die `Heavy`-Volltextscans. Läuft die Zeit ab, kommt zurück was fertig ist, und `SearchResults.Incomplete`
    **nennt die Lücke**. Disziplin wie `NooseiGateway`: ein Provider wirft nie, eine Welle wird immer zu Ende
    awaited, der Abbruch durch den Nutzer fällt durch.
  - **Trefferzahlen sind immer „so viele sichtbare Zeilen zeige ich", nie eine Aussage über den Bestand** —
    es gibt keine separate `CountAsync`. Volle Kategorie ⇒ `Capped` ⇒ „50+".
  - **Keine Vorfilter auf `/suche`.** Kategorien sind Ergebnis-Facetten (`SearchFacetBar`), rein clientseitig über
    die schon geholten Gruppen. `SearchCriteria.Categories` schränkt nur *server*-seitig ein und wird ausschließlich
    von `suche_akten` und „Rest nachladen" gesetzt; `SearchCriteria.Facet` ist die Anzeige.
  - **`SearchParentResolver`** löst polymorphe Kinder (Kommentar/Quelle/Wiedervorlage/Verknüpfung/Zusatzfeld/…) auf
    einen *sichtbaren* Elternteil auf. **Kein Default-Arm:** ein unbekannter Elterntyp versteckt das Kind — die
    Umkehrung von `Visibility.IsRecordVisibleAsync`, das einen unbekannten Typ als sichtbar beantwortet.
  - **Partner: zwei unabhängige Deckel**, beide im Orchestrator erneut behauptet — die 9-Typen-Grenze
    (`PartnerVisibility.IsReleasableType`) und die Rang-Allowlist (`IPartnerVisibilityPolicyService`, die vorher
    nur die Navigation gated hat). Es gibt **keinen** separaten Partner-Suchpfad mehr.
  - **`SearchCoverageTests` reflektiert über alle `DbSet`s:** jede Entität braucht einen Provider oder einen Eintrag
    in `SearchCatalog.NotSearchable` **mit Begründung**. Eine neue Tabelle macht den Build rot, bis jemand
    entschieden hat — das ist die eigentliche Garantie, nicht der Katalog. **Seit dem öffentlichen Bereich sind es
    zwei Wächter:** dieselbe neue Tabelle braucht zusätzlich einen Eintrag in `PublicVisibility`.
- **Maintenance/Banner/Theme/Logo:** `SystemSettingService` über Key/Value-Tabelle, 10s `IMemoryCache`. Logo/Uploads liegen **außerhalb wwwroot** unter `App_Data/uploads`, ausgeliefert über autorisierte Minimal-API-Endpoints.
- **Vier getrennte Token-Systeme, nie vermischen:** `PlaceholderService` (`{{Name}}`, `{{Aktenzeichen}}`, `{{Datum}}`, `{{Uhrzeit}}`, `{{Agent}}`, `{{Dienstgrad}}` — Dokument-/Aktivitäts-/Personal-Vorlagen) · `BewerbungTemplateRenderer` (bare `NAME`/`BEWERBER`/`DATUM`/`UHRZEIT`/`DIENSTGRAD`, nur Bewerbungs-Anschreiben) · `MentionParser` (`@{Typ:GUID}`, aufgelöst über `MentionService.ResolveManyAsync` → `<MentionText>`) ·
  `PublicTemplateRenderer` (bare `BUERGER`/`AKTENZEICHEN`/`DATUM`/`UHRZEIT`/`NAME`, nur Bürger-Nachrichten,
  **ohne** HTML-Encoding — siehe „Phase 11" im öffentlichen Bereich).
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

- **Alte Routen leben weiter** in **einer** Shell: `Components/Common/Navigation/LegacyRouteRedirect.razor`
  trägt alle ~38 entfernten `@page`-Direktiven und schlägt das Ziel in `Navigation/LegacyRoutes.cs` nach.
  Abschnitte, die eine **überlebende** Seite verlassen, brauchen zusätzlich `LegacyRoutes.MovedSettingsTab`
  (`Target()` matcht nur ganze entfernte Routen) — so leitet `/einstellungen?tab=protokoll` auf `/nachweis`.
  **Beim Entfernen einer Route: `@page` erst in die Shell eintragen, wenn die alte Datei im selben Schritt
  gelöscht wird** — zwei Komponenten auf derselben Route werfen erst zur *Laufzeit*, nicht beim Kompilieren.
- `Navigation/MergedPageSections.cs` hält die Slug-Listen der Sammelseiten; `LegacyRoutesTests` prüft
  darüber, dass jedes Redirect-Ziel einen existierenden `?tab=`-Abschnitt benennt.
- **Strengere Abschnitte** werden in `<AuthorizeView>` gewickelt. Weil `RecordSection.OnInitialized` die
  Registrierung auslöst, registriert sich ein nicht gerenderter Abschnitt gar nicht erst — der Rail-Button
  fehlt, und ein manipuliertes `?tab=` fällt auf den ersten *erlaubten* Abschnitt zurück.
- **Globales `[Authorize(Policy = Policies.ActiveAgent)]` in `_Imports.razor`** → jede neue Seite ist standardmäßig auth-pflichtig. Öffentliche Seiten brauchen explizit `[AllowAnonymous]` (Login, Pending, Blocked, Error, NotFound, Legal).
- **Strengere Seiten:** `@attribute [Authorize(Policy = Policies.LeadershipPage|AdminPage|HighestClassificationPage)]`. Feingranular per `<AuthorizeView Policy="@Policies.X" Context="...">` (explizite `Context`-Namen bei Verschachtelung). `*Page`-Policies lassen bewusst auch den `OnlyReader` rein — nicht zu Rang-Requirements „vereinfachen".
- **Kein Code-Behind** (`*.razor.cs` existiert nicht) — Logik in inline `@code`. Private Felder `_camelCase`.
- **Dark-Mode hardcoded** (`IsDarkMode="true"`, nur `PaletteDark` in `Theme/NooseTheme.cs`). Admin-Akzentfarben zur Laufzeit über `/einstellungen?tab=system` (`NooseTheme.WithColours(...)`).

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

`Components/Layout/NavMenu.razor` ist zweispaltig: links eine 64-px-Leiste mit Bereichs-Icons, rechts die
Einträge genau eines Bereichs. Ein Icon-Klick **navigiert nicht**, er wechselt nur das Panel.

- **Zwei orthogonale Achsen:** `NavSection` trägt die *Policy* (`NavSectionPolicy.For`), `NavArea` die
  *Darstellung*. Sie divergieren bewusst (`organigramm` ist `Analyse`/`Dienststelle`, `bewerbungen` ist
  `VerwaltungBewerbungen`/`Verwaltung`) — nicht zusammenlegen.
- Das rechte Panel **muss** in `<MudNavMenu Color="Color.Primary" Bordered="true">` bleiben: MudBlazor
  scopet die farbige Aktiv-Markierung auf `.mud-navmenu-primary .mud-nav-link.active`.
- Die Leiste ist von Hand gebaut (Flex + `MudIconButton`), weil MudBlazor kein Nav-Primitive hat, das
  auswählt ohne zu navigieren. `DrawerVariant.Mini` ist **nicht** die Lösung.
- Blazors `NavLink` setzt **nie** `aria-current` — das wird über `NavCatalog.ByRoute` selbst berechnet.
- **Bereichs-Sichtbarkeit** kommt aus einem Snapshot: `IAuthorizationService` einmal je *distinkter Policy*
  in `OnInitializedAsync` (nicht je Eintrag). Ein Bereichs-Icon erscheint nur, wenn mindestens ein Eintrag
  erlaubt ist. Der Snapshot darf veralten — Rang-/Flag-Änderungen rotieren den `SecurityStamp` und erzwingen
  ohnehin ein Re-Login.
- **`NavPreferences.CollapsedGroups` ist tot, bleibt aber als Property stehen**: der Service macht
  Read-Modify-Write über den ganzen JSON-Blob, ein Entfernen würde die Daten beim nächsten Speichern
  stillschweigend löschen. Aktiv ist stattdessen `LastArea`.
- Favoriten auf V1.5-verschmolzene Seiten werden über `LegacyRoutes.AliasKey` auf ihr neues Ziel gemappt.
- **JS-Interop** ist self-hosted + lazy-loaded je Seite mit `?v=`-Cache-Buster: `graph.js` (vis-network), `kalender.js` (FullCalendar), `richtext.js` (Quill 1.3.7); `app.js` (Strg+K Command-Palette) ist das einzige global geladene Modul. Interop-Komponenten: `IAsyncDisposable`, Import in `OnAfterRenderAsync(firstRender)`, `[JSInvokable]`-Callbacks, alles in `try/catch` gegen `JSDisconnectedException`.

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
  es nicht mehr. Alle 58 Kategorien tragen den Trait — er entscheidet nur, ob das Modell **eingrenzen** darf;
  Treffer daraus kamen bei einer unbeschränkten `suche_akten` ohnehin an, nur ohne `id=` zum Weiterverfolgen.
- **`SearchIndexBackfillWorker.Version` hochzählen**, wenn `SearchIndexProjection` einen Typ dazubekommt. Sonst
  bekommen Bestandsinstallationen **null** Index-Zeilen für den neuen Typ, und die phonetische Suche wirkt „flaky".
- **Suchtests konstruieren mit `MaxConcurrency = 1`** (`SearchTestHost`): `SqliteTestContext` gibt jedem Context
  dieselbe offene `SqliteConnection`, und zwei gleichzeitige Kommandos darauf sind undefiniert.
- **Stale Docs:** `Authorization/README.md` und `Infrastructure/README.md` sind veraltete „Phase 0"-Stubs; viele `<see cref>`-Tags zeigen auf alte deutsche Typnamen. Quelle ist der Code, nicht die READMEs.

## NOOSEI (KI-Integration)

- **Ein einziger Weg zum Modell:** `INooseiGateway.AskAsync`. Es prüft `Permission.RequireLlmUse`, dann das
  Wochenkontingent, führt aus (bei Werkzeugen mehrere Runden), bucht **genau einmal** ab und schreibt eine
  Zeile in `KiAnfragen`. `ILlmService` ist reiner Transport (eine Runde = ein HTTP-Call) und bleibt DB-frei.
  `NooseiGatewayCoverageTests` schlägt fehl, sobald ein vierter Produktionsdateiname `ILlmService` nennt.
- **Der Modellname ist aus `ILlmService` entfernt.** Keine Komponente *kann* ihn rendern; sichtbar ist er nur
  in `/einstellungen?tab=noosei`. Nach außen heißt alles NOOSEI.
- **Werkzeuge** (`Services/Llm/Tools/`) liefern deutschen Klartext und filtern **jeder für sich** über den
  `ViewerScope` des fragenden Agenten. `NooseiToolResult.NotFound()` ist für „existiert nicht" und „darfst du
  nicht sehen" **absichtlich identisch** — alles andere macht ein Werkzeug zum Existenz-Orakel für VS-Akten.
  Keine Schreibwerkzeuge: NOOSEI liest, Agenten schreiben.
- **Ein Werkzeug muss seine `Refs` liefern.** `NooseiToolResult.Refs` ist der einzige Weg, auf dem eine berührte
  Akte in die Quellen-Chips unter der Antwort (`KiNachrichten.Quellen`) und in die Aktenliste der Protokollzeile
  kommt — der `NooseiToolExecutor` gibt deshalb `NooseiToolOutcome(Text, Refs)` zurück, nicht nur Text. Refs ohne
  `Id` (die Werkzeug-Zähleinträge des Gateways) fallen bei den Chips bewusst raus.
- **Eine Anzahl ist eine Aussage über den Bestand.** `finde_akten` (Merkmalssuche und Zählung) fächert deshalb
  über die Listendienste aus und filtert im Speicher — die VS-Filterung kommt aus dem kanonischen Lesepfad, nicht
  aus einer zweiten Abfrage. Genauso `hole_kennzahlen`: `isLeadership` wird aus `Scope.MayClassifiedRead`
  **abgeleitet**, nie als `true` übergeben, sonst verrät ein Aggregat die Existenz eingestufter Akten, die kein
  Werkzeug nennen würde.
- **Welcher Aktentyp in welches Werkzeug darf, steht ausschließlich in `NooseiRecordTypes`** (`INooseiTool.cs`):
  eine `Uses`-Zeile je Typ mit den Flags `Read`/`List`/`Chronicle`, aus denen die Schema-Enums beim statischen
  Init berechnet werden. **Deutscher Name, Plural und die durchsuchbare Menge stehen dort NICHT** — sie kommen
  aus `SearchCatalog` (`NooseiUse.Search` ist der `SearchTraits.Assistant`-Trait, nicht eine Kopie davon).
  Zwei Label-Tabellen über dieselben 58 Kategorien driften; abgeleitet können sie es baulich nicht.
  **Jedes Werkzeug mit `typ`-Parameter nimmt den geprüften Overload `Clr(german, NooseiUse.X)`** — nicht das
  nackte `Clr(german)`. Das Schema-Enum ist nur ein Hinweis, und ein durchgerutschter Typ landet in
  `Visibility.IsRecordVisibleAsync`. Drei Achsen statt einer Liste, weil sonst die schmalste Fähigkeit für alle
  entscheidet. `NooseiRecordTypesTests` prüft je Flag per Dateiscan, dass der Dienst dahinter es auch kann.
  `German(clr)` fällt nie auf den CLR-Namen zurück, sondern auf `"Eintrag"` — ein englischer Typname liest sich
  für das Modell wie eine Aktenart, die es öffnen darf.
- **`NooseiUse.Read` verlangt zwingend einen Arm in `Visibility.IsRecordVisibleAsync`.** Dessen Schwanz beantwortet
  jeden **unbekannten** Typ mit „für alle sichtbar" — für einen lesbaren Typ ist das ein Leck, kein Default.
  `EveryReadableType_HasAnArmInTheVisibilityGate` hält das per Dateiscan fest. `Job` und `Appointment` waren dort
  lange reine Existenzprüfungen, obwohl ihre echten Regeln in `JobVisibility`/`AppointmentVisibility` stehen; das
  blieb nur folgenlos, solange beide nicht lesbar waren. Ein neuer Arm **benennt** einen vorhandenen Helfer und
  schreibt nie ein eigenes Prädikat.
- **Ein Dossier ist ein Budget, Akteninhalte sind ein eigenes.** `lies_akte` liefert Stammdaten plus einen Auszug;
  Kommentare, Quellen, Wiedervorlagen, Doks, Chat, Tagesordnung und Bewerbungs-Schriftwechsel holt
  `lies_akteninhalt` (`ReadRecordContentTool`) paginiert und mit `MaxContentResultChars`. Es **gatet die Elternakte
  selbst** — `TagService.GetForRecordAsync` und `CustomFieldValueService` haben für interne Agenten kein eigenes
  Gate, sie verlassen sich auf die Seite, die sie rendert. Hier ist das Werkzeug diese Seite.
- **`finde_akten` zählt Akten, `lies_bereich` berichtet einen Zustand.** Die Trennung steht in beiden
  Beschreibungen und in `NooseiPrompts.ToolChoice`; ohne sie rät das Modell. In `lies_bereich` (`ReadAreaTool`)
  liest ein Bereich ohne Recht **wortgleich wie ein leerer** — `UnauthorizedAccessException` wird abgefangen,
  sonst wäre das Werkzeug ein Rechte-Orakel über Bereiche, die das Schema ohnehin nennt.
- **`DossierContextBuilder` ist `partial` über zwei Dateien** (Aktenarten / Betrieb). Der Drift-Scan liest
  `DossierContextBuilder*.cs` — wer nur die Hauptdatei scannt, bekommt einen falsch-roten Wächter und entschärft ihn.
- **Modell pro Funktion** über `LlmOptions.ModelByFeature` (`ModelFor(feature)`, leer = Standardmodell).
  `LlmService` löst es aus `request.Context.Feature` auf — es gibt bewusst kein `Model` auf `LlmRequest`, damit
  Funktion und Modell nicht auseinanderlaufen können. Sichtbar bleibt es nur in `/einstellungen?tab=noosei`.
- **Der Akten-Anker der Unterhaltung wird jede Runde neu geprüft**, nicht einmal beim Anlegen: `?akte=Typ:Id`
  ist eine Nutzereingabe, und die Systemzeile nennt die Akte beim Namen. Ohne
  `Visibility.IsRecordVisibleAsync` gegen den *aktuellen* Scope wäre der Anker ein Existenz-Orakel.
- **„Verbindung" heißt drei Tabellen, nicht eine.** Mitgliedschaften (`FraktionMitglieder`/`PersonengruppeMitglieder`/
  `ParteiMitglieder`), typisierte `PersonBeziehungen` und die manuellen `Verknuepfungen` — `GraphEdgeLoader` ist die
  kanonische Aufzählung. `zeige_verbindungen` deckt alle drei ab, **in beide Richtungen** (Person → ihre Fraktionen,
  Fraktion → ihre Mitglieder); `finde_verbindungsweg` geht über `IGraphService.FindPathAsync` mehrere Schritte weit.
  Ein neuer Verbindungstyp gehört in `GraphEdgeLoader` **und** in `ListRelatedTool`, sonst ist er für NOOSEI unsichtbar.
  Ebenso: eine Akte, die eine Zuordnung nur von einer Seite rendert, macht sie im Kurzbrief der anderen Seite unsichtbar
  (war so bei `DossierContextBuilder`: Fraktionen listeten ihre Mitglieder, Personen nicht ihre Fraktionen).
- **`lies_kalender` nimmt bewusst nur `ICalendarService`, keinen `IDbContextFactory`.** Neun Quellen mit neun
  eigenen Sichtbarkeitsregeln (Agenda am Uhrzeit-Gate, Aufgaben am Kippschalter, Abmeldungen am Roster) laufen
  dort schon durch; ohne DB-Handle gibt es im Werkzeug baulich keinen Weg daran vorbei. `CalendarEntry.EntityType`/
  `EntityId` setzt **nur**, wer schon entschieden hat, dass der Betrachter die Akte kennen darf — eine
  Wiedervorlage auf eingestuftem Elternteil trägt weder Titel noch Link noch Referenz.
- **Der Kurzbrief-Cache wird auf minimalem Privileg erzeugt** (`DossierScope.ForRecord`), weil eine Zeile pro
  Akte von jedem gelesen wird, der die Akte sehen darf. `DossierContextBuilder.BuildAsync(..., scope: null, ...)`
  wählt genau das; der Werkzeug-Pfad übergibt den echten Scope.
- **Die Werkzeugaufrufe einer Runde laufen zusammen**, nicht nacheinander (`Task.WhenAll`); vier serialisierte
  Aktenlesungen sprengen sonst das Turn-Budget, das jede einzelne mühelos einhält. Zwei Regeln hängen daran:
  die **Wiederholungserkennung bleibt sequenziell** und in der Reihenfolge des Modells (sonst entscheiden zwei
  identische Aufrufe je nach Laufzeit unterschiedlich, und die Refs verlieren die Ordnung, auf die
  `MaxSources` beim Deduplizieren baut), und das **Turn-Budget wird genau einmal *nach* dem Bündel geprüft**
  (`turnCts.Token.ThrowIfCancellationRequested()`) — `RunToolAsync` wirft deshalb nie, auch nicht bei
  Abbruch, weil ein liegengelassener Werkzeug-Task später als unbeobachtete Ausnahme ohne zugehörige Anfrage
  auftaucht.
- **Ein abgelaufener Turn liefert aus, was da ist.** Läuft die Zeit ab, während ein Werkzeug noch liest, wird
  der Text der letzten Runde mit `Truncated` und einem deutschen Hinweis ausgeliefert statt einer Ausnahme —
  120 Sekunden Spinner, Kontingent belastet und kein Wort war die schlechteste aller Antworten. Der
  **Abbruch durch den Agenten fällt weiter durch** (Unterscheidung über `!cancellationToken.IsCancellationRequested`),
  und ohne bereits erzeugten Text wird nichts gerettet. Die Zeile ist `Erfolg = true` **mit**
  `Fehlerart = Timeout`: der Agent hat eine Antwort bekommen, und genau diese Kombination zählt die Auswertung.
- **`KiAnfragen` trägt acht nullable Betriebsspalten** (`Abschlussgrund`, `Versuche`, `ModellDauerMs`,
  `Werkzeugaufrufe`, `Werkzeugfehler`, `Eingeschraenkt`, `AbbruchGrund`, `Fehlerart`), befüllt über
  `LlmRequestTrace` am `LlmChargeInput`. **Nullable heißt „nicht gemessen"**, 0 heißt „gemessen und keins" —
  eine Zeile von vor der Migration darf nicht als Turn ohne Werkzeuge durchgehen. `DauerMs − ModellDauerMs`
  ist das Werkzeugbudget und die einzige Zahl, die ein langsames Modell von einer langsamen Datenbank trennt.
  Ausgewertet in `/einstellungen?tab=ki-betrieb` (`NooseiHealthPanel`) — **ausschließlich in Kontingent-Token**,
  damit `NooseiCostVisibilityTests` dort gar nicht erst greifen muss. Die Werkzeug-Rangliste ist eine
  **Stichprobe** der neuesten Zeilen (die Namen liegen in `Kontextrefs`, das kein Index erreicht) und sagt das
  auch dazu.
- **Kontingente:** ISO-Woche, Reset Montag 00:00 lokal, träge beim Lesen (kein Worker), Vorbild ist das
  Finanzierungsbudget. **1.000 Kontingent-Token = 1 Cent** echter API-Kosten (`usage.cost` von OpenRouter).
  Der Übertrag ist auf `Basis · %/100` **gedeckelt** (`LlmQuotaMath.CarryOut`) und wird auch **beim Lesen**
  geklemmt — anders als beim Finanzierungsbudget, das compoundieren kann.
- **Tagesgrenze zusätzlich zur Woche:** `LlmRankQuota.DailyPercent` (Standard 40 %), durchgesetzt in
  `EnsureAvailableAsync`. Gemessen an der **Basis, nicht an Basis + Übertrag** — eine Woche mit Übertrag soll
  länger reichen, nicht schneller ausgebbar sein; ein individuelles `LlmQuotaOverride` verschiebt sie mit.
  **Die Wochensperre schlägt die Tagessperre** (`IsDayBlocked` enthält `!IsBlocked`), sonst verspräche die
  Meldung „ab morgen früh" etwas, das erst am Montag stimmt. Die Burn-Rate-Regel R2 *meldet* weiterhin nur;
  gestoppt wird hier.
- **Der Kontingent-Lesepfad fragt vier Mal, egal für wie viele.** `QuotaSnapshot.LoadAsync` holt geschlossene
  Perioden, Verbrauch je Woche, Korrekturen und den heutigen Verbrauch gruppiert; `CloseElapsedAsync` liest
  danach gar nichts mehr, auch nicht im `ReadPredecessor`-Sonderfall. Vorher waren es sieben Abfragen **je
  Agent** — bei dreißig Agenten 210 Round-Trips in einem synchronen Render, und derselbe Pfad hängt hinter
  jeder Antwort, weil die Abrechnung mit einem Status-Lesen abschließt.
- **Nur der KI-Eigner ändert Kontingente** (`Ki:OwnerDiscordId(s)` → Claim `noose:kiowner` → `Policies.AiOwner` /
  `Permission.RequireAiOwner`). Führung und andere Admins lesen nur. Bewusst eine eigene Achse neben den
  Bootstrap-Admins, von denen es mehrere gibt.
- **`RequireLlmUse` sperrt Partner, Demo und die Nur-Lese-Aufsicht.** Unterhaltungen (`KiUnterhaltungen`)
  sind besitzer-privat, liegen **nicht** im globalen Papierkorb, und ihr `RechteStempel` verwirft beim
  Replay alle Werkzeug-Antworten, sobald sich der Scope des Besitzers geändert hat. Weil es keinen Papierkorb
  gibt, ist `DeleteAsync` ein **Hard-Delete** — der Dialog im Chat sagt deshalb „endgültig" und „nicht
  wiederherstellbar".
- **Die Werkzeug-Spur unter einer Antwort kommt aus den gespeicherten `tool`-Zeilen**, nicht aus einer zweiten
  Kopie auf der Assistant-Zeile — sonst zeigen frische und wiedergeöffnete Unterhaltung Unterschiedliches.
  Folge: ein Aufruf ohne Ergebnis fehlt in der Spur, weil genau diese Zeilen bewusst nicht gespeichert werden.
  Deutsche Bezeichnung und Fortschrittstext eines Werkzeugs stehen zusammen in `NooseiToolLabels` — getrennt
  driften sie, und ein neues Werkzeug erscheint dann in der einen Liste als roher Bezeichner.
- **Vorschlags-Chips kosten nichts.** Startfragen sind fest, Folgefragen werden aus den Quellen der letzten
  Antwort abgeleitet — kein Modellaufruf. Ein Chip **füllt nur das Eingabefeld**, dieselbe Regel wie bei einer
  aus der Befehlspalette übernommenen Frage: Senden bleibt eine bewusste Handlung, weil es Kontingent kostet.
- **Der Bild-Platzhalter des Editors ist ein Attribut, kein `src`.** `richtext.js` ersetzt jedes base64-Bild
  durch `data-noosei-bild="n"` und tauscht es beim Übernehmen zurück. Ein Platzhalter *im* `src` funktioniert
  nicht: `src` ist ein URI-Attribut, und `HtmlCleanup` wirft jedes unbekannte Schema weg — das löschte das Bild
  still aus dem korrigierten Dokument. Deshalb geht der Editor-Pfad über `HtmlCleanup.CleanAiPayload`
  (Allowlist + Marker), alle anderen Pfade über `Clean`, das den Marker bewusst verwirft.
- **Echtes Geld sieht ausschließlich der KI-Eigner.** Alle anderen — Agenten, Führung, andere Admins, die
  Nur-Lese-Aufsicht — rechnen in Kontingent-Token. Zwei Schichten sichern das: Beträge stehen nur unter
  `Components/Pages/Admin/` (Seite hängt an `Policies.LeadershipPage`, Anfragen-Protokoll zusätzlich an
  `Policies.CounterIntel`), **und** dort nochmals hinter `IsAiOwner()` — als `_maySeeCost` in den Panels, als
  Parameter `MaySeeCost` im `LlmRequestDetailDialog` (Standard `false`, damit ein neuer Aufrufer nichts leakt).
  Auch der Umrechnungssatz „1.000 Token = 1 Cent" ist ein Preis und fällt darunter. `NooseiCostVisibilityTests`
  prüft beide Schichten per Dateiscan. `LlmQuotaMath.ToCents`/`ToCost` bleiben — nur die Anzeige ist begrenzt.
- **Wochenkosten:** `LlmCostForecast.MaxTokens` summiert `Available` (Basis **+** Übertrag, nicht die Rang-Basis)
  über den `OnlySelectable()`-Bestand — das Maximum, wenn alle restlos verbrauchen. `Expected` mittelt die
  **abgeschlossenen** Wochen aus `ILlmRequestLogService.GetWeeklySpendAsync`; die laufende Woche ist als
  `Running` markiert und fliegt raus, sonst fiele die Prognose jeden Montag ab und stiege bis Sonntag wieder.
- **Verbrauch wird beim Erzeugen gebucht, nicht beim Übernehmen.** Der Editor-Dialog meldet die Kosten über
  `NooseiDialog.OnGenerated`, sobald die Antwort da ist. Verwerfen, Escape und ein zweiter Anlauf kosten
  genauso; über das Dialog-*Ergebnis* zu buchen zeigte nach einem „Verwerfen" die Zahlen des letzten
  *angenommenen* Durchgangs an. `_lastDiscarded` wird erst nach dem erfolgreichen `applyAiResult` gelöscht.
- **Kontingent-Lesepfade nehmen den `ClaimsPrincipal`** (`Permission.RequireQuotaRead`: eigenes immer, fremdes
  bzw. der ganze Bestand nur mit `MayClassifiedRead`). Die Nur-Lese-Aufsicht sieht die Zahlen, die
  Anomalie-Auswertung darüber bleibt hinter `RequireLeadershipNoReader`.
- **Editor-Korrektur sieht nie Markup:** `TextBlocks` zerlegt das HTML in nummerierte Klartext-Blöcke und
  schreibt die Korrektur in die Textknoten zurück. Formatierung, Gliederung und base64-Bilder sind damit
  mechanisch geschützt, nicht per Prompt. Danach laufen harte Prüfungen (Zahlen, Aktenzeichen, `{{…}}`,
  Erwähnungen, Bewerbungs-Tokens) — ein Verstoß verwirft die Antwort.

## Öffentlicher Bereich

Gebaut sind Phase 1–13, 14a–14c und 15a aus `PublicPlan.md`: Bürgerkonten, das Schaltergerüst, die
redaktionellen Seiten, die öffentliche Fahndung, ihr Ausbau (Warnhinweise, Gefasst-Archiv, Poster, Ablauf,
Aufrufzähler, Discord-Push), das Kopfgeld, die Bürgerhinweise (Formular, Eingang, Rückfrage, Verfolgung,
Triage, Übernahme), die Belohnung (Auszahlung über die Kasse, Beleg für den Bürger), der Ticket-Chat an die
Führungsebene, die Vorlagen für Bürger-Nachrichten, die Organisationsprofile samt beider Gefahrenlisten,
die Sachfahndung (gesuchte Fahrzeuge und Waffen), der Bürger-Einspruch gegen eine Ausschreibung, die
Pressemitteilungen, die amtlichen Warnungen und die freigegebenen Gesetzesauszüge sowie die freigegebenen
Monatstexte und die Gefahrenlage-Ampel. Die öffentlichen Zahlen und der Startseiten-Umbau sind geplant,
aber **nicht** vorhanden — der Modul-Schlüssel `Statistik` existiert schon und steht auf „aus".

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
    Und er wird als **public** Property deklariert: `[SupplyParameterFromQuery]` auf einem privaten Feld
    bindet nicht — kein Fehler, kein Warning, der Filter tut einfach nichts (`?einordnung=` auf
    `/organisationen` war genau das). Kein Test fängt es, weil `.razor` ohne bUnit nicht testbar ist.
  - Ein Tab je Seite gibt es nicht: `Infoseiten` hat **einen** Tab auf den Hub `/info`, damit die Nav weiter
    allein aus `PublicModules` kommt.
- **Eine öffentliche Ausschreibung (`OeffentlicheFahndung`, `/gesucht/{Aktenzeichen}`) ist ein
  Publikations-Snapshot, kein Blick in die Akte.** Jedes Außenfeld steht auf der Zeile; die Personenakte wird
  nach dem Publizieren nur noch für **eine** Frage gelesen, und zwar negativ (siehe Unterdrückungsgürtel).
  Eigener Aktenzeichen-Präfix **`FA`** — `F` gehört den Fraktionen, und `CaseNumberCounter` ist auf
  `(Präfix, Jahr)` verschlüsselt. Das Aktenzeichen ist **nullable** (erst bei der ersten Publikation geprägt)
  und **unique** indexiert — anders als der Seiten-Slug, weil eine Zählernummer nie wiederverwendet wird.
  - **`Status` allein entscheidet die öffentliche Sichtbarkeit.** Zurückziehen behält Aktenzeichen, Vorwurf und
    Fotokopie, damit ein Wiedereinschalten ein Klick auf derselben Adresse ist; Löschen ist erst nach dem
    Zurückziehen erlaubt, sonst wäre es eine stille Depublikation ohne Grund auf der Akte.
  - **Zurückziehen und „gefasst" lassen das Modul-Gate bewusst aus.** Publizieren braucht ein lebendes Modul,
    *De*publizieren nie — sonst machte der Not-Aus das Zurückziehen unmöglich, genau verkehrt herum.
  - **Der Unterdrückungsgürtel im Lesepfad ist eine zweite Abfrage, keine Unterabfrage — und das ist der
    eigentliche Punkt.** `IgnoreQueryFilters()` gilt **für die ganze Kompilierung, nicht für den Operanden**:
    in einer Unterabfrage benutzt, entfernt es den Soft-Delete-Filter auch vom **äußeren** Set. Genau so ging
    eine soft-gelöschte, veröffentlichte Ausschreibung anonym live (nachgemessen, nicht vermutet). `f.Person`
    ist als Navigation ebenso unbrauchbar: sie erbt den Filter und ist für eine gelöschte Akte `null`, also
    zeigte `f.Person == null || …` genau die Zeilen, die es verbergen soll. Deshalb: Zeilen laden, dann
    `OpenRecordsAsync`/`VisibleRecordsAsync` als **eigene** Abfrage, dann im Speicher filtern — dort kann
    `IgnoreQueryFilters` nur das weiten, was es weiten soll. Zusätzlich zieht `RetractForRecordAsync` die Zeile
    selbst offline, gerufen aus `PersonService.EditAsync`/`DeleteAsync` und `PersonMergeService` (das
    `IsClassified` an `PersonService` vorbei setzt) — der Gürtel ist der Gurt, der Hook der Airbag.
  - **Eine Ausschreibung trägt den Inhalt der Akte, also gilt für sie das Lesegate der Akte.** `GetAllAsync`,
    `GetDraftAsync`, `GetOptionsAsync` und `GetForPersonAsync` filtern über `RecordVisibility.IsVisible` —
    ohne das las ein Rang-3-Agent Name, Aktenzeichen, Vorwurf und über `GetOptionsAsync` sogar die **aktuellen**
    `PersonOrte` einer Verschlusssache, die er nirgends sonst öffnen darf. Eine Akte, die gar nicht mehr
    auflöst, ist nicht sichtbar (fail closed).
  - **Zwei Lese-Guards, nicht einer.** `RequirePublicWantedRead` (Rang ≥ 3 oder Aufsicht) gilt nur für die
    **Querliste**; die Ausschreibung *einer* Akte öffnet `RequirePublicWantedRecordRead` (jeder interne Agent).
    Sonst legt ein Rang-2-Agent einen Entwurf an und kommt nie wieder an ihn heran — und die ganze Rang-Weiche
    samt Antrag ist toter Code.
  - **Die VS-Sperre prüft alle drei Flags und ihre Meldung hängt vom Akteur ab:** wer keine eingestuften Akten
    lesen darf, bekommt wortgleich das „nicht gefunden" — sonst verrät der Publizieren-Knopf die Einstufung.
  - **Zwei eigene Guards, und `RequireWriteAccess` ist keiner davon.** Der blockt nur Aufsicht und Partner; ein
    angemeldeter Bürger trägt keinen Rang-Claim und fiele in den „Rang 1–2 ⇒ Antrag"-Zweig. Deshalb
    `Permission.RequirePublicWantedWrite`. Lesen ist `RequirePublicWantedRead` (Rang ≥ 3 **oder** Aufsicht),
    bewusst weiter als `RequireClassifiedRead`: wer direkt publizieren darf, muss seine Entwürfe öffnen können.
  - **Die Inhaltsprüfung sitzt im gemeinsamen Publish-Rumpf, nicht im Aufrufer.** Genehmigen ist der zweite
    Eingang, und eine `Beantragt`-Zeile lässt sich zwischen Antrag und Entscheidung bearbeiten — läuft die
    Prüfung nur in `PublishAsync`, geht ein Platzhalter oder eine Erwähnung über die Genehmigung live.
    Genauso braucht der Genehmigungspfad `RequirePublicWantedWrite` **vor** `RequireHighestClassification`:
    Letzteres allein lässt die Nur-Lese-Aufsicht und das Demo-Principal durch, die dann Aktenzeichen und
    Fotokopie erzeugen, bevor der `ReadOnlyBarrierInterceptor` das Speichern verweigert.
  - **Löschen schließt offene Anträge**, sonst zählt das Nav-Badge eine Zeile, die der Posteingang nicht mehr
    findet und niemand mehr entscheiden kann. Zähler und Liste kommen deshalb aus **derselben** Abfrage.
  - **Foto-Wechsel gilt sofort, nicht erst beim nächsten Publizieren.** `UpdateSnapshotAsync` kopiert bei einer
    laufenden Ausschreibung neu und löscht die alte Kopie — sonst meldet das Entfernen eines Fotos Erfolg,
    während es anonym weiter abrufbar bleibt. Schlägt ein Publizieren fehl, wird die frische Kopie entfernt:
    sie ist die einzige Nebenwirkung, die ein Rollback nicht zurücknimmt.
  - **Rang ≥ 3 publiziert, Rang 1–2 erzeugt einen `Request` mit `RequestType.Veroeffentlichung`** — entschieden
    wird er in `IPublicWantedService`, **nie** über `RequestService.DecideAsync` (Präzedenz: `PartnerFreigabe`).
    Deshalb weist `DecideAsync` jeden Nicht-`Upgrade`-Antrag jetzt ab: Genehmigen heißt dort bedingungslos „setze
    die Einstufung", mit `Classification.Unknown` wäre das eine stille Herabstufung. `HasOpenRequestAsync` und
    der Dedup in `UpgradeRequestAsync` sind aus demselben Grund typ-gebunden.
    **`GetOpenCountAsync` bleibt unangetastet** — `DashboardService` liest es unbedingt für jeden Agenten und
    beschriftet die Zahl „Hochstufung"; der Publikationszähler kommt nur in `NavMenu` dazu.
  - **Das Foto wird beim Publizieren kopiert** (`App_Data/uploads/fahndung`, eigener
    `IPublicWantedPhotoStorageService`). `PersonService.PhotoRemoveAsync` löscht die Datei hart, während die
    Zeile nur soft-gelöscht wird — eine Referenz zerrisse den Steckbrief lautlos. Der Endpoint
    `/gesucht/{Aktenzeichen}/foto` ist die **einzige** `[AllowAnonymous]`-Dateiroute der App: die Autorisierung
    ist die Publikationsprüfung, und er liefert **eine** `404` für jeden Fehlschlag, sonst wäre er ein
    Existenz-Orakel. Er liegt unter `/gesucht`, weil das Präfix schon öffentlich ist — eine eigene
    `/dateien/…`-Route bräuchte `PublicRoutes.ExtraPrefixes` **und** eine `robots.txt`-Zeile.
  - **Nach außen geht die Gefahrenstufe, nicht der Score.** `OeffentlicheGefahrenstufe` wird beim Publizieren
    festgehalten (Aktion „Stufe aktualisieren" im Panel). Der rohe 0–100-Wert wäre der einzige verbliebene
    Grund, `Personen` für Inhalt zu lesen, und die Score-Konfiguration steht in `NeverPublic` als „Anleitung
    zur Umgehung". `PublicWantedModelTests` hält das als **positive** Allowlist der Nach-außen-Typen fest —
    eine Ausnahmeliste wird pro Datei erteilt und weitet sich still.
  - **Publizieren schreibt kein `ManualAudit.Row` gegen die Personenakte** (entgegen Leitsatz 9 in
    `PublicPlan.md`): die Zeile ist `IAuditable`, und eine zweite, `Person`-getypte Zeile fiele in
    `TimelineDisplay.MapAudit` durch den Schwanz und läse sich als „Akte geändert". Der Zeitstrahl kommt über
    den Fan-out in `TimelineService.AuditSourceAsync` — und der verlangt **vier** Registrierungen, nicht drei:
    `AuditSourceAsync`, `MapAudit`, `AuditEntityDisplay` **und `ChronikParentResolver`**.
  - **Der Aufrufzähler ist die dritte dokumentierte Ausnahme von „Bulk-Write ⇒ Guard selbst rufen"** (neben
    Score-Writes und `FactionRecency.StampAsync`): es gibt keinen `ClaimsPrincipal`, der Aufrufer ist ein anonymer
    Besucher, und die eine Zahl verlässt das Haus nie. `CountViewAsync` schreibt über `ExecuteUpdateAsync`, weil ein
    getracktes Inkrement `GeaendertAm` stempeln, **eine `AuditLog`-Zeile pro anonymem Aufruf** schreiben und die
    Ausschreibung bei jedem Seitenaufruf auf den Zeitstrahl der Personenakte schieben würde. Das ganze
    Publikations-Prädikat steht im `Where`, nicht in einem vorgelagerten Lesen — ein zählender Schreibvorgang darf
    eine Zeile, die nicht draußen ist, baulich nicht berühren. Gezählt wird **nur** in `WantedProfile`: der
    Foto-Endpoint zählte Thumbnails statt Leser, und das Poster wird von einem Steckbrief aus geöffnet, der schon
    gezählt hat.
  - **Das Panel in `PersonDetail` liegt außerhalb des `einstufung`-Abschnitts** (`@if (!_isPartner)`):
    `PartnerTabCatalog` listet diesen Slug, dort wäre alles einem freigegebenen Partner sichtbar. Der Warnbanner
    zeigt nur `Veroeffentlicht` — ein `Beantragt` ist nicht draußen und verriete eine offene interne Entscheidung.
    Der Übersichts-Abschnitt auf `/fahndung?tab=oeffentlich` hängt in `AuthorizeView Policy="InternalAgent"`,
    weil `/fahndung` keine Seiten-Policy trägt und `ActiveAgent` erbt — was ein Partner erfüllt.
  - **`DemoModeMiddleware.ExcludedPrefixes` wird aus `PublicRoutes.Prefixes` abgeleitet**, nicht ein zweites Mal
    gepflegt — sonst trägt ein anonymer Besucher bei aktivem Demo-Modus das Demo-Principal. `/gesucht` stand dort
    von Hand; `/gefasst` ist eine **Geschwister**-Route, kein Kind davon, und wäre genauso durchgerutscht. Ebenso
    gibt `PartnerRoutes.IsAllowed` für jede öffentliche Route `true` zurück: ein Partner kann dieselbe Seite
    abgemeldet öffnen, die Sperrmeldung behauptete also eine Einschränkung, die es nicht gibt.
  - Die öffentlichen Seiten heißen `WantedHub`/`WantedProfile`/`WantedArchiveHub`/`WantedPoster`, **nicht**
    `WantedBoard`: `Services/WantedBoard.cs` ist eine global importierte statische Klasse, und `/fahndung` bleibt
    die interne Seite.

- **Phase 5 (Ausbau) — was daran anders ist als am Rest des öffentlichen Bereichs:**
  - **Genau ein Speicherpfad, genau ein Cache-Schlüssel.** `PublicWantedService.SaveAndInvalidateAsync` ist die
    einzige Stelle mit `SaveChangesAsync` **und** die einzige mit `cache.Remove`; ein Dateiscan
    (`PublicWantedCacheDisciplineTests`) hält das fest, samt „kein zweiter Produktionsdateiname kennt den
    Schlüssel". Board und Archiv liegen deshalb in **einem** Snapshot-Record: sie stammen aus derselben Tabelle,
    werden von denselben Schreibpfaden ungültig, und „gefasst setzen" verschiebt eine Zeile zwischen beiden Listen
    auf einmal. Ein zweiter Schlüssel verdoppelte jede Invalidierungsstelle. Die **Modul-Flags** werden weiterhin
    außerhalb des Caches gelesen, je Menge eines: Archiv aus darf das Board nicht mitnehmen und umgekehrt.
  - **Das Warnhinweis-Label ist der einzige Außenwert, der live gelesen wird** statt auf der Snapshot-Zeile zu
    stehen. Bewusst: ein Warnhinweis ist ein redaktionelles Etikett der Behörde, kein Akteninhalt — eine Kopie
    ließe einen korrigierten Tippfehler auf jedem laufenden Poster stehen, und `IstAktiv = false` müsste vierzig
    Ausschreibungen einzeln nacharbeiten. **Der Preis:** das Label geht ohne Publikationsschritt live, also prüft
    `WarnhinweisService` beim Schreiben dieselben drei Regeln wie ein Vorwurf (Klartext, keine `@{…}`-Erwähnung,
    kein `{{`-Platzhalter) plus Längendeckel. Die **Farbe** ist eine Allowlist (`WarnhinweisColours`) — **nie
    `Enum.Parse`**: ein in der Spalte gelandeter Wert wäre auf einer `[AllowAnonymous]`-Seite ein HTTP 500,
    dieselbe Klasse wie `?vorschau=1`. Unsichtbare Farben (`Inherit`, `Transparent`, `Surface`, `Dark`) stehen
    nicht drin; ein Warnhinweis, den niemand sieht, ist schlechter als keiner.
  - **`Gefasst` gehört in die Rückzugs-Statusmenge.** `PubliclyVisible` (= `Veroeffentlicht`, `Beantragt`,
    `Gefasst`) wird von `RetractForRecordAsync` **und** dem Guard von `RetractAsync` benutzt — kein zweites
    Literal-Array. Ohne das zeigte `/gefasst` weiter Foto, Namen und Datum einer Person, die im August gefasst und
    im September als Informant eingestuft wird; und der einzige Weg aus dem Archiv wäre `DeleteAsync` gewesen, also
    eine stille Depublikation ohne Grund auf der Akte.
  - **`/gefasst` ist eine Liste und verlinkt auf nichts.** `/gesucht/{az}` antwortet für eine gefasste Zeile weiter
    „nicht gefunden"; ein Link dorthin wäre eine Sackgasse und suggerierte eine laufende Fahndung. Eigener
    Nach-außen-Record `PublicWantedArchiveCard` statt nullable Felder auf der Board-Karte — was nicht auf dem Typ
    ist, kann keine Seite versehentlich rendern. Gedeckelt auf die 100 jüngsten.
  - **Der Foto-Endpoint bleibt unverändert.** `GetPublishedPhotoAsync` weitet intern auf `Gefasst`, steigt aber
    weiter **nur** über den Snapshot ein (also durch den Unterdrückungsgürtel) und prüft **das Modul der Menge,
    in der es die Zeile gefunden hat**. Kein Query-Parameter: der wäre angreifer-kontrolliert und machte
    „gefasst?" getrennt von „veröffentlicht?" abfragbar — genau das Existenz-Orakel, das die eine `404` verhindert.
  - **Das Poster benutzt `PrintFrame` nicht.** Das druckt über JS-Interop in `OnAfterRenderAsync` und
    `MudButton OnClick` — beides tot auf einer `[ExcludeFromInteractiveRouting]`-Seite, also *stumm* kaputt — und
    rendert „Gedruckt am … von {PrintedBy}", den VS-Stempel und „NOOSE-interne Akte". Stattdessen ein eigener
    statischer Rahmen mit rohem `onclick="window.print()"`, **ohne** Auto-Druck. Die Seite bleibt unter
    `Components/Pages/Public/` (ein Umzug nähme sie aus **allen vier** `PublicPageScanTests`-Prüfungen), zieht
    **beide** Modul-Gates selbst — `PrintLayout` trägt weder `PublicNav` noch den Not-Aus-Banner — und trägt
    **immer** `noindex`, nicht nur im Nichtgefunden-Fall.
  - **Zwei `NotificationType`-Werte, nicht einer.** `NotificationService.NotifyManyAsync` ruft am Ende unbedingt
    `discord.PushAsync`, also schriebe ein routbarer Betriebs-Typ jede abgelaufene Ausschreibung in den
    öffentlichen Kanal. `PublicWantedPublished` ist routbar, `PublicWantedExpired` bewusst nicht. Der Push sitzt
    **nach dem Commit** in `PublishRowAsync` (eine Discord-Nachricht lässt sich nicht zurückrufen) und nimmt als
    Parameter **nur** einen `PublicWantedCard` — dieser Record kann `PersonId`, das interne `NOOSE-P-`-Aktenzeichen,
    einen Codenamen oder einen Score strukturell nicht tragen, also die Nachricht auch nicht. Kein Vorwurfstext:
    nach einem Rückzug ist der Post dann ein toter Link statt einer stehengebliebenen Anschuldigung.
  - **Der Ablauf-Worker ist keine Sicherheitskontrolle.** `LoadAsync` filtert `ExpiresAt > now` ohnehin; ein toter,
    verspäteter oder doppelt laufender Worker leakt nichts, er lässt nur den internen Status unehrlich. Genau
    deshalb darf niemand den Ablauf-Filter aus dem Lesepfad entfernen, „weil der Worker das jetzt macht". Er hält
    keinen `DbContext` (Gürtel, Statusregeln und Cache-Invalidierung dürfen nicht an einer zweiten Stelle liegen),
    nimmt **nur** `Veroeffentlicht` (ein `Beantragt` mit Ablaufdatum stürbe sonst still, ohne dass sein offener
    Antrag geschlossen wird), rechnet in `UtcNow` und schickt **eine** Sammelmeldung je Lauf. Der Statuswechsel ist
    der Idempotenz-Token — deshalb braucht die Tabelle keine `NotifiedAt`-Spalte, anders als `Followup`.
  - **Eine Chip-Zuordnung überlebt das Deaktivieren ihres Warnhinweises.** `SetHintsAsync` nimmt Zeilen an, die
    *aktiv **oder** bereits zugeordnet* sind: **Hinzufügen** bleibt auf aktive beschränkt (der Riegel gegen einen
    manipulierten Dialog-Post), **Bestehendes** bleibt. Nur-aktiv hätte die Zuordnung beim nächsten „Übernehmen"
    still gelöscht — der Picker bietet einen inaktiven nicht an, der Agent sähe also nicht, was er zerstört.
    Der Editor zeigt ihn deshalb grau mit „(inaktiv, nicht öffentlich)". Und der Schreibpfad prüft die
    **Aktensichtbarkeit immer**, nicht nur bei einer laufenden Ausschreibung — sonst könnte, wer die Id eines
    Entwurfs kennt, eine Verschlusssache bechippen und eine Audit-Zeile gegen sie schreiben.
  - **`MySqlTranslationTests` kompiliert die neuen Abfrageformen gegen Pomelo, nicht gegen SQLite.** Alle
    Integrationstests laufen auf SQLite, das ein anderer Übersetzer ist; eine dort akzeptierte Form kann auf
    Pomelo mit „could not be translated" werfen — zur Laufzeit, auf einer anonymen Seite. `ToQueryString()`
    kompiliert ohne Verbindung, der Test braucht also keinen Server. Neue Abfrageform im öffentlichen Bereich
    ⇒ eine Zeile dort.
  - **`WatchlistRecordRollup` ist die fünfte Registry**, die eine neue auditierte Entität braucht. Ihr Default-Arm
    **warnt nur**, also lief der ganze öffentliche Bereich seit Phase 1 mit einer Logzeile pro Schreibvorgang —
    im Serverlog aufgefallen, nicht im Test. `OeffentlicheFahndung` rollt auf ihre `Person` (Publizieren ist die
    folgenreichste Änderung, die einer Akte passieren kann), die übrigen öffentlichen Tabellen sind „not watchable".
  - **`PublicWantedModelTests` ist keine handgepflegte Liste mehr**, sondern Reflection über den ganzen Namensraum
    `Models.Public`: jeder Record steht in `Outward` **oder** in `Inward` — mit Begründung, Muster
    `PublicVisibility`. Vorher war ein neuer Nach-außen-Record schlicht ungelistet und nichts wurde rot. Generische
    Argumente von Sammlungs-Properties werden mitgescannt, sonst bliebe `IReadOnlyList<PublicWantedHint>` ungeprüft.
  - **`AuditEntityDisplay`-Wächter nur für den öffentlichen Bereich.** Rund siebzig interne Kindtabellen laufen seit
    jeher ohne Label; das zu beheben ist eigene Arbeit. Und geprüft wird gegen die **Quelle**, nicht gegen
    `Label(name) != name`: „Warnhinweis" liest sich in beiden Sprachen gleich, ein Wertvergleich hielte den
    korrekten Arm für einen Treffer.
- **Phase 6 (Kopfgeld) — was daran anders ist:**
  - **Nach außen geht eine Zahl, nie eine Aufschlüsselung.** `PublicBounty(Total, IsCap)` kann Herkunft, Stifter,
    Konto, Kassenbuchung und Anzahl der Anteile strukturell nicht tragen; eine Aufschlüsselung wäre ein öffentliches
    Verzeichnis, welcher Agent eigenes Geld auf wen gesetzt hat. Zwei Schichten: der Typ **und** ein Dateiscan über
    `Components/Pages/Public/` (`PublicSurfaceGuardTests`), weil eine Seite den Dienst auch selbst fragen könnte.
  - **Die Summe wird im Snapshot berechnet, hinter dem Gürtel** — fünfte Abfrage in `LoadAsync`, exakt neben
    `HintsAsync`. **Keine Spalte** auf der Ausschreibungszeile (eine denormalisierte Summe driftet still, und eine
    falsche Zahl über Geld ist schlimmer als eine 10 s alte) und **kein zweiter Lesepfad** (der müsste den
    Unterdrückungsgürtel wiederholen — genau die Phase-4-Falle). Eine Summe `<= 0` erzeugt keinen Eintrag statt
    „0 $" außen.
  - **`SaveAndInvalidateAsync` nimmt jetzt `AppDbContext?`.** Der öffentliche Einstieg
    `IPublicWantedService.InvalidatePublicViewAsync()` ruft dieselbe Methode mit `null` — dadurch bleibt
    `PublicWantedService.cs` bei **einem** `SaveChangesAsync(` und **einem** `cache.Remove(`, und
    `PublicWantedCacheDisciplineTests` musste nicht angefasst werden. Der zweite Wächter dazu:
    jede Produktionsdatei, die `FahndungKopfgeldAnteile` **und** `SaveChangesAsync` nennt, muss auch
    `InvalidatePublicViewAsync` nennen.
  - **`IBountyService` besitzt die Anteile, `IPublicWantedService` die Obergrenze.** `KopfgeldIstObergrenze` ist ein
    Snapshot-Feld auf der Ausschreibungszeile, und deren Tabelle hat genau einen Schreibpfad.
  - **In der öffentlichen Summe zählen nur `Zugesagt` und `Gesichert`.** Ein `Beantragt` ist kein Geld — und
    verriete nebenbei eine laufende interne Entscheidung. `Ausgezahlt` ist ausgegeben (gesetzt wird es erst mit der
    Belohnungsphase), `Zurueckgezogen` ist weg. Diese Regel steht **einmal**, in `Services/Public/BountyShares.cs`:
    als EF-Prädikat `Advertised` und als In-Memory-Zwilling `IsAdvertised` (Muster `AgentSelection`). Sie entscheidet
    den öffentlichen Snapshot, die interne Aufschlüsselung **und** das Vorher/Nachher einer Erhöhung — ausgeschrieben
    driftet sie.
  - **Der Kopfgeld-Posteingang verlangt, dass die Ausschreibung noch existiert** (`PendingRequests`), genau wie der
    Veröffentlichungs-Posteingang. Ein gelöschter Entwurf macht seinen Antrag sonst unentscheidbar — Genehmigen
    antwortet „nicht gefunden" — während das Nav-Badge ihn weiterzählt.
  - **Rang 1–2 beantragt behördliches Geld** (`RequestType.Kopfgeld` mit eigener Spalte `KopfgeldAnteilId`, **nicht**
    `VeroeffentlichungFahndungId` — `PublicWantedService.PendingRequests` joint darauf und sammelte den Antrag sonst
    im falschen Posteingang ein). Der Anteil entsteht als `Beantragt`; Genehmigen schaltet ihn auf `Zugesagt`,
    Ablehnen auf `Zurueckgezogen`. Guard-Reihenfolge wie beim Veröffentlichungsantrag: `RequireBountyWrite` **vor**
    `RequireHighestClassification`.
  - **`Permission.RequireBountyWrite` ist ein eigener Guard**, weil `RequireWriteAccess` nur Aufsicht und Partner
    blockt — ein angemeldeter Bürger käme durch.
  - **Die Deckung zählt zwei Summanden**, und der zweite ist der nicht offensichtliche: offene **behördliche**
    Zusagen **plus** bereits eingezahltes **privates** Geld. Eine Einzahlung hebt den Kontostand und ist trotzdem
    schon vergeben; ohne sie meldet die Deckung Entwarnung, die es nicht gibt. Warnung, **keine** Sperre.
  - **Einzahlen ist ein Compare-and-swap**, wörtlich nach `FinancingService.PayAsync`: zwei Tabs würden sonst zwei
    Einzahlungen auf denselben Anteil buchen (verschiedene Ids, der Unique-Index greift nicht). `ExecuteUpdate`
    umgeht den Interceptor ⇒ `ManualAudit.Row` von Hand. Die öffentliche Summe ändert sich dabei **nicht**.
  - **Anteile sind `IAuditable`, aber **nicht** `ISoftDelete`** — Geldhistorie ist append-only, zurückgezogen wird
    per Status. Deshalb keine `TrashService`-Registrierung.
  - **Discord meldet nur Erhöhungen** (`PublicWantedBountyRaised`, routbar), und nur auf einer laufenden,
    nicht abgelaufenen Ausschreibung bei eingeschaltetem Modul. Eine Senkung bleibt still: sie untergräbt die eigene
    Ausschreibung, und der alte Post steht ohnehin unkorrigierbar weiter. Die Compose-Methode nimmt ausschließlich
    `PublicBountyAnnouncement`.
  - **Der Anteil rollt bewusst nicht auf die Beobachtungsliste.** `WatchlistRecordRollup` ist eine statische Map ohne
    Datenbank, und Anteil → Ausschreibung → Akte sind zwei Hops; das Publizieren der Ausschreibung bleibt das
    beobachtbare Ereignis. Zeitstrahl und Chronik gehen dagegen sehr wohl über beide Hops
    (`TimelineService.AuditSourceAsync`, `TimelineDisplay.MapAudit`, `ChronikParentResolver`).
- **Phase 7 (Bürgerhinweise) — was daran anders ist:**
  - **`/hinweis` ist das öffentliche Formular, `/hinweise` der interne Eingang** — ein Buchstabe Unterschied,
    getrennt durch die Segmentgrenze in `PublicRoutes.Matches` (Präzedenz `/fahndung` vs. `/gesucht`, mit Test).
    Der **NavCatalog-Key ist `buergerhinweise`**: `hinweise` gehört den algorithmischen *Ermittlungs*hinweisen
    (`/ermittlungshinweise`), und gespeicherte Favoriten zeigen darauf.
  - **Erste interaktive Seite unter `Components/Pages/Public/`.** Leitsatz 5 erlaubt das für Formulare;
    `PublicPageScanTests` hält jede *Lese*seite weiter statisch und führt dafür `InteractiveExempt` (eine Ausnahme
    je Datei **mit Begründung**, Muster `LayoutExempt`). Der Knopf am Steckbrief ist deshalb ein **Link** auf
    `/hinweis?fahndung={Aktenzeichen}` — ein Dialog wäre auf der statischen Seite stumm tot.
  - **Der Bezug wird über das Aktenzeichen aufgelöst, nie über eine Id vom Client.** `SubmitAsync` fragt
    `IPublicWantedService.GetByCaseNumberAsync` (also den Lesepfad hinter dem Unterdrückungsgürtel) und sucht die
    Zeilen-Id erst danach. Eine rohe `FahndungId` machte das Formular zum Existenz-Orakel für Entwürfe.
  - **Das Rate-Limit steht im Dienst, nicht in der Middleware** — die Einreichung läuft über SignalR und erreicht
    `UseRateLimiter` nie. `TipRules.PerDay` wird in `SubmitAsync` gezählt, **mit `IgnoreQueryFilters`**, sonst kauft
    Löschen einen weiteren Versuch. Die Policy `noose-hinweis` hängt nur am Datei-Endpoint.
  - **Anonymität ist eine Projektion und eine Audit-Regel, keine UI-Bedingung.** `TipDetail.CitizenName` bleibt
    `null`, solange die Zusage gilt. Und weil der Interceptor beim Einreichen das *einreichende Konto* stempelt —
    ein Agent kann über seine Zivil-Identität melden —, streichen **Zeitstrahl und Chronik den Akteur** einer
    `Hinweis`-Zeile: eine Regel in `Services/Public/TipAnonymity.cs`, von `TimelineService` und
    `GlobalChronikService` genannt. Das Änderungsprotokoll auf `/nachweis` ruft sie **bewusst nicht** — dort ist
    das Konto die Missbrauchskontrolle.
  - **Eine Bürger-Zeile trägt baulich keinen Agenten.** `HinweisNachricht.AutorAgentId` wird nur auf
    `Intern`-Zeilen gesetzt, `CitizenTipMessage` hat gar kein Autorfeld, der Absender ist konstant „NOOSE". Nach
    außen adressiert ein Bürger seinen Hinweis über das **Aktenzeichen**, nie über die Zeilen-Id.
  - **Zwei Guards.** `Permission.RequireTipRead` = jeder interne Agent **inkl. Nur-Lese-Aufsicht** (sie liest
    sonst alles; der Eingang wäre die einzige Ausnahme), `RequireTipHandling` = zusätzlich Schreibrecht. Beide über
    die neue `AgentPrincipalExtensions.IsInternalAgent()` — `Status == Active` erledigt dort vier Ausschlüsse auf
    einmal (Pending, Blocked, Bewerber, Bürger).
  - **Der Anhang ist nicht öffentlich**: eigener Pfad `App_Data/uploads/hinweise`, eigener Storage-Dienst,
    `/dateien/hinweise/{id}` mit `.RequireAuthorization()` — anders als die Fahndungs-Fotoroute, deren
    Autorisierung die Publikationsprüfung ist. Eigentümer und Bearbeiter bekommen etwas, alle anderen eine `404`.
  - **`PublicTipReceived`/`PublicTipAnswered` sind nicht routbar.** `NotifyManyAsync` pusht jede routbare Kategorie
    nach Discord, und ein eingehender Hinweis im öffentlichen Kanal outet den Hinweisgeber. Benachrichtigt wird die
    Führung; alle anderen haben das Nav-Badge (`tips`).
  - **Kein Suchprovider, kein NOOSEI-Zugriff** — beide Tabellen stehen in `SearchCatalog.NotSearchable`: ein Provider
    müsste die Anonymitätszusage mittragen, die bisher nur die Bearbeiter-Projektion kennt. Kommt mit Phase 16.
  - Registriert in `PublicVisibility` (beide `NeverPublic`), `SearchCatalog`, `TrashService`/`TrashProjection`
    (die Papierkorb-Zeile nennt weder Bürger noch Text), `AuditEntityDisplay`, `WatchlistRecordRollup`
    (beide „not watchable"), den vier Zeitstrahl-Stellen und `MergedPageSections.Trash`.
- **Phase 8a (Hinweis-Triage) — was daran anders ist:**
  - **Die Priorität multipliziert Bänder mit Untergrenze 1.** `TipPriority` (Kopfgeldband × Gefahrenstufenband ×
    Vertrauensband, 1..100) ist die einzige Wahrheit der Formel. Wörtlich multipliziert wäre sie ohne Kopfgeld
    immer 0 und ein `Critical`-Hinweis sortierte unter eine Bagatelle. Folge, bewusst: ein Hinweis **ohne**
    Fahndungsbezug liegt unter jedem bezogenen — der Eingang trennt ohnehin nach Status in drei Reiter.
  - **`Prioritaet` ist ein Cache, `TipPriorityService` sein einziger Schreiber**, und der hängt **nur** am
    `IDbContextFactory` — genau deshalb dürfen `PublicWantedService` und `BountyService` ihn rufen, ohne einen
    DI-Zyklus zu bauen (`TipService → IPublicWantedService` besteht schon). Er stempelt per `ExecuteUpdateAsync`
    und nur **offene** Hinweise (`TipRules.OpenRows`): getrackt stempelte er `GeaendertAm`, schriebe eine
    `AuditLog`-Zeile und schöbe den Hinweis bei jeder Kopfgeld-Erhöhung auf den Zeitstrahl der Personenakte.
    **Vierter dokumentierter Fall von „Bulk-Write ⇒ Guard selbst rufen"** neben Score-Writes,
    `FactionRecency.StampAsync` und `CountViewAsync` — hier ohne eigenen Guard, weil jeder Aufrufer schon ein
    abgesicherter Schreibpfad ist. `BountyService.SaveAsync` nimmt dafür die `wantedId`: ein Choke-Point für
    beide Folgen eines Anteil-Writes (Snapshot verwerfen **und** nachstempeln).
  - **`TipPriorityService` nennt `FahndungKopfgeldAnteile`, aber kein `SaveChangesAsync`** — deshalb greift
    `PublicSurfaceGuardTests.EveryWriterOfTheBountyTable_DropsThePublicSnapshot` dort bewusst nicht. Wer ein
    `SaveChangesAsync` ergänzt, braucht `InvalidatePublicViewAsync`, und der Wächter sagt es ihm.
  - **Dublettenerkennung ist ein symmetrisches Maß, nicht `TextSimilarity.PhraseSimilar`.** Das verlangt für
    *jedes* Wort einer Seite einen Partner: zwei Meldungen zum selben Vorfall mit ungleicher Länge fielen immer
    durch, und ein Zweizeiler schluckte einen ausführlichen Bericht. `TipDuplicates` mittelt beide Richtungen
    (Schwelle 0.6, mindestens vier tragende Wörter), in-memory wie die Suche. Gruppiert wird **nach** dem
    Commit, Kandidatenfenster 30 Tage bei **gleichem** Fahndungsbezug (beide `null` als gleich, mit
    ausgeschriebenen Zweigen — `== null` gegen eine Variable übersetzt zu SQL-`NULL`), und ein Fehlschlag der
    Erkennung kippt die Einreichung nie.
  - **Die Vertrauensstufe wird neu berechnet, nicht inkrementiert.** `TipRules.IsTransitionAllowed` erlaubt
    `Bestaetigt → InPruefung → Bestaetigt`; ein Inkrement zählte doppelt und bliebe nach einem Rückzieher zu
    hoch. `IBuergerService.RecomputeConfirmedTipsAsync` zählt die Zeilen (selbstheilend) und wird auch beim
    Löschen und Wiederherstellen gerufen. `TipRules.PerDay` **ist** `TipTrust.DailyQuota(1)`; ein Test hält die
    Gleichheit, zwei Zahlen wären Drift.
  - **Die Vertrauens*stufe* geht auch bei anonymen Hinweisen nach innen, die exakte Zahl nicht.** Sie steckt als
    Faktor in der sichtbaren Priorität und ließe sich zurückrechnen; die Zusage gilt der Identität, nicht der
    Erfolgsbilanz. `CitizenConfirmedTips` bleibt gesperrt, `CitizenName` unverändert `null`.
- **Phase 8b (Übernahme) — was daran anders ist:**
  - **Jede Übernahme endet in einer *manuellen* Verknüpfung.** `TimelineService`, `ChronikParentResolver` und
    `LinkPanel` filtern `!v.Automatic` — automatisch wäre sie auf dem Zeitstrahl unsichtbar, und genau dort ist
    sie der Herkunftsnachweis. Ein zweites `ManualAudit.Row` gegen die Personenakte gibt es **nicht**
    (Präzedenz Phase 4: eine `Person`-getypte Zusatzzeile liest sich als „Akte geändert").
  - **`TipTakeoverService` ruft nur Dienste, es baut keine Entität** (Vorbild `ApplicationCaseService`):
    Klassifizierungs-Gates bleiben bei `PersonService`/`ObservationService`, die Ziel-Sichtbarkeit bei
    `LinkService.CreateAsync`, und `ICaseNumberService.NextAsync` bekommt seine Transaktion von den Diensten.
    **Ausnahme, die eine eigene Prüfung braucht:** `ObservationService.CreateAsync` gatet nur die
    `SecrecyLevel` der Akte, nicht ihre Einstufung, und die `personId` kommt vom Client — `ToObservationAsync`
    ruft deshalb selbst `Visibility.IsRecordVisibleAsync`. `AttachPersonAsync` braucht das nicht, dort prüft
    `LinkService` das Ziel.
    Guard ist `RequireTipHandling` — `MayWrite` allein ließe ein angemeldetes Bürgerkonto durch. Ein
    `Neu`-Hinweis geht danach auf `InPruefung`; bestätigen bleibt eine eigene Entscheidung.
  - **Doppelklick-Schutz statt Compare-and-swap:** es gibt keine Anspruchsspalte auf `Hinweise`, also prüft
    `ToNewPersonAsync` vorab auf eine bestehende `Hinweis → Person`-Verknüpfung; verliert ein zweiter Tab das
    Rennen doch, wird die frische Akte soft-gelöscht und die Verwerfung auditiert. Anders als bei Geld ist eine
    doppelte Akte ein Papierkorb-Eintrag.
  - **`Hinweis` als Verknüpfungs-Gegenstück braucht drei Registries:** `LinkService` (Auflösungs-Arm **und**
    `knownTypes` — ohne den Arm rendert der `else`-Zweig die rohe GUID), `RecordsReference` (sonst steht auf dem
    Zeitstrahl „Akte") und `LinkPanel.TypeDisplay`. Nach draußen geht nur das Aktenzeichen; für einen Partner
    fällt die Verknüpfung automatisch heraus, weil `releasedTargets` den Typ nicht kennt.
  - **Die Hinweisgeber-Historie an der Personenakte listet nur offengelegte Hinweise — auch nicht als Zähler.**
    Der Abschnitt ist über die Identität des Bürgers verschlüsselt, eine Zahl nannte ihn durch Rechnen. Regel:
    `TipAnonymity.IsHidden` plus Query-Zwilling `TipAnonymity.Disclosable`, an einer Stelle, von beiden
    Lesepfaden genannt. Der Abschnitt ist intern (`@if (!_isPartner)`), sein Slug steht **nicht** in `_tabs`
    und **nicht** in `PartnerTabCatalog`.
  - **`IBuergerService.LinkPersonAsync` prüft mehr als sein Vorbild:** `RequireLeadership` +
    `RequireWriteAccess` **und** `Visibility.IsRecordVisibleAsync`. `BewerbungService.LinkPersonAsync` prüft nur
    `AnyAsync(p => p.Id == personId)` und ließe eine Verschlusssache verlinken, die der Akteur nicht öffnen
    darf — bewusst nicht kopiert.
  - **Die Gegenaufklärungs-Engine hat keine Regel-Art, sondern eine Bedingungsmenge.** Ein neuer Fall ist
    deshalb eine **Bedingungskategorie** an **sieben** Stellen: Tri-State `ActorSharesOrgWithTarget` +
    `NeedsOrgLookup` in `CounterIntelRuleDefinition`, drei Felder auf `CounterIntelEvent`, Anreicherung in
    `CounterIntelEventLoader`, ein **fail-closed** Arm in `CounterIntelRuleEvaluator.Matches`, ein `if`-Block in
    `CounterIntelRuleDisplay.Summary` (muss mit `Matches` im Gleichschritt bleiben), `Flag(...)` + `ActorLabel`
    im `CounterIntelRuleDialog`, und die Vorgabe-Regel in `CounterIntelRuleDefaults` **plus** ihr JSON-Literal in
    der Migration. **Der Default muss `null` sein** — die geseedeten Regeln kennen die Eigenschaft nicht.
  - **Aufgelöst wird über die Zivil-Identität des handelnden Kontos**, nicht über den Agentenstatus: ein Agent,
    der über sein Bürgerprofil meldet, ist derselbe Konflikt. Ziel-Person ist bei `Hinweis` die Person hinter
    der Ausschreibung, bei `Person` die Akte selbst, sonst `null` (Bedingung baulich unerfüllbar). Meldung über
    die **eigene** Akte gilt als geteilt.
  - **Das Cockpit ist keine Hintertür um `ResolveAnonymityAsync`:** sind alle gezählten Ereignisse einer Gruppe
    Hinweise mit gewahrter Zusage, heißt das Subjekt „Anonymer Hinweisgeber" und trägt **kein** `Href`; sonst
    zeigt ein Bürgerkonto auf `/einstellungen?tab=buerger` statt auf `/personal/{id}`, das für einen Zivilisten
    ins Leere führt. Gemeldet wird das Muster, der Name kommt weiter nur über den auditierten Weg.
- **Phase 9 (Belohnung) — was daran anders ist:**
  - **Ein Auszahlungspfad, eine Transaktion.** `RewardService.PayoutAsync` bucht je Anteil über
    `IKassenService.BookAsync(db, …)`, legt die `HinweisBelohnungen` an, setzt die Anteile auf `Ausgezahlt` und
    schließt die Hinweise über `ITipService.MarkRewardedAsync(db, …)` — alles in **einem** Kontext und **einer**
    Transaktion, weil kein Geld ohne Statuswechsel und kein Statuswechsel ohne Geld existieren darf. Die Transaktion
    ist ohnehin Pflicht: `ICaseNumberService.NextAsync` verweigert ohne umschließende Transaktion. Nach dem Commit,
    nie davor: `InvalidatePublicViewAsync`, `StampForNoticeAsync`, `AfterRewardAsync`.
  - **Eigener `IRewardService`, nicht `IBountyService.PayoutAsync`.** `BountyService` ist einaudienz-intern; die
    Belohnung ist der erste Geldpfad mit einer **Bürger**-Leseseite (Beleg). Er besitzt `HinweisBelohnungen` und ist
    der einzige Schreiber von `BountyShareStatus.Ausgezahlt` — das macht ihn zum Schreiber der Anteil-Tabelle, also
    greift `PublicSurfaceGuardTests.EveryWriterOfTheBountyTable_DropsThePublicSnapshot` und verlangt
    `InvalidatePublicViewAsync`. Richtig so: `Ausgezahlt` fällt aus `BountyShares.Advertised`, die öffentliche Summe
    sinkt auf 0.
  - **`Gefasst` ist Vorbedingung, keine Nebenwirkung.** Die Auszahlung weist eine nicht gefasste Ausschreibung ab,
    statt sie selbst umzuschalten — die Fahndungstabelle behält ihren einen Schreibpfad (`PublicWantedService`), und
    das Panel schreibt den Hinweis darauf hin, statt einen toten Knopf zu zeigen.
  - **Eine Auszahlung je Ausschreibung.** Danach sind **alle** beworbenen Anteile `Ausgezahlt`; der Statuswechsel ist
    der Idempotenz-Token (Muster Ablauf-Worker), gesetzt per Compare-and-swap wie in `PayInAsync`, mit
    `ManualAudit.Row` je Anteil, weil `ExecuteUpdate` den Interceptor umgeht. **`Ausgezahlt` heißt erledigt, nicht
    restlos geleert** — sonst zählt `GetCoverageAsync` einen abgeschlossenen Fall für immer als offene Verpflichtung.
  - **Die Verteilregel steht einmal**, in `Services/Public/RewardAllocation.cs`: zuerst Geld ohne persönliche
    Übergabe (`Gesichert`, `NooseKasse`), dann unbezahlte private Zusagen (`AgentPrivat` + `Zugesagt` ⇒ keine
    Buchung, `SelbstAusgezahltAm`), je Gruppe ältester Anteil zuerst, `AnteilId` als Gleichstand-Entscheider —
    dieselbe Auszahlung muss immer dieselben Buchungen erzeugen. Dort sitzen auch die Σ-Invariante und die Ablehnung
    einer dritten Dezimalstelle (die Spalte hält zwei, MySQL schneidet die dritte wortlos ab).
  - **`HinweisBelohnung` ist `IAuditable`, aber **nicht** `ISoftDelete`** — Geldhistorie ist append-only, Präzedenz
    `FahndungKopfgeldAnteil`. Keine `TrashService`-Registrierung; eine Fehlbuchung wird in der Kasse gegengebucht.
    Die **`BelegNummer` trägt eine Gruppe** (je Auszahlung und Hinweis) und ist deshalb **nicht** unique indexiert:
    eine Zeile ist ein (Hinweis × Anteil)-Paar, der Bürger bekommt einen Beleg je Hinweis, und der Beleg summiert
    seine Zeilen. Unique ist `KassenBuchungId`. Präfix **`BEL`** (`B` gehört den Bewerbungen).
  - **Der Verwendungszweck der Kassenbuchung nennt nur Aktenzeichen.** `/kasse` liest jeder Agent; ein Bürgername
    dort wäre die Anonymitätszusage über das Kassenbuch umgangen — auch bei aufgelöster Anonymität. Eigener Test.
  - **Anonym ist unauszahlbar** (`TipAnonymity.IsHidden`): Geld braucht einen Empfänger, und der Beleg nennt ihn.
    Ein `Neu`-Hinweis ebenso — `TipRules` erlaubt den Sprung nach `FuehrteZurErgreifung` bewusst nicht. Der Dialog
    listet beide Fälle mit Grund.
  - **`Permission.RequireRewardPayout` ist eine eigene Achse** (interner Agent + Schreibrecht + Führung):
    `RequireKassenBookingWrite` greift nur im Buchungszweig, eine vollständig aus privater Zusage bezahlte Belohnung
    liefe also ohne Führungsprüfung durch. Schreib-Guard vor allem anderen (Präzedenz Phase 6).
  - **Das Modul-Gate sitzt auf den Bürger-Lesepfaden, nicht auf der Auszahlung** — Präzedenz „Publizieren braucht
    ein lebendes Modul, *De*publizieren nie"; eine interne Geldbewegung hängt an keinem öffentlichen Schalter.
    Gefragt wird die **gespeicherte Wahl allein** (`PublicModuleSnapshot.Find(key)?.IsEnabled`), nicht
    `RequireEnabledAsync`: das faltet den Not-Aus ein, und der lässt `/buerger` bewusst offen. Derselbe Grund führt
    `PartnerRoutes.IsAllowed` für `/buerger/**` auf `true` — `BuergerLayout` fragt die Liste nie, `PrintLayout`
    schon, und ohne die Zeile wäre der Beleg die einzige Bürgerseite mit „nicht freigegeben" für einen Partner.
  - **Beleg: Eigentümer und Führung**, jeder andere bekommt `null` ⇒ „nicht gefunden" (nie „kein Zugriff", sonst
    Existenz-Orakel). Der Bearbeiter steht auf **keiner** Projektion — `CitizenRewardReceipt` kann ihn strukturell
    nicht tragen —, und `RewardReceipt.razor` setzt `PrintedBy` nicht. Die Seite liegt unter `Pages/Portal/`, nicht
    unter `Pages/Public/`: der Kontobereich eines angemeldeten Bürgers ist nicht öffentlich, also greifen
    `PublicPageScanTests` und `PublicRoutes` dort nicht — und `PrintFrame` funktioniert, weil Portal-Seiten
    interaktiv sind (anders als das Fahndungsposter).
  - **`PublicRewardPaid` ist nicht routbar** — eine Belohnungsmeldung im öffentlichen Kanal outet den Hinweisgeber.
  - **Zeitstrahl über drei Hops, in zwei Abfragen.** Belohnung → Anteil → Ausschreibung → Akte; gestaffelt statt
    verschachtelt, weil `IgnoreQueryFilters()` kompilierungsweit gilt (Phase-4-Falle). Registriert in
    `TimelineService.AuditSourceAsync`, `TimelineDisplay.MapAudit`, `AuditEntityDisplay` und
    `ChronikParentResolver`; **nicht** im `WatchlistRecordRollup` (statische Map ohne Datenbank — beobachtbar ist
    das Gefasst-Setzen).
- **Phase 10 (Ticket-Chat) — was daran anders ist:**
  - **Ein Ticket hängt an keiner Akte.** Es ist Schriftwechsel, kein Aktenmaterial: **kein** Eintrag in
    `TimelineService.AuditSourceAsync`, `TimelineDisplay.MapAudit`, `ChronikParentResolver`, `RecordsReference`
    oder `LinkService` — es gibt keinen Elternteil, auf den es fan-in könnte. Registriert ist es in
    `PublicVisibility`, `SearchCatalog` (`NotSearchable`, Provider mit Phase 16), `AuditEntityDisplay`
    (Label **und** Route), `WatchlistRecordRollup` („not watchable"), `TrashService`/`TrashProjection` und
    `MergedPageSections.Trash`.
  - **`MayClassifiedRead` ist die Service-Seite von `Policies.LeadershipPage`.** `Permission.RequireTicketRead`
    = `IsInternalAgent() && MayClassifiedRead()`, also Führung *oder* Nur-Lese-Aufsicht — genau die Menge, die
    die Seiten-Policy hereinlässt. `RequireTicketHandling` legt `MayWrite()` darüber, **vor** der Rangprüfung
    (Präzedenz Phase 6/9), damit Aufsicht und Demo-Principal nicht erst ein Aktenzeichen prägen.
    `IsInternalAgent()` steht davor, weil ein Bürgerkonto überhaupt keinen Rang-Claim trägt.
  - **Der Absender außen ist eine Konstante** (`TicketRules.AgencySender`), kein UI-Zweig: die Bürger-Zeile wird
    mit `AutorAgentId = null` geschrieben, und `CitizenTicketMessage` hat kein Autorfeld. Zweite Schicht ist ein
    Dateiscan über `Components/Pages/Portal/` (`PublicSurfaceGuardTests`), der auf **ganze Bezeichner** matcht —
    `CitizenTicketDetail` enthält `TicketDetail`, ein Teilstring-Treffer meldete die Typen, die die Zusage
    einhalten. Der Öffnen-Dialog liegt deshalb unter `Pages/Portal/Shared/`: der Ordner ist die Grenze des Wächters.
  - **Zwei Deckel, nur einer ignoriert den Soft-Delete.** `MaxOpen = 2` zählt lebende Zeilen
    (`TicketRules.OpenRows`), `PerDay = 3` zählt mit `IgnoreQueryFilters` — Löschen gibt den Platz zurück, nicht
    das Tageskontingent. Beide im Dienst, nicht in der Middleware: das Öffnen läuft über SignalR.
  - **Geschlossen ist geschlossen.** Die Bürger-Antwort auf ein abgeschlossenes Ticket wird abgewiesen, nur die
    Führung öffnet wieder (`Geschlossen → InBearbeitung`, nie zurück auf `Offen`). Wiedereröffnen räumt
    `GeschlossenAm`/`GeschlossenVonId` — die Felder beschreiben die aktuelle Schließung, nicht ihre Geschichte.
    Automatisch bewegen sich nur zwei Kanten (Führung antwortet ⇒ `WartetAufBuerger`, Bürger antwortet von dort
    ⇒ `InBearbeitung`); ein **unangetastetes** Ticket bleibt `Offen`, sonst behauptete der Status Arbeit.
  - **Lesestände per `ExecuteUpdateAsync`, `LetzteAktivitaetAm` getrackt.** Der Stempel reitet auf dem
    Statuswechsel mit, der ihn verursacht — anders als bei einem Hinweis unschädlich, weil kein Zeitstrahl daran
    hängt. Je Seite ein eigener Lesestand; die Aufsicht setzt keinen (`MayWrite()` im Dienst, nicht erst im
    Interceptor).
  - **`PublicTicketOpened`/`PublicTicketAnswered` sind nicht routbar** — eine Ticketmeldung im öffentlichen Kanal
    nennt einen namentlich bekannten Bürger. Beim Bürger klingelt es nur bei `WartetAufBuerger` und `Geschlossen`.
  - **`TicketBroadcaster` trägt Zeilen-Id *und* Aktenzeichen:** der Schalter adressiert über die Id, die
    Bürgerseite nur über das Aktenzeichen — ohne das zweite Handle lädt jeder Bürger-Circuit bei jeder
    Ticketänderung im Haus neu. **`TicketArt` hat genau einen Wert**; die Spalte existiert, damit eine zweite Art
    ein Enum-Wert und keine Migration ist.
  - **Der Nav-Eintrag ist der Zugangsschutz:** `NavSection.VerwaltungFuehrung` **ist** `Policies.LeadershipPage`,
    also ist „ein Junior-Agent sieht nichts davon" eine Katalog-Eigenschaft. Die Papierkorb-Zeile nennt Betreff
    und Status, nie den Bürger oder den Schriftwechsel.
  - **Das Änderungsprotokoll auf `/nachweis` liest jeder interne Agent — Inhalt kommt trotzdem nie dort an.**
    Der `AuditSaveChangesInterceptor` erfasst Feldwerte nur bei `Modified`/`Deleted`, nicht bei `Created`: das
    Anlegen eines Tickets und **jede** Nachricht schreiben eine Zeile ohne `ChangesJson`, und an einem Ticket
    ändern sich später nur Status, Bearbeiter und Zeitstempel. Sichtbar ist damit „Ticket existiert, Status
    bewegte sich" plus das handelnde Konto — dieselbe bewusste Offenlegung wie bei `Hinweis` (dort trotz
    Anonymitätszusage, weil das Konto die Missbrauchskontrolle ist). In **Chronik und Zeitstrahl** taucht ein
    Ticket gar nicht auf: `GlobalChronikService.RecordTypes` ist eine geschlossene Liste von zehn Aktenarten,
    und ein Typ ohne Elternteil fällt dort heraus.
- **Phase 11 (Öffentliche Vorlagen) — was daran anders ist:**
  - **Klartext, kein HTML — und das ist der Grund, `BewerbungTemplateRenderer` nicht wiederzuverwenden.**
    `HinweisNachricht.Text` und `TicketNachricht.Text` sind Klartext; der Bewerbungs-Renderer `HtmlEncode`t
    jede Ersetzung, weil ein Anschreiben Markup ist, und hätte dem Bürger „Müller &amp;amp; Sohn" zugestellt.
    `PublicTemplateRenderer` encodet deshalb **nichts** (eigener Test hält die Abweichung fest) und
    `OeffentlicheVorlage.Text` ist `longtext` mit mehrzeiligem Textfeld statt RichTextEditor: Zeilenumbrüche
    sind die Formatierung.
  - **Fünf Arten, jede mit Konsument** (`TicketEingang`, `TicketAntwort`, `HinweisEingang`,
    `HinweisRueckfrage`, `HinweisAblehnung`). `Belohnungszusage` und `Pressemitteilung` aus dem Plantext sind
    **nicht** gebaut — der erste bräuchte einen neuen Schreibpfad in `RewardService` (der schickt dem Bürger
    heute nur Glocke und Beleg), der zweite kommt mit Phase 14. Aus demselben Grund fehlt `BETRAG`: ein nie
    ersetztes Token geht als Literal nach draußen. Präzedenz `TicketArt` mit einem Wert.
  - **Ein Fallback für `BUERGER`, und der anonyme Hinweis ist der Grund.** Die Auto-Bestätigung fragt
    `TipAnonymity.IsHidden` — die Zusage gilt auch gegen die eigene Bestätigung der Behörde, nicht nur gegen
    die Bearbeiter-Projektion. `NAME` wird **zuerst** geschwärzt (Muster Bewerber-Pfad), damit nichts
    Eingesetztes nachträglich getroffen wird.
  - **Die Lesepfade tragen bewusst keinen Guard** (`IPublicTemplateService`): eine Vorlage ist
    Behörden-Textbaustein, kein Akteninhalt, und die Auto-Bestätigung wird gelesen, während ein **Bürger**
    handelt — ein Guard hätte sie mit `UnauthorizedAccessException` beantwortet. Muster
    `IDocumentTemplateService.GetActiveAsync`. Geschrieben wird nur über
    `Permission.RequirePublicTemplateWrite` (Schreibprüfung vor der Rangprüfung).
  - **Tokens bleiben beim Speichern roh, Fremdtokens werden abgewiesen** (`HasForeignToken`: `{{…}}`,
    Erwähnungen, `BEWERBER`, `DIENSTGRAD`). `DATUM`/`UHRZEIT` teilt sich der Satz bewusst mit dem
    Bewerber-Pfad. **`MentionParser.Parse` als Ablehnungsprüfung ist erlaubt** (wie in `WarnhinweisService`);
    verboten ist das Auflösen im öffentlichen Pfad — deshalb steht er nicht im Datei-Scan, und der streicht
    Kommentare, bevor er sucht: die Sätze, die erklären, warum ein System nicht benutzt wird, müssen es
    nennen dürfen.
  - **Die Auto-Bestätigung bewegt keinen Status und läutet nicht.** Ticket bleibt `Offen`, Hinweis bleibt
    `Neu`; `Public*Answered` ist für eine echte Antwort. Sie entsteht in **derselben** Transaktion wie Akte
    und erste Nachricht (Bestätigung ohne Vorgang ist damit unmöglich), die Vorlage wird davor gelesen.
    **Ohne aktive Vorlage passiert nichts** — es gibt keinen Fallback-Text im Code, der Aktiv-Schalter ist
    also auch der Aus-Schalter. `PublicTemplateSeeder` füllt je Art eine Startvorlage, aber **nur bei ganz
    leerer Tabelle** (Muster `WarnhinweisSeeder`).
  - **`PublicTemplateRules.MaxLength` ist abgeleitet** (`TicketRules.MaxMessageLength` minus Reserve): was
    gespeichert wird, muss gerendert noch in eine Nachricht passen.
  - **Kein `PublicModules`-Schlüssel** (interne Konfiguration) und **kein Papierkorb-Eintrag**
    (Konfigurationstabelle, Präzedenz `Warnhinweis`/`DocumentTemplate` — beide `ISoftDelete` und trotzdem
    nicht im globalen Papierkorb; zurückgezogen wird über `IstAktiv`). Registriert ist die Tabelle in
    `PublicVisibility`, `SearchCatalog` (`NotSearchable`), `AuditEntityDisplay` (Label **und** Route),
    `MergedPageSections.Settings`, `WatchlistRecordRollup` — **und in `FeedbackPageTabs`, der sechsten
    Registry**, die jeder neue `/einstellungen`-Abschnitt braucht (`FeedbackPageTabsTests` verlangt jeden
    `MergedPageSections`-Slug im Feedback-Picker).
- **Phase 12 (Organisationen & Gefahrenlisten) — was daran anders ist:**
  - **Nach außen geht die Gefahrenstufe, nie der rohe Score** — und das ist mechanisch gesichert, nicht nur
    verabredet: `PublicPageScanTests.InternalMarkers` enthält wörtlich `"ThreatScore"`, eine öffentliche
    Seite, die ihn nennt, macht den Build rot. Die Zahl ließe die Formel aus `AlgoPlan.md` rückwärts rechnen,
    und die Score-Konfiguration steht in `NeverPublic` als „Anleitung zur Umgehung". Festgehalten wird die
    Stufe beim Publizieren, nachgezogen nur auf Knopfdruck.
  - **Zwei Statusachsen auf einer Zeile.** `Status` (`PublicProfileStatus`) entscheidet allein die
    öffentliche Sichtbarkeit, `Einordnung` (`PublicFactionStanding`) ist das Etikett beobachtet/verboten. Ein
    Feld für beides könnte eine Publikation nicht zurückziehen, ohne die Einordnung zu verlieren. Die
    Einordnung wird **nie** aus `Faction.Classification` abgeleitet — die interne Einstufung nach außen zu
    spiegeln veröffentlichte, woran die Behörde arbeitet. Erlaubt ist nur, was in
    `PublicFactionStandingDisplay.All` steht (`RequireKnownStanding`), sonst stünde draußen eine rohe Zahl.
  - **Keine Rang-Weiche, deshalb zwei Guards statt drei.** Alles ab `SeniorSpecialAgent(3)`:
    `Permission.RequirePublicFactionProfileWrite` (interner Agent mit Schreibrecht — `RequireWriteAccess`
    allein ließe ein Bürgerkonto durch) **vor** `RequireHighestClassification`, und
    `RequirePublicFactionProfileRead` (Rang ≥ 3 oder Aufsicht). Die Antrags-Weiche der Fahndung existiert
    dort nur, weil Rang 1–2 Entwürfe anlegen darf; hier gibt es diesen Zweig nicht, also keinen fünften
    `RequestType`, keine Spalte auf `Antraege`, keinen Posteingang, kein Badge — und ein dritter
    „RecordRead"-Guard hätte niemanden einzulassen.
  - **Ein Hub, keine Detailseite.** Name, Einordnung, Stufe und Kurzbeschreibung stehen auf der Karte; eine
    Detailseite wiederholte dieselben vier Felder. Kein Aktenzeichen-Präfix, kein `CaseNumberCounter`. Die
    Ausschreibung verlinkt bewusst **nicht** auf die Organisation: `OeffentlicheFahndung.FraktionId` ist ein
    interner FK, und den Fraktionsnamen beim Rendern nachzulesen wäre ein Live-Blick statt eines Snapshots.
  - **Eigener Cache-Schlüssel, ein Speicherpfad.** `SaveAndInvalidateAsync` ist die einzige Stelle mit
    `SaveChangesAsync` **und** `cache.Remove`; `PublicFactionProfileCacheDisciplineTests` hält das per
    Dateiscan fest, samt „kein zweiter Produktionsdateiname kennt den Schlüssel" und „wer die Profiltabelle
    schreibt, ist dieser eine Dienst". Eigener Schlüssel neben dem Fahndungs-Snapshot, weil es eine andere
    Tabelle ist — das Phase-5-Argument gegen einen zweiten Schlüssel galt Board **und** Archiv aus
    *derselben* Tabelle.
  - **Der Unterdrückungsgürtel ist wieder eine zweite Abfrage** (`OpenFactionsAsync`), nie eine
    Unterabfrage: `IgnoreQueryFilters()` gilt kompilierungsweit. `p.Faction` als Navigation ist ebenso
    unbrauchbar — sie erbt den Filter und ist für eine gelöschte Akte `null`. Zusätzlich zieht
    `RetractForRecordAsync` die Zeile offline, gerufen aus `FactionService.RefreshAsync` (sobald die Akte VS
    wird) und `.DeleteAsync`; einen `FactionMergeService` gibt es nicht.
  - **`/gefahr/personen` hat keinen eigenen Lesepfad**, es projiziert
    `IPublicWantedService.GetBoardAsync().Cards` — die Stufe steht dort schon, hinter demselben Gürtel. Eine
    zweite Abfrage müsste ihn wiederholen, genau die Phase-4-Falle. Die Ranking-Regel steht einmal, in
    `Services/Public/HazardRanking.cs` (Stufe absteigend, Publikationsdatum als Gleichstand-Entscheider,
    `HazardLevel.No` fällt heraus, Deckel 25) — zwei Oberflächen lesen sie, ausgeschrieben driftet sie. Der
    **Deckel wird auf der Seite genannt**, weil ein stiller Schnitt sich wie Vollständigkeit liest. Die
    Personenliste filtert vorher auf `PublicWantedKind.Fahndung`: eine Vermisstenmeldung und ein
    Zeugenaufruf tragen ebenfalls eine Gefahrenstufe, und beide unter „gefährlichste Personen" zu listen
    wäre eine Anschuldigung, die die Ausschreibung nie erhoben hat. Heute wird nur `Fahndung` ausgegeben —
    die Zeile ist für Phase 13 da, die `Fahrzeug` und `Waffe` bringt.
  - **Gates je Datenmenge, verschachtelt:** `/organisationen` an `Organisationen`, `/gefahr/fraktionen` an
    `Gefahrenlisten` **und** `Organisationen`, `/gefahr/personen` an `Gefahrenlisten` **und** `Fahndung`
    (Präzedenz `GetPublishedPhotoAsync`: „das Modul der Menge, in der es die Zeile gefunden hat").
    `PublicModuleGate` nimmt genau ein Modul; zwei verschachtelt zeigen je den eigenen Offline-Text, was
    genau richtig ist — die Meldung sagt, welcher Schalter es war. Zurückziehen und Löschen fragen das Modul
    **nie**.
  - **Kein Discord-Push.** Ein Organisationsprofil ist kein Handlungsaufruf, und ein Kanal-Post wäre eine
    bleibende Anschuldigung gegen eine ganze Fraktion, die ein Rückzug nicht zurückruft (Präzedenz: eine
    Senkung des Kopfgelds bleibt still). Damit kein neuer `NotificationType`.
  - **Die Inhaltsprüfung sitzt im Publish-Rumpf** (Klartext vorhanden, keine `@{…}`-Erwähnung, kein barer
    `{{`-Opener wie in `WarnhinweisService`) — anders als in Phase 4 gibt es nur *einen* Eingang, also
    genügt eine Stelle. Ein **Entwurf** darf eine Erwähnung tragen, er ist intern; eine laufende Publikation
    wird beim Speichern erneut geprüft. Der Schreibpfad prüft die **Aktensichtbarkeit immer**, nicht nur bei
    laufender Publikation — sonst schriebe, wer die Id eines Entwurfs kennt, gegen eine Verschlusssache
    (Präzedenz `SetHintsAsync`).
  - **Kein Unique-Index auf `FraktionId`** — mit Soft-Delete sperrte er die Fraktion für immer
    (Phase-3-Lektion vom Seiten-Slug). „Ein lebendes Profil je Fraktion" ist eine Dienst-Regel, und
    **Wiederherstellen ist der zweite Weg, sie zu verletzen**: es prüft die Adresse erneut und bringt das
    Profil als **Entwurf** zurück, damit ein Rückgängig nichts nebenbei wieder veröffentlicht.
  - **`FeedbackPageTabs` gilt auch für einen `/fahndung`-Abschnitt**, nicht nur für `/einstellungen` — der
    Wächter verlangt jeden `MergedPageSections`-Slug im Feedback-Picker. Und `TrashServiceTests` vergleicht
    die Papierkorb-Slugs **der Reihenfolge nach** gegen `TrashService.Kinds`: die neue Zeile muss dort
    stehen, wo ihre `Source` steht.
  - **Nicht registriert, mit Grund:** `RecordsReference`/`LinkService` (das Profil ist kein
    Verknüpfungsziel, sondern eine Eigenschaft seiner Fraktion) · `PublicRoutes`/`robots.txt` (`/gefahr`
    steht seit Phase 2 in `ExtraPrefixes`, `/organisationen` ist eine Modul-Nav-Route und damit unabhängig
    von `Available` schon in `Prefixes`) · `CaseNumberCounter` · kein Foto, kein Ablaufdatum, kein
    Aufrufzähler (ein Hub ohne Detailseite hat nichts zu zählen).
- **Phase 13a (Sachfahndung) — was daran anders ist:**
  - **Keine Migration, und das ist ein Fund.** Geplant war eine Spalte auf die Quellzeile des Steckbriefs.
    `PersonService.EditAsync` ersetzt die Steckbrief-Kinder aber **vollständig**
    (`db.PersonVehicles.RemoveRange(person.Vehicles)` + `ChildrenMap`) — jede `PersonVehicle`-/
    `PersonWeapon`-Id ist nach dem nächsten Speichern der Akte eine neue GUID. Ein gespeicherter Verweis
    wäre toter Zeiger, und als FK mit `Restrict` hätte er **jeden Steckbrief-Edit** einer ausgeschriebenen
    Person blockiert. Die Quellzeile ist deshalb reine Vorbefüllung: einmal gelesen, nie gespeichert.
    Dedupliziert wird folgerichtig auf `(Akte, Art, Anzeigename)` — das Kennzeichen benennt die
    Ausschreibung ohnehin nach außen.
  - **„Ohne Personenbezug" gilt nach außen, nicht intern.** `PersonId` bleibt gesetzt, weil daran
    Unterdrückungsgürtel, Zeitstrahl, Chronik und `RetractForRecordAsync` hängen: eine als Verschlusssache
    eingestufte Halterin zieht ihr Kennzeichen ohne eine Zeile neuen Code offline. Der Riegel in
    `PublishAsync` gegen `PersonId is null` bleibt deshalb stehen — eine trägerlose Ausschreibung wäre die
    einzige öffentliche Zeile, hinter der keine Akte steht, also die einzige ohne Gürtel.
  - **Kein Foto, in drei Schichten.** Der einzige Fotospeicher im Haus ist `PersonPhotos`, und
    `PhotoSourceSetAsync` löst über `row.PersonId` auf — mit gesetzter `PersonId` **würde** ein Lichtbild
    der Halterin an einem Kennzeichen auflösen. `GetOptionsAsync` bietet keins an, `UpdateSnapshotAsync`
    **weist** ein `PhotoSourceId` **ab** statt es still zu nullen (Präzedenz `SetHintsAsync`: der Riegel
    gilt dem manipulierten Dialog-Post), `PhotoCopyAsync` räumt bedingungslos.
  - **Der Vorwurf wird bewusst nicht vorbefüllt.** `Person.WantedReason` ist ein Vorwurf gegen die Person
    und nennt sie im Freitext meist beim Namen; auf einer Kennzeichen-Karte wäre das genau der Bezug, den
    die Phase nicht veröffentlicht. `RequirePublishableContent` verlangt den Text ohnehin.
  - **`Services/Public/WantedKinds.cs` ist die einzige Art-Achse** (`IsItem` + die zwei EF-Zwillinge,
    Muster `BountyShares`/`TipAnonymity`). `Vermisst` und `Zeugenaufruf` liegen auf der **Personen**-Seite:
    beide sind Aussagen über einen Menschen, auch wenn sie noch niemand ausstellt.
  - **`FahndungFahrzeuge` ist ein Unterschalter von `Fahndung`**, kein zweites Board: `/gesucht` hängt am
    Board-Modul, aus ⇒ alles dunkel; nur die Sachfahndung aus ⇒ die Kennzeichen fallen, die Personen
    bleiben. Umgesetzt über `PublicWantedBoard.WithoutItems()`, das Karten, Steckbriefe, Archiv **und**
    Kopfgeld-Wörterbuch in einem Zug räumt — **kein zweiter Cache-Schlüssel**, aus demselben Grund, aus dem
    Board und Archiv sich schon einen teilen. `GetByCaseNumberAsync`/`GetBountyAsync` gehen darüber und
    brauchten keine Änderung; `GetPublishedPhotoAsync` bekam das Gate trotzdem ausdrücklich, weil ein
    Endpoint sich nicht auf eine Regel verlassen darf, die in einer anderen Datei steht.
  - **Kein eigener Nav-Tab.** Die `art=`-Chips auf `/gesucht` existieren seit Phase 4 und erzeugen sich aus
    den Arten, die tatsächlich auf dem Board liegen; ein Tab auf `/gesucht?art=…` wäre eine zweite Wahrheit
    über dieselbe Seite (`NavRoute` bleibt `null`, Muster `Kopfgeld`).
  - **Der Personen-Pfad wurde art-eng gezogen:** „eine Ausschreibung je Akte" gilt jetzt nur noch für
    `WantedKinds.PersonRows`, sonst sperrte ein ausgeschriebenes Kennzeichen die Personenfahndung derselben
    Akte. `GetForPersonAsync` und `GetBannerForPersonAsync` ignorieren Sach-Zeilen — der rote Banner
    behauptet, diese **Person** sei öffentlich ausgeschrieben, was ein Kennzeichen nicht wahr macht.
  - **Keine Registry-Runde**, weil keine neue Entität entsteht: kein `AuditEntityDisplay`, kein
    `WatchlistRecordRollup`, kein Papierkorb, kein Zeitstrahl-Arm, kein `SearchCatalog`, keine
    `MergedPageSections`, keine Route, kein Aktenzeichen-Präfix, kein `NotificationType`. Geändert wurden
    zwei Zeilen: der `Available`-Schalter und der `PublicVisibility`-Text der Ausschreibung.
  - **Publizieren gatet auf beide Schalter, und das Gate sitzt hinter dem Zeilen-Ladevorgang.**
    `RequireModulesAsync(kind)` verlangt immer `Fahndung` und zusätzlich `FahndungFahrzeuge` für eine
    Sach-Art — sonst ginge ein Kennzeichen bei ausgeschaltetem Sach-Modul auf `Veroeffentlicht`, der
    Lesepfad striche es weg, und der **nicht zurückrufbare** Discord-Post verlinkte auf eine 404. Welches
    Modul greift, hängt an der Art, also muss die Zeile vorher geladen sein; `RequirePublicWantedWrite`
    läuft weiter davor, damit die Nur-Lese-Aufsicht nichts über Schalterstände erfährt. Dieselbe Regel
    gilt für Bearbeiten, Chips und Kopfgeld-Obergrenze einer laufenden Zeile — *De*publizieren gatet nach
    wie vor nie.
  - **Die Gefahrenstufe kommt weiter aus dem Score der Akte**, auch auf einer Sach-Ausschreibung: die Stufe
    ist die nach außen zulässige Form des Werts, und sie sagt hier, wie gefährlich die Annäherung an das
    Fahrzeug ist. `HazardLevel.No` auf jeder Sach-Karte wäre die schlechtere Aussage — sie läse sich als
    „harmlos“. In der Personen-Rangliste taucht sie trotzdem nicht auf: `/gefahr/personen` filtert seit
    Phase 12 auf `Kind == Fahndung`, und dieser Filter wird jetzt erst scharf.
  - Im Archiv heißt es **„Sichergestellt"**, nicht „Gefasst" — ein Fahrzeug wird nicht gefasst. Route und
    Überschrift behalten das gemeinsame Wort.
- **Phase 13b (Einspruch) — was daran anders ist:**
  - **Stattgeben setzt voraus, dass die Ausschreibung schon offline ist.** `RequireNoticeOfflineAsync` weist
    ab, solange ihr Status in `PublicWantedService.PubliclyVisible` steht — `Gefasst` eingeschlossen, denn
    eine gefasste Ausschreibung steht im Archiv weiter draußen. Wörtlich das Phase-9-Muster („`Gefasst` ist
    Vorbedingung, keine Nebenwirkung"): der Mensch zieht zuerst mit einem echten Grund zurück, und die
    Fahndungstabelle behält dabei ihren **einen** Schreibpfad. Die Statusmenge wird **benannt**, nicht
    kopiert — `PubliclyVisible` ist dafür `internal`, Präzedenz `RequirePublishableRecordAsync`.
  - **Der Einspruch hängt an der Ausschreibung, nicht an der Akte** — er bestreitet, was veröffentlicht
    wurde, und der Snapshot ist das Einzige, was der Bürger je gesehen hat. Zeitstrahl und Chronik gehen
    über zwei Hops (Einspruch → Ausschreibung → Akte), gestaffelt wie beim Kopfgeld-Anteil.
  - **Kein Nachrichten-Thread.** Die Behörde antwortet genau einmal, in `Entscheidungsnotiz`; alles Längere
    ist ein Ticket. Eine Entscheidung ohne Begründung wird abgewiesen — der Text ist das, was der Bürger
    bekommt. Reopening räumt Notiz, Entscheider und Datum: die Felder beschreiben die *aktuelle*
    Entscheidung, nicht ihre Geschichte.
  - **Zwei Deckel, nur einer ignoriert den Soft-Delete.** `PerDay = 3` zählt mit `IgnoreQueryFilters`
    (Löschen gibt das Tageskontingent nicht zurück), „ein offener Einspruch je Ausschreibung und Konto"
    zählt lebende Zeilen. Beide im Dienst, nicht in der Middleware: die Einreichung läuft über SignalR.
  - **Die Ausschreibung wird über das Aktenzeichen aufgelöst, nie über eine Id von außen** — über
    `GetByCaseNumberAsync`, also hinter dem Unterdrückungsgürtel; Entwurf und zurückgezogene Zeile lesen
    sich wortgleich als „gibt es nicht" (Präzedenz `TipService.ResolveNoticeAsync`).
  - **Alle vier Lesepfade weiten über den Soft-Delete-Filter und schreiben `!IsDeleted` von Hand wieder
    hin** — und der Grund ist beim Schalter schärfer als bei der Bürgerliste: die Projektion dereferenziert
    die **Pflicht**-Navigation `Wanted`, EF joint sie deshalb **INNER**. Mit aktivem Filter fällt ein
    Einspruch, dessen Ausschreibung gelöscht wurde, komplett aus `GetListAsync` heraus, während
    `GetCountsAsync` ihn weiterzählt (es berührt keine Navigation) — ein offener Einspruch, den niemand
    findet, neben einem Reiter, der auf seiner Existenz besteht. Nachgemessen. `GetListAsync`,
    `GetCountsAsync` und `GetAsync` lesen deshalb dieselbe Menge. Bei `GetOwnAsync` ist die Weitung
    zusätzlich inhaltlich gewollt: der Bürger muss lesen können, *wogegen* er Einspruch erhoben hat, auch
    nach Rückzug oder Löschung.
  - **Wiederherstellen prüft die Invariante nach.** „Ein offener Einspruch je Ausschreibung und Konto" ist
    eine Dienst-Regel ohne Index, und der Papierkorb ist ihre zweite Tür — nach dem Löschen darf der Bürger
    einen neuen einlegen. Geprüft wird nur für **offene** Zeilen; eine entschiedene belegt nichts.
  - **Der Vorgang wird gerufen, nicht gebaut** (Muster `TipTakeoverService`), und die Zuordnung ist ein
    **Compare-and-swap** über `ExecuteUpdateAsync` (Muster `PayInAsync`): zwei Tabs legten sonst zwei
    Vorgänge an, der letzte Schreiber gewinnt und einer bleibt verwaist. Der Verlierer verwirft seinen.
    `ExecuteUpdate` umgeht den Interceptor ⇒ `ManualAudit.Row` von Hand.
  - **Zwei Guards mit unterschiedlicher Breite.** `RequireObjectionRead` ist die Menge, die auch die
    Ausschreibungsliste arbeitet (Rang ≥ 3 oder Aufsicht) — ein Einspruch ist Fahndungsarbeit, keine
    Führungs-Korrespondenz, und wer publiziert hat, muss den Widerspruch sehen. `RequireObjectionHandling`
    legt Schreibrecht **und** Führung darüber, in dieser Reihenfolge.
  - **Aktenzeichen-Präfix `EIN`**, weil eine Entscheidung zitierbar sein muss — wie Hinweis, Ticket und
    Beleg. Der Schalter adressiert über die Zeilen-Id, der Bürger über das Aktenzeichen.
  - **`PublicObjectionReceived`/`PublicObjectionDecided` sind nicht routbar**: das eine nennt einen Bürger,
    der eine öffentliche Anschuldigung bestreitet, das andere ist an genau einen Bürger adressiert.
  - **Keine Vorlage** (Phase-11-Regel „eine Art, ein Konsument": es gibt keinen Thread, in den eine
    automatische Bestätigung geschrieben werden könnte) und **kein Papierkorb-Verzicht**: die Zeile ist
    `ISoftDelete` und in `TrashService` registriert, ihre Papierkorb-Zeile nennt weder Bürger noch Text.
  - **Was nichts rendert, existiert nicht:** `GetOpenCountForNoticeAsync`, `ObjectionRow.DecidedByCodename`
    und `ObjectionDetail.WantedId` sind wieder entfernt — und eine Identität, die niemand anzeigt, hat auf
    einer Listenzeile ohnehin nichts zu suchen. `PublicSurfaceGuardTests.DeskInternals` ist handgepflegt und
    wusste von der Phase nichts; `ObjectionRow`, `ObjectionDetail` und `DecidedByCodename` stehen jetzt drin.
    Im Panel erscheinen Reiter-Zähler nur für `_mayRead` (ein „Offen (0)" behauptet gegenüber Rang 2 eine
    Tatsache, die er nicht lesen darf) und das Begründungsfeld nur, wenn von hier aus überhaupt eine
    Entscheidung erreichbar ist — sonst würde ein getippter Text beim Zurücksetzen auf „In Prüfung"
    wortlos verworfen.
  - **`/buerger/einspruch` begrüßt einen anonymen Besucher mit dem Discord-Login**, nicht mit einer
    Umleitung auf die Startseite: die Seite wird von einem öffentlichen Steckbrief aus verlinkt, und eine
    Umleitung sähe aus wie ein kaputter Link. Muster `TipForm`, mit `returnUrl` zurück auf die Ausschreibung.
- **Phase 14a (Presse) — was daran anders ist:**
  - **Ein Aktenzeichen statt eines Slugs, und ein Entwurf hat deshalb baulich keine Adresse.** Der
    Seiten-Slug aus Phase 3 darf **nicht** unique indexiert werden (eine soft-gelöschte Zeile behält ihre
    Adresse); eine Zählernummer wird nie wiederverwendet, also ist `Aktenzeichen` unique — und
    `RestoreAsync` braucht anders als bei einer redaktionellen Seite keine Adressprüfung. Geprägt wird es
    bei der **ersten** Publikation (Präzedenz `OeffentlicheFahndung`), Präfix **`PM`**. Folge: ein Entwurf
    ist nicht erreichbar, weil es keine Adresse gibt, nicht weil ein Status ihn versteckt.
  - **Titel und Teaser sind mitgeschnappt, anders als bei einer redaktionellen Seite.** `OeffentlicheSeite`
    hält nur den Rumpf in zwei Spalten und liest den Titel live; dort ist das folgenlos. Eine
    Pressemitteilung ist eine **datierte Aussage** — wer später einen Tippfehler korrigiert und dabei die
    Schlagzeile anfasst, hätte mit „Entwurf speichern" die längst veröffentlichte Überschrift geändert, ohne
    einen Publizieren-Klick. Deshalb `InhaltTitel`/`InhaltTeaser` neben `InhaltHtml`, alle drei **nur** von
    `PublishAsync` geschrieben, und `PressEdit.DraftDiffers` vergleicht alle drei — sonst nennt das Panel
    eine veraltete Schlagzeile aktuell.
  - **Der Auto-Entwurf bei „gefasst" kommt aus einem festen Skelett im Code** (`PressDraftText`),
    ausdrücklich **nicht** aus einer `OeffentlicheVorlage`. Vier Fehlpassungen des 4. Token-Systems, jede
    für sich hinreichend: `PublicTemplateRenderer` encodet bewusst **nichts** (eine Pressemitteilung ist
    HTML, keine Klartext-Nachricht), er schwärzt `NAME` zu `███████` (eine Festnahmemeldung will den Namen
    *zeigen*), `BUERGER` fällt auf „Bürger/in" zurück (es gibt keinen Bürger) und `MaxLength` hängt an
    `TicketRules.MaxMessageLength`. Ein zusätzlicher Token im geteilten Renderer ginge in **jeder**
    Bürgervorlage unexpandiert nach draußen. `PressDraftText` encodet je Einsetzung mit
    `WebUtility.HtmlEncode` (dasselbe wie `BewerbungTemplateRenderer`) und wickelt jede Zeile in ein `<p>`;
    Umlaute werden dabei zu Zahlentitäten, das ist gewollt und hausüblich.
  - **`DiscordGepushtAm` ist der Idempotenz-Token, weil es hier keinen Statuswechsel gibt, der die Rolle
    übernehmen könnte.** Beim Ablauf-Worker und bei der Belohnung ist der Statuswechsel selbst der Token;
    Zurückziehen → Korrigieren → Wiederveröffentlichen ist dagegen ein legitimer Rundgang und darf den
    Kanal nicht ein zweites Mal beschicken. Der Push sitzt **nach** dem Commit und nimmt **nur** einen
    `PublicPressCard` — der Record kann Autor, Aktenbezug und interne Id strukturell nicht tragen.
  - **`PublicPressPublished` ist die erste routbare öffentliche Kategorie ohne Vorbehalt.** Alles aus
    Hinweisen, Tickets und Einsprüchen ist bewusst nicht routbar, weil die Meldung einen Bürger nennt; eine
    amtliche Verlautbarung nennt keinen und ihr Link ist eine dauerhafte öffentliche Adresse.
  - **Der Auto-Entwurf fragt die gespeicherte Modul-Wahl, nicht `RequireEnabledAsync`.** Letzteres faltet
    den Not-Aus ein, und der ist eine vorübergehende Störung der öffentlichen Seiten — kein Grund, einen
    internen Entwurf zu verlieren (Präzedenz: der Beleg-Lesepfad aus Phase 9). Bei dauerhaft
    ausgeschaltetem Modul entsteht **kein** Entwurf: er existiert, um veröffentlicht zu werden, und einer
    je Festnahme, den niemand veröffentlichen kann, ist Rauschen. Der Aufruf steht nach dem Commit in
    `CapturedAsync` und in `try/catch` — eine ausgefallene Bequemlichkeit nimmt keine Festnahme zurück.
    Kein DI-Zyklus: `PressReleaseService` kennt `IPublicWantedService` nicht, es bekommt den Card übergeben.
  - **`CreateCaptureDraftAsync` hält `RequirePublicWantedWrite`, nicht `RequirePressWrite`** — die Autorität,
    die die Ausschreibung schließt, nicht die, die eine Mitteilung veröffentlicht. `RequirePublicWantedWrite`
    hat **keine Rangschwelle**; mit der Führungsprüfung wäre der Automatismus für jede Festnahme unterhalb
    Rang 4 ausgefallen, und weil der Hook in `try/catch` steht, **lautlos**. Der Entwurf bleibt intern, und
    veröffentlichen darf ihn weiterhin nur die Führung. Nicht „aufräumen".
  - **Keine Vorschau-Route**, Folge der Aktenzeichen-Entscheidung: sie wäre die einzige öffentliche Route,
    die für eine unveröffentlichte Zeile antwortet. Der Editor rendert denselben Stand, der veröffentlicht
    würde; `?vorschau=1` bleibt der redaktionellen Seite, die ihren Slug von Anfang an hat.
  - **Zurückziehen behält Inhalt *und* Nummer**, Löschen erst danach, Wiederherstellen kommt als Entwurf.
    Zurückziehen und Löschen fragen das Modul **nie** — Publizieren braucht ein lebendes Modul,
    *De*publizieren nie, sonst machte der Not-Aus das Zurückziehen unmöglich.
  - **`Permission.RequirePressWrite`** = `IsInternalAgent()` → `MayWrite()` → `IsLeadership()`, in dieser
    Reihenfolge: ein Bürgerkonto trägt gar keinen Rang-Claim, und die Rangprüfung allein ließe Aufsicht und
    Demo-Principal bis zum Prägen einer Nummer laufen. Gelesen wird mit `RequireClassifiedRead` (Vorbild
    `RequirePublicPageWrite`). Rang 3 publiziert Ausschreibungen, aber die Stimme der Behörde bleibt bei
    der Führung — deshalb gibt es hier **keine** Antrags-Weiche wie in Phase 4.
  - **Das Veröffentlichungsdatum wird einmal geprägt, wie das Aktenzeichen, und beim Zurückziehen geräumt.**
    `PublishAsync` stempelte anfangs bei jedem Aufruf neu — eine im März veröffentlichte Mitteilung trug nach
    einer Tippfehler-Korrektur den heutigen Tag, und weil `/presse` nach genau diesem Feld sortiert, stand sie
    danach oben. Also `PublishedAt ??=` und `PublishedById ??=`, und `RetractAsync` setzt beide auf `null`:
    eine Mitteilung, die vom Netz war und wieder rausgeht, ist ehrlich neu datiert. Panel-Spalte deshalb
    „Veröffentlicht", nicht „Zuletzt veröffentlicht". Gleiches gilt für die Warnung aus 14b; bei Fahndung und
    redaktioneller Seite bleibt das Stempeln, weil dort kein Datum nach außen geht.
  - **Nebenbefund, mitbehoben: `/lageberichte` war als indexierbar deklariert.** Intern liegen dort
    `LegacyRouteRedirect` und die führungs-only Druckseite `/lageberichte/{Id}` mit den eingestuften
    Aggregaten; gleichzeitig nannte `PublicModules.SituationReports` die Route als `NavRoute`, und
    `PublicRoutes.Prefixes` sammelt Nav-Routen **ohne** `Available`-Filter ein. Damit stand
    `Allow: /lageberichte` in `robots.txt`, der `noindex`-Header fehlte, `DemoModeMiddleware` schloss die
    Route aus und `PartnerRoutes.IsAllowed` gab `true`. Die öffentliche Route heißt jetzt **`/berichte`**,
    das Label bleibt „Lageberichte". **Ein Modul-`NavRoute` ist damit auch eine Indexierungs-Aussage** —
    eine Route, die intern schon belegt ist, darf dort nicht stehen.
  - **`PressCacheDisciplineTests` ist der dritte Cache-Wächter** (nach Fahndung und Organisationsprofilen):
    ein `SaveChangesAsync(`, ein `cache.Remove(`, kein zweiter Produktionsdateiname kennt den Schlüssel, und
    wer `db.Pressemitteilungen` schreibt, ist dieser eine Dienst. Eigener Schlüssel, weil eigene Tabelle.
  - **Nicht registriert, mit Grund:** die vier Zeitstrahl-/Chronik-Stellen sowie `RecordsReference` und
    `LinkService` — eine Pressemitteilung hängt an keiner Akte, es gibt keinen Elternteil, auf den sie
    fan-in könnte (Präzedenz `Ticket`). `PublicRoutes`/`robots.txt` brauchten nichts: `/presse` kommt aus
    der Modul-`NavRoute`.
- **Phase 14b (Warnungen & Recht) — was daran anders ist:**
  - **`OeffentlicheWarnung` ist nicht `Warnhinweis`.** Die eine ist eine Durchsage mit Rumpf und Ablauf,
    die andere die Chip-Werteliste an einer Ausschreibung aus Phase 5. Zwei Tabellen, zwei Bedeutungen,
    zwei Nachbar-Abschnitte in `/einstellungen` (Slug `warnungen` neben `warnhinweise`) — beide
    Panel-Texte sagen deshalb, was sie *nicht* sind.
  - **Ein Hub, keine Detailseite, kein Aktenzeichen** (Präzedenz Phase 12): eine Warnung besteht aus
    Titel und Text, eine Detailseite wiederholte genau diese zwei Felder. Die Karte trägt den ganzen
    Rumpf, also gibt es keinen Präfix, keinen `CaseNumberCounter` und keine Nichtgefunden-Route mit
    ihrem `noindex`. Deckel 20 statt 50 wie bei der Presse — jede Karte bringt ihren Rumpf mit — und
    **der Deckel wird auf der Seite genannt**.
  - **Titel und Rumpf sind mitgeschnappt, `GueltigBis` bewusst nicht.** Die 14a-Regel gilt: neben einem
    Publizieren-Knopf darf „Entwurf speichern" nichts ändern, was schon draußen steht. Das Ablaufdatum
    ist die begründete Ausnahme und hat **keine** zweite Spalte — Verlängern ist keine neue Aussage, und
    ein Republizieren-Zwang ließe eine Warnung sterben, weil niemand den zweiten Knopf drückt (Präzedenz:
    das live gelesene Warnhinweis-Label aus Phase 5). Folge, gewollt: ein Vergangenheitsdatum im Entwurf
    nimmt eine laufende Warnung sofort offline.
  - **Der Filter ist die Kontrolle, und hier gibt es gar keinen Worker.** Der Ablauf-Worker aus Phase 5
    existiert, damit der *interne* Status ehrlich bleibt und offene Anträge geschlossen werden — eine
    Warnung hat weder das eine noch das andere. Der Preis ist benannt: sie steht bis zu ein Cache-Fenster
    (10 s) zu lange, was ein in Tagen gemessener Ablauf nicht merkt, und ihr Status bleibt
    „veröffentlicht"; das Panel schreibt „abgelaufen" daneben. **Löschen verlangt trotzdem erst das
    Zurückziehen** — der Status ist die Aussage, nicht der Filter. Publizieren mit einem Datum in der
    Vergangenheit wird abgewiesen: der Lesepfad filtert auf dasselbe Feld, die Aktion meldete sonst
    Erfolg und änderte nichts Sichtbares.
  - **`Services/Public/PublicExpiry.cs` hält „ein gewählter Tag zählt noch mit"** — herausgezogen aus
    `PublicWantedService`, wo die Regel privat stand. Zwei Kopien driften, und eine Warnung, die mittags
    stirbt, liest sich wie ein Fehler statt wie eine Entscheidung.
  - **Kein Discord-Push, die Umkehrung von 14a.** Eine Pressemitteilung ist routbar, weil ihr Link eine
    dauerhafte Adresse ist; eine Warnung läuft ab, und der Post behauptete danach weiter Gefahr,
    unkorrigierbar — dasselbe Argument, das `PublicWantedExpired` von der Allowlist fernhält. Damit kein
    neuer `NotificationType`.
  - **`/recht` bekommt einen eigenen `IPublicLawService`, obwohl er dünn ist.** Leitsatz 3: ein
    öffentlicher Lesepfad liest nie über einen internen Listendienst. `LawService.GetListAsync`
    beantwortet eine andere Frage (samt Partner-Achse) und darf sich weiten, ohne dass jemand an die
    Öffentlichkeit denkt. Freigegeben wird über **`SetPublicAsync` als einzigen Schreibpfad** von
    `Law.IsPublic`; der Gesetzes-Editor unter `/gesetze` pflegt weiter nur den Text.
  - **Die Gesetzestabelle ist die erste öffentliche Datenquelle mit einem zweiten Schreiber.** Bei
    Fahndung, Organisationsprofilen und Presse heißt die Regel „ein Dienst schreibt die Tabelle";
    `Gesetze` gehört dagegen `ILawService`. Hier heißt sie deshalb **jeder Schreiber verwirft den
    Snapshot**: `LawService` ruft nach `CreateAsync`, `RefreshAsync` und `DeleteAsync`
    `InvalidatePublicViewAsync()` — sonst stünde ein korrigierter oder gelöschter Paragraf ein volles
    Cache-Fenster lang draußen. Auch nach `CreateAsync`, wo es beweisbar unnötig ist: „dieser Paragraf
    kann nicht öffentlich sein" ist eine Aussage über heute. Kein DI-Zyklus — `PublicLawService` kennt
    `ILawService` nicht. `InvalidatePublicViewAsync` läuft durch denselben Choke-Point mit `db == null`
    (Präzedenz Phase 6), damit die Datei bei **einem** `cache.Remove` bleibt.
  - **`PublicLawCacheDisciplineTests` ist deshalb anders gebaut als seine drei Geschwister.** Ein Scan
    „wer die Tabelle nennt und speichert, ist der eine Dienst" wäre dreifach falsch-rot:
    `SearchIndexBackfillWorker`, `LinkService` und `PartnerShareService` **lesen** Paragrafen und
    schreiben anderes. Sie stehen als **Leser mit Begründung** in einer Liste, die Schreiber in einer
    zweiten (Muster `PublicVisibility`) — eine neue Datei mit `db.Laws` + `SaveChangesAsync` macht den
    Build rot, bis jemand entschieden hat, was sie ist.
  - **Kein Deckel auf `/recht`**, anders als auf jedem anderen öffentlichen Hub: ein Gesetzbuch soll
    vollständig sein, „die 50 jüngsten Paragrafen" wäre bei Recht eine falsche Aussage. Der
    **Gesetzestext geht als Klartext raus, nie als `MarkupString`** (`white-space: pre-wrap`,
    Zeilenumbrüche sind seine Formatierung — Präzedenz `OeffentlicheVorlage.Text`); der Warnungs-Rumpf
    ist HTML und läuft über `HtmlCleanup.Clean` beim Speichern **und** beim Publizieren.
  - **Zwei eigene Guards mit heute identischer Bedingung.** `RequireWarningWrite` und
    `RequireLawReleaseWrite` sind beide `IsInternalAgent()` → `MayWrite()` → `IsLeadership()`, in dieser
    Reihenfolge. Getrennt, weil die Meldung den Bereich nennt und zwei Bereiche, die heute dieselben
    Leute einlassen, es morgen nicht müssen. Gelesen wird mit `RequireClassifiedRead`. Freigeben und
    Publizieren brauchen ein lebendes Modul, Zurückziehen und Zurücknehmen **nie**.
  - **`LawService.DeleteAsync` räumt `IstOeffentlich`.** Für einen Paragrafen gibt es heute keinen Weg aus
    dem Papierkorb zurück — aber eine soft-gelöschte Zeile, die ihr Freigabe-Kennzeichen behält, käme an dem
    Tag veröffentlicht zurück, an dem jemand einen baut (Präzedenz: „Wiederherstellen kommt als Entwurf
    zurück"). Das Publikationsdatum von Warnung und Mitteilung folgt derselben Linie, siehe 14a.
  - **`Law` wandert in `PublicVisibility` von `NeverPublic` nach `Publishable`** — das ist die
    eigentliche Aussage der Phase. **Nicht** registriert, mit Grund: die vier Zeitstrahl-/Chronik-Stellen
    sowie `RecordsReference`/`LinkService` — eine Warnung hängt an keiner Akte (Präzedenz `Ticket`,
    `Pressemitteilung`); `PublicRoutes`/`robots.txt` — `/warnungen` und `/recht` kommen seit Phase 2 aus
    der Modul-`NavRoute`, und damit auch `DemoModeMiddleware` und `PartnerRoutes.IsAllowed`.
- **Phase 14c (Lageberichte) — was daran anders ist:**
  - **Der Zeitraum ist die Adresse, nicht ein Aktenzeichen.** `/berichte/2026-08` ist zitierbar, wird nie
    wiederverwendet und ist nicht fälschbar: `Jahr`/`Monat` kommen beim Anlegen **aus dem Anker** und sind
    danach unveränderlich — die Panel-Eingabe trägt sie nicht. Damit kein Präfix, kein `CaseNumberCounter`,
    keine Transaktion. **Kein Unique-Index** darauf (Phase-3-Lektion vom Seiten-Slug: mit Soft-Delete
    sperrte er den Monat für immer); „ein lebender Bericht je Monat" ist eine Dienst-Regel über die
    lebenden Zeilen, und **`RestoreAsync` prüft sie erneut** — nach dem Löschen darf jemand einen neuen
    Text für denselben Monat schreiben, der Papierkorb ist die zweite Tür.
  - **Die Adresse wird geparst, bevor sie nachgeschlagen wird.** `GetByPeriodAsync` geht über
    `ReportPeriod.TryParse` und baut den Schlüssel neu; ohne das läse `2026-8` als zweite Schreibweise
    derselben Seite. `Services/Public/ReportPeriod.cs` ist die einzige Wahrheit von Format, Label und
    Parser (Muster `PublicExpiry`). Der Route-Parameter ist ein **`string`** — Blazor antwortet auf einen
    unparsbaren Wert mit HTTP 500, und an eine öffentliche URL hängt jeder alles.
  - **Der Anker ist nullable, obwohl er beim Anlegen Pflicht ist** — die 13b-Lektion: eine
    **Pflicht**-Navigation wird **INNER** gejoint, also fiele eine Zeile, deren interner Monatsbericht
    gelöscht wurde, lautlos aus `GetAllAsync`, während eine Zählung ohne Navigation sie weiterzählt.
    Nullable ⇒ LEFT JOIN, das Panel schreibt „Monatsbericht gelöscht" daneben, `MySqlTranslationTests`
    hält das `LEFT JOIN` fest.
  - **Kein Löschriegel und kein Rückzugs-Hook auf `SituationReportService.DeleteAsync`.** Der öffentliche
    Text trägt kein einziges Feld des Snapshots, also macht Archiv-Aufräumen ihn nicht falsch (Präzedenz
    14a: eine Pressemitteilung überlebt das Löschen der Ausschreibung, weil sie eine eigene datierte
    Aussage ist). Ein Hook nach dem Muster `RetractForRecordAsync` wäre hier die **schlechtere** Wahl —
    er machte Aufräumen zur stillen Depublikation ohne Grund auf der Zeile. Aus demselben Grund gibt es
    **keinen Unterdrückungsgürtel**: der Lesepfad dereferenziert den Anker nie, also ist die
    `IgnoreQueryFilters`-Falle aus Phase 4/12 baulich nicht erreichbar.
  - **Text, keine Zahlen — und das ist ein Verhaltenstest, kein Namensscan.** Ein interner Monatsbericht
    wird mit `Classified = 4711`, einem Personennamen, einem `NOOSE-P-`-Aktenzeichen und einem
    `/personen/{id}`-Href geseedet; danach wird der **ganze** öffentliche Snapshot serialisiert und darf
    keines davon enthalten, auch nicht die Anker-Id und nicht den Codenamen. Zweite Schicht ist die
    Struktur (`PublicReportCard`/`PublicReportView` können diese Werte nicht tragen), dritte
    `PublicPageScanTests.InternalMarkers` mit `SnapshotJson`, `StatisticsReport`, `DashboardMetrics`,
    `StatisticsTopEntry`, `SituationReportDisplay`, `ISituationReportService` — dieselbe Mechanik, die
    `ThreatScore` seit Phase 12 hält.
  - **Der Anker-Picker liest die Tabelle selbst — `ISituationReportService` wäre ein Objektgraph-Leck.**
    Die öffentlichen Seiten *injizieren* `IPublicReportService`, also baut jeder anonyme Aufruf jeden
    Konstruktor-Parameter dieses Dienstes mit auf; mit dem Statistik-Dienst hängen `IStatisticsService`,
    `IFinancingStatisticsService` und `INotificationService` an `/berichte`. Der Marker-Scan hält
    `ISituationReportService` von den *Seiten* fern und sieht es eine Schicht tiefer nicht. `GetAnchorsAsync`
    liest deshalb vier Spalten direkt — wie `NewForAnchorAsync` ohnehin —, und `GetArchiveAsync` löst je
    Bericht einen Codenamen auf, den ein Picker nicht braucht. **Neue Regel mit Wächter:**
    `PublicSurfaceGuardTests.NoServiceInjectedByAPublicPage_PullsInAnInternalStack` leitet die Dienste aus den
    `@inject`-Zeilen von `Components/Pages/Public/` ab, löst `IFooService` → `FooService.cs` auf und
    **streicht vorher die Kommentare** (Präzedenz `ForeignTokenSystems`). Er meldet einen nicht auflösbaren
    Dienst, statt ihn zu überspringen — sonst leert eine Umbenennung den Scan lautlos.
  - **Der Monatsname ist auf de-DE gepinnt, nicht `CurrentCulture`** — wie `FinancingPeriod`,
    `StatisticsService`, `AttendanceStatisticsService`. Ein Host mit anderer Locale schriebe sonst
    „March 2026“ in eine deutsche Seite, ohne dass etwas rot wird: „August 2026“ liest sich in beiden
    Sprachen gleich, ein Label-Test darauf beweist also nichts. Getestet wird mit März/Oktober/Dezember.
  - **Kein Discord-Push**, anders als bei der Presse: ein Monatsbericht ist keine Eilmeldung, die Nav zeigt
    ihn ohnehin. Damit kein neuer `NotificationType`. Deckel **24** (zwei Jahre) und **auf der Seite
    genannt**; `ByPeriod` über `GroupBy`, nicht `ToDictionary` — die Eindeutigkeit hängt an einer
    Dienst-Regel, nicht an einem Index, und ein werfender Snapshot versteckte **jeden** Bericht.
  - **`Permission.RequirePublicReportWrite`** = `IsInternalAgent()` → `MayWrite()` → `IsLeadership()`, in
    dieser Reihenfolge und als eigener Guard mit eigener Meldung (Präzedenz `RequireWarningWrite`).
    Gelesen wird mit `RequireClassifiedRead`. Titel und Rumpf sind mitgeschnappt, das Publikationsdatum
    einmalig geprägt und beim Zurückziehen geräumt (14a, beide Teile). Publizieren braucht ein lebendes
    Modul, Zurückziehen und Löschen **nie**.
  - **Nicht** registriert, mit Grund: die vier Zeitstrahl-/Chronik-Stellen sowie
    `RecordsReference`/`LinkService` — ein Lagebericht hängt an keiner Akte (Präzedenz `Ticket`,
    `Pressemitteilung`, `OeffentlicheWarnung`); `PublicRoutes`/`robots.txt` — `/berichte` kommt seit 14a
    aus der Modul-`NavRoute`, und damit auch `DemoModeMiddleware` und `PartnerRoutes.IsAllowed`.
- **Phase 15a (Gefahrenlage) — was daran anders ist:**
  - **Die Ampel ist eine redaktionelle Aussage, und der Trend ist genau die Stufe davor.** Der Plantext
    wollte ihn „aggregiert aus `ThreatScoreHistory`, ohne Aktenbezug"; das ist zweifach unbaubar. Die Reihe
    deckt **jede** Person und Fraktion ab, Verschlusssachen eingeschlossen — ein Aggregat darüber ist
    genau das, was die NOOSEI-Doktrin schon intern verbietet, hier anonym. Und sie exportierte den rohen
    Score in abgeleiteter Form, während `PublicPageScanTests.InternalMarkers` wörtlich `"ThreatScore"`
    führt. Präzedenz ist Phase 12: die öffentliche `Einordnung` einer Fraktion wird nie aus
    `Faction.Classification` abgeleitet. `NeverPublic["ThreatScoreHistory"]` versprach den geplanten
    Trend und nennt jetzt den Grund.
  - **Vier `SystemSettings`-Zeilen statt einer Tabelle** (`GefahrenlageStufe`/`…Einschaetzung`/`…Seit`/
    `…Zuvor`, Präzedenz Not-Aus): es gibt genau eine Gefahrenlage. Gespeichert wird der **Name** der Stufe,
    nicht die Zahl — eine „2" sagt weder in der Einstellungstabelle noch in der Audit-Zeile etwas.
    Protokolliert wird zweimal, wie beim Not-Aus: die rohen `SystemSetting`-Zeilen über den Interceptor,
    dazu eine `ManualAudit.Row` mit eigenem Typ `"PublicSituation"`, die die Aktion benennt.
  - **`Seit` bewegt sich nur bei einem Stufenwechsel** (14a-Lektion `PublishedAt ??=` in ihrer schärfsten
    Form: die Seite zeigt genau dieses Datum als „seit"). `Zuvor` wird nur dort gesetzt, und ein zweites
    Speichern ohne Unterschied schreibt gar nichts — auch keine Protokollzeile.
  - **`null` heißt Schweigen, und Schweigen ist nicht `Niedrig`.** `GetPublishedAsync` liefert `null` bei
    Modul aus, nie gesetzt und unerreichbarer Datenbank; eine Standardstufe wäre in allen dreien die
    Behauptung „keine Gefahr". Bewusste Abweichung von den anderen öffentlichen Diensten, die eine leere
    Liste zurückgeben — die ist ehrlich „nichts veröffentlicht".
  - **`SetAsync` ist der einzige Publish-Pfad ohne Modul-Gate.** Es gibt keinen Entwurf/Publiziert-Schnitt,
    also zwänge ein Gate die allererste Stufe live zu gehen, bevor jemand sie schreiben kann — und weil
    das Ausschalten des Moduls hier das Zurückziehen **ist**, machte es das unumkehrbar.
  - **Eigenes Enum, Allowlist beim Lesen.** `PublicSituationLevel` ist bewusst nicht `HazardLevel` (das ist
    `HazardLevelLogic.From(score)`, die Stufe *einer Akte*); ein geteiltes Enum wäre die Einladung, das eine
    aus dem anderen zu rechnen. `PublicSituationLevelDisplay.Parse` ist eine Allowlist, **nie**
    `Enum.Parse`: der Wert kommt aus einer von Hand editierbaren Zeile, und ein Streuwert wäre auf einer
    `[AllowAnonymous]`-Seite ein HTTP 500. Vier unterschiedliche **sichtbare** Farben.
  - **Die Einschätzung ist Klartext**, kein HTML: kein `HtmlCleanup`, kein `MarkupString`, Zeilenumbrüche
    sind die Formatierung. Ein Wächter verbietet `MarkupString` gezielt in `SituationPage.razor` — nur
    dort, weil Warnungs-Hub und Presse-Artikel es legitim benutzen.
  - **`SystemSetting` wandert von `NeverPublic` nach `Publishable`**, mit einem Text, der genau die vier
    Schlüssel nennt und festhält, dass jede andere Zeile (Discord-Webhooks, Wartungstext, Theme, Not-Aus)
    das Haus nie verlässt. `PublicVisibilityCoverageTests` wird davon nicht rot — die Entität war schon
    entschieden; deshalb eine bewusste Handänderung.
  - **`EverySettingsRouteOfTheAuditDisplay_NamesAnExistingSection` liest jetzt die Quelle**, nicht die
    `DbSet`s: `AuditEntityDisplay` beantwortet auch Konfigurations-Typen ohne Tabelle (`"PublicArea"`,
    `"PublicSituation"`), und genau die sah die Reflection-Variante nicht.
  - **Nebenbefund, mitbehoben: `robots.txt` matcht nach Zeichenkette, `PublicRoutes.Matches` nach Segment.**
    `Allow: /lage` deckte damit das interne `/lageberichte` mit ab — dieselbe Klasse wie der 14a-Befund, nur
    von der anderen Seite. Im selben Zug fielen `Allow: /hinweis` ⇒ `/hinweise` (der interne Eingang, dessen
    Trennung die Segmentgrenze eben nur im Header leistet) und `Allow: /info` ⇒ `/informanten` auf. Drei
    `Disallow:`-Zeilen (längste Regel gewinnt, RFC 9309) plus ein abgeleiteter Wächter in
    `PublicRoutesTests`: er liest alle `@page`-Routen, nimmt die internen und verlangt für jede, die eine
    Allow-Zeile mitdeckt, eine **echt längere** Disallow-Zeile. **Eine neue öffentliche Route, die Präfix
    einer internen ist, macht damit den Build rot.**
  - **Nicht** registriert, mit Grund: keine neue Entität ⇒ kein `SearchCatalog`, kein
    `WatchlistRecordRollup`, kein Papierkorb, keine der vier Zeitstrahl-/Chronik-Stellen, kein
    `RecordsReference`/`LinkService`, kein `NotificationType`, kein Discord-Push (eine Stufe ist keine
    Eilmeldung, die Nav zeigt sie ohnehin) · `PublicRoutes` — `/lage` ist seit Phase 2 die Modul-`NavRoute`,
    und `Prefixes` sammelt Nav-Routen ohne `Available`-Filter ein, `Allow: /lage` stand also schon in
    `robots.txt`.
- **Migrationen des öffentlichen Bereichs heißen `Oeffentlich<Planphase>_<Name>`**, nicht `PhaseNN_` — die
  interne Zählung steht schon bei `Phase69` und hätte sich sechsfach überschnitten. Einzige Ausnahme:
  `Phase61_BuergerKonto` (Phase 1) war beim Auffallen bereits angewendet.

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
- `PublicPlan.md` — Öffentlicher Bereich (Fahndung/Kopfgeld/Hinweise/Ticket-Chat/CMS), 16 Phasen; **Phase 1–13, 14a–14c und 15a gebaut**, 15b–16 offen
- `DEPLOYMENT.md` — Server-Setup (nginx → Kestrel `127.0.0.1:5000` → MariaDB), systemd, Troubleshooting
- `GoalOfTheSite.txt` — Original-Spec (Ränge, Feldlisten, Einstufungs-Stufen)
- `CODE_REVIEW_TODO.md` — bekannte Tech-Debt-/Review-Findings
