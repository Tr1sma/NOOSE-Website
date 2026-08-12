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
- **Middleware-Reihenfolge** (load-bearing): `UseForwardedHeaders` (zuerst, vertraut nur Loopback/nginx) → `RequestLocalization` → (nur Prod) `ExceptionHandler`+`HSTS` → `StatusCodePagesWithReExecute("/not-found")` → `HttpsRedirection` → `Authentication` → `Authorization` → `RateLimiter` → `Antiforgery` → `MapStaticAssets` → `/health` → `MapRazorComponents<App>` → `Map*Endpoints`-Gruppen.
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
- **Statische Helfer in `Services/`** (NICHT DI-registriert): `Permission`, `Visibility`, `AgentSelection`, `ClassificationHelper`, `TextSimilarity`, `RecordsReference`, `MentionParser`, `HtmlCleanup`, `TrashProjection`. Geteilte Logik dorthin extrahieren statt kopieren.
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
    entschieden hat — das ist die eigentliche Garantie, nicht der Katalog.
- **Maintenance/Banner/Theme/Logo:** `SystemSettingService` über Key/Value-Tabelle, 10s `IMemoryCache`. Logo/Uploads liegen **außerhalb wwwroot** unter `App_Data/uploads`, ausgeliefert über autorisierte Minimal-API-Endpoints.
- **Drei getrennte Token-Systeme, nie vermischen:** `PlaceholderService` (`{{Name}}`, `{{Aktenzeichen}}`, `{{Datum}}`, `{{Uhrzeit}}`, `{{Agent}}`, `{{Dienstgrad}}` — Dokument-/Aktivitäts-/Personal-Vorlagen) · `BewerbungTemplateRenderer` (bare `NAME`/`BEWERBER`/`DATUM`/`UHRZEIT`/`DIENSTGRAD`, nur Bewerbungs-Anschreiben) · `MentionParser` (`@{Typ:GUID}`, aufgelöst über `MentionService.ResolveManyAsync` → `<MentionText>`).
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

- **Ein Feature-Ordner je Bereich** unter `Components/Pages/` (Account, Admin, Board, Calendar, Cases, Factions, Graph, Groups, Jobs, Laws, Operations, OrgChart, Parties, People, Personnel, Search, Statistics, Taskforces, Wanted, Watchlist). Pro Feature: `*List`/`*Editor`/`*Detail`/`*Print` + `Shared/`. Cross-Feature → `Components/Common/Shared/`.
- **Deutsche Routen:** `/personen`, `/fraktionen`, `/vorgaenge`, `/aufgaben`, `/operationen`, `/parteien`, `/personengruppen`, `/taskforces`, `/kalender`, `/organigramm`, `/statistik`, `/brett`, `/gesetze`, `/suche`, `/graph`, `/fahndung`. CRUD-Subroutes `/{feature}/neu`, `/{Id}`, `/{Id}/bearbeiten`, `/{Id}/druck`.
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
- **Score-Writes gehen via `ExecuteUpdateAsync`**, um den Audit-Interceptor zu umgehen (sonst stempelt jeder Recompute `GeaendertAm` → bricht die Aktualitäts-Ampel). **Bulk-/Raw-SQL umgeht generell die Interceptors** → `Permission.RequireWriteAccess` dann explizit aufrufen.
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
