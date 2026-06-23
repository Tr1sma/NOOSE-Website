# NOOSE-Website

**Zentrale Akten- und Intelligence-Datenbank der NOOSE (National Office of Security Enforcement)** — einer fiktiven Sicherheitsbehörde auf einem FiveM-/GTA-RP-Server.

Live: **https://noose.info**

---

## Überblick

NOOSE-Website ersetzt verstreute Discord-Threads durch eine zentrale, durchsuchbare, bidirektional verlinkte Akten-Datenbank. Pro Person und pro Fraktion existiert genau eine Akte, in der alles zusammenläuft — Beobachtungen, Dokumente, Beziehungen, Einstufungen, Bedrohungsbewertungen.

Alles ist auditiert (Created/Modified/Deleted), Soft-Delete-fähig und rang-/rollengestaffelt. Codebase anglisiert (englische Identifier), Domänen-Vokabular und UI sind Deutsch.

---

## Features

**Übergreifend**
- **Globale Suche / Command-Palette (Strg+K):** indexierte Suche über Personen, Fraktionen, Gruppen, Parteien, Operationen, Vorgänge, Gesetze, Dokumente, Quellen, Kommentare; Fuzzy- und Deep-Scan-Modus; gespeicherte Suchprofile.
- **Bidirektionale Verknüpfung:** generische Link-Engine — Person↔Person, Person↔Dok, Dok↔Vorgang, Gesetz↔Akte.
- **Beziehungsgraph (vis-network):** interaktives Node-Link-Diagramm; Fokus-Modus mit Tiefensteuerung; Pfadsuche zwischen Akten; PNG-Export; Vollbild.
- **Kalender (FullCalendar):** Dual-Modus (persönlich & behördenweit); filterbare Event-Quellen (Termine, Aufgaben, Beobachtungen, Operationen, Fraktions-Aktivitäten); Event↔Akte-Verlinkung.
- **Rich-Text (Quill):** in Dok-Erstellung, Beobachtungs-Notizen und Dok-Quellen.
- **Druckansichten:** für Personen, Fraktionen, Gruppen, Parteien, Vorgänge, Operationen, Aufgaben, Taskforces, Dokumente, Ankündigungen, Statistik.
- **Papierkorb (Soft-Delete):** alle Akten-Typen, führungs-zugänglich, mit Wiederherstellung.
- **Beobachtete Akten (Watchlist):** Akten per Stern folgen → Änderungs-Benachrichtigungen.
- **Aktualitäts-Ampel:** farbcodierte Frische-Badges in allen Listen (Warn-/Stale-Schwellen je Akten-Typ).
- **Einstufung & Zugriff:** Inline-Schloss-Icons, rollenbasierte Sichtbarkeit (Führung, TRU, HRB).

**Akten & Bereiche**
- **Personen** `/personen` — Kern-Akten: Profil, Aliase, Lebensstatus, Bedrohungs-Score, Verbindungen, Beobachtungen, Doks, Fotogalerie, Verlauf, Merge (Führung).
- **Fraktionen** `/fraktionen` — Mitglieder, Ränge, Konflikte/Allianzen, Galerie, Aktivitäts-Timeline, Bedrohungs-Score, Einstufung.
- **Parteien** `/parteien` — politische Organisationen mit Führung/Mitgliedern, Einstufung, zugeordneten Agenten.
- **Personengruppen** `/personengruppen` — lose Sammlungen, Kategorie-Filter, Einstufung.
- **Operationen** `/operationen` — Einsätze/Berichte: Zeitraum, Ergebnis, beteiligte Agenten, Status.
- **Taskforces** `/taskforces` — Einsatzgruppen: Leitungsrollen (Chefermittler, CID-Lead, TRU-Lead), Mitglieder, Geltungsbereich, Genehmigungsstatus.
- **Vorgänge** `/vorgaenge` — Ermittlungs-Dossiers: bündeln Personen, Operationen, Beobachtungen, Doks; Status/Einstufung; Verlauf.
- **Dokumente** `/dokumente` — Dok-Bibliothek + Datei-Uploads; angepinnte Doks (Führung); VS-Stufen (Leadership/TRU/HRB); als Quelle verlinkbar.
- **Gesetzbuch** `/gesetze` — durchsuchbare Gesetzes-Referenz (Paragraph, Titel, Strafmaß), verlinkbar.
- **Aufgaben** `/aufgaben` — Kanban-Board mit Drag-and-Drop (Offen/In Arbeit/Erledigt/Abgebrochen), Priorität, Überfällig-Flags.
- **Schwarzes Brett** `/brett` — Ankündigungen, gezielte Broadcasts, Glocken-Push, Lesebestätigungen.
- **Statistik** `/statistik` — Metrik-Karten, Verteilungs-Charts, 12-Monats-Zeitreihen, Top-10-Bedrohungs-Rankings, CSV-/PDF-Export.
- **Organigramm** `/organigramm` — Hierarchie nach Rang; TRU-/HRB-Quergruppen; Taskforce-Struktur.
- **Personal** `/personal` — Personalakten: Dienstgrad-Verlauf, Belobigungen, Disziplinar, Beförderungen.
- **Recruiting / Bewerbungen** `/bewerbungen`, `/portal` — HRB-Workflow, Bewerber-Portal, Test-Builder, Messaging.
- **Mein Profil** `/profil`, **Admin** `/admin/basisdaten`, **Öffentlich** `/`, `/karriere`.

---

## Tech-Stack

| Bereich | Technologie | Version |
|---------|-------------|---------|
| Runtime | .NET | `net10.0` |
| UI | Blazor Web App (nur **Interactive Server**, SignalR) | — |
| Komponenten | MudBlazor (**nur Dark-Mode**, „Anthrazit + Cyan") | 9.5.0 |
| ORM | Pomelo.EntityFrameworkCore.MySql (zieht EF Core 9 transitiv) | 9.0.0 |
| Identity | Microsoft.AspNetCore.Identity.EntityFrameworkCore | 9.0.16 |
| EF Design | Microsoft.EntityFrameworkCore.Design | 9.0.16 |
| OAuth | AspNet.Security.OAuth.Discord | 10.0.0 |
| HTML-Sanitizing | HtmlSanitizer | 9.0.892 |
| Health-Checks | Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 9.0.16 |
| EF-Tool | dotnet-ef (lokal gepinnt) | 9.0.17 |

**Self-hosted Frontend-Libs** (unter `wwwroot/lib`, lazy via JS-Interop): Quill 1.3.7, vis-network 9.1.9, FullCalendar 6.1.15.

**DB:** lokal MariaDB/XAMPP, Produktion MySQL 8.0 / MariaDB — Engine via `ServerVersion.AutoDetect()`.

> ⚠️ **EF/Identity bewusst auf der 9.0.x-Linie.** Pomelo 9.0.0 unterstützt nur EF Core 9; ein Upgrade auf 10.0.x würde EF Core 10 ziehen und mit Pomelo kollidieren. Die 9.0.x-Pakete laufen sauber auf der .NET-10-Runtime.

---

## Architektur

Schichten innerhalb von `NOOSE-Website/`:

| Ordner | Inhalt |
|--------|--------|
| `Components/` | Razor-Pages + UI (dünn), je Feature ein Ordner |
| `Data/` | `AppDbContext`, `Entities/<Domain>/`, `Migrations/` |
| `Models/` | DTOs, `Enums/`, `Abstractions/` (Marker-Interfaces) |
| `Services/` | Business-Logik + Authorization-Durchsetzung; `Graph/`, `Statistics/`, `Threat/` |
| `Authorization/` | Policies, Requirements, Handler, `ClaimsPrincipal`-Extensions |
| `Infrastructure/` | Interceptors, Broadcaster, Background-Worker, Audit, File-Storage, CurrentUser |

**Tragende Muster**
- **Render-Mode:** alles `InteractiveServer`, außer `[ExcludeFromInteractiveRouting]` (statisch) — pro Seite via `App.razor`.
- **DbContext-Factory:** immer `IDbContextFactory<AppDbContext>` injizieren und pro Operation einen kurzlebigen Context erzeugen (entgeht der Blazor-Circuit-Lebensdauer).
- **3 SaveChanges-Interceptors, Reihenfolge zählt:** `ReadOnlyBarrierInterceptor` (zuerst, vetoed Schreibzugriffe von OnlyReadern) → `AuditSaveChangesInterceptor` (stempelt `IAuditable`, wandelt Hard- in Soft-Delete) → `WatchlistChangeInterceptor` (Fan-out an Follower).
- **Soft-Delete & Audit via Marker-Interfaces** (`ISoftDelete`, `IAuditable`): globaler Query-Filter `!IsDeleted` per Reflection; neue Entität → Interface implementieren.
- **Singleton-Broadcaster für Live-Updates:** scoped Service schreibt die Row, ruft dann den Singleton (`NotificationBroadcaster`, `TaskforceChatBroadcaster`, `SharesBroadcaster`, `AcknowledgmentBroadcaster`, `WatchlistDispatcher`) für Push an verbundene Circuits.
- **Background-Worker** (`AddHostedService`): `FollowupDueWorker` (Wiedervorlagen), `ThreatScoreSweepWorker` (tägl. Score-Recompute), `SituationReportWorker` (monatl. Lagebericht).
- **Authorization im Service-Layer:** Write-Methoden nehmen `ClaimsPrincipal actor` und rufen `Permission.Require*`-Guards als erste Anweisung; Sichtbarkeit zentral in `Visibility`. Berechtigungslogik existiert nur in `Authorization/AgentPrincipalExtensions.cs` und `Services/Permission.cs`.

---

## Datenmodell & Domäne

- **Ein** `AppDbContext : IdentityDbContext<Agent>`; alle Fluent-Configs in `OnModelCreating`.
- **DB-Spalten Deutsch, C#-Member Englisch** — z. B. `Person.CaseNumber` → Spalte `Aktenzeichen`, Tabelle `Personen`, `IsDeleted` → `IstGeloescht`.
- **Aktenzeichen** menschenlesbar, z. B. `NOOSE-P-2026-0001`, race-safe über `CaseNumberCounter`.
- **Polymorphe Assoziationen** (Quellen, Kommentare, Tags, Links, Followups) über `(EntityType, EntityId)` — kein echter FK.

**Kern-Akten-Typen**

| Aktentyp | Tabelle | Einstufung | Bedrohungs-Score |
|----------|---------|------------|------------------|
| Person | `Personen` | ✓ | 0–100 |
| Fraktion | `Fraktionen` | ✓ | 0–100 (null = nicht bewertet) |
| Partei | `Parteien` | ✓ | — |
| Personengruppe | `Personengruppen` | ✓ | — |
| Operation | `Operationen` | ✓ | — |
| Taskforce | `Taskforces` | — | — |

**Zwei Einstufungs-Achsen**
- **`Classification`** (Status einer Akte): `Unknown(0)` → `ReviewCase`/Prüffall `(1)` → `SuspicionCase`/Verdachtsfall `(2)` → `SecuredStateThreatening`/Gesichert staatsgefährdend `(3)`. Höchste Stufe nur durch SeniorSpecialAgent oder Admin, sonst per Antrag.
- **`DocumentClassification`** (VS-Stufe eines Dokuments): `None(0)` (alle) → `Leadership(1)` → `Tru(2)` → `Hrb(3)`. Server-seitig durchgesetzt.

**Bedrohungs-Score**
- Wertebereich 0–100, gültig für Person & Fraktion (Operationen haben keinen Score).
- Basis-Anker nach Einstufung: `SecuredStateThreatening` 75, `SuspicionCase` 50, `ReviewCase` 12, `Unknown` 0.
- Optionaler Konfidenzwert (0–100) bildet Datenlücken ab; Begründung in strukturiertem `BedrohungsDetailJson`.
- `null` = nicht bewertet/exempt (z. B. Staats-Fraktionen). Täglicher Recompute durch `ThreatScoreSweepWorker`.

---

## Rollen & Rechte

Drei orthogonale Achsen: **Rang**, **Boolean-Flags**, **Policies**.

**Ränge** (`Rank`-Enum, int-backed)

| Wert | Rang | Hinweis |
|------|------|---------|
| 1 | JuniorAgent | |
| 2 | SpecialAgent | |
| 3 | SeniorSpecialAgent | darf höchste Einstufung setzen |
| 4 | SupervisorySpecialAgent | **ab hier Führung (Leadership)** |
| 5 | DeputyDirector | entscheidet über Beförderungen |
| 6 | Director | |

Führung = `rank >= SupervisorySpecialAgent` **oder** `IsAdmin`.

**Boolean-Flags auf `Agent`** (Spalten `Ist*`)

| Flag | Bedeutung |
|------|-----------|
| `IsAdmin` | Vollzugriff; setzt Leadership, short-circuited jedes Rang-Requirement |
| `IsTRU` | Tactical Response Unit; Zugriff auf TRU-VS |
| `IsHRB` | Human Resources Branch; Zugriff auf HRB-VS + Recruiting-Verwaltung |
| `IsTeamLead` | reiner Sichtbarkeitsmarker; allein kein Zugriff |

**OnlyReader** = `IsTeamLead && !IsAdmin` (abgeleitet, kein Flag): liest alles inkl. VS, schreibt **nichts** (hart vetoed vom `ReadOnlyBarrierInterceptor`), sieht **nie** Klarnamen, kann alle Taskforces einsehen.

---

## Schnellstart

**Voraussetzungen**
- .NET 10 SDK
- MariaDB (XAMPP) oder MySQL 8.0 lokal

**Secrets (lokal, User-Secrets)**

`appsettings.json` enthält nur leere Platzhalter. Echte Werte via User-Secrets (UserSecretsId `d41f8a93-2c7b-4e16-9a55-0b3e7c1f6d28`):

```powershell
# Connection-String ist ein Beispiel/Template — Server, DB und Credentials anpassen
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=noose;User ID=root;Password=;SslMode=None;"
dotnet user-secrets set "Authentication:Discord:ClientId" "YOUR_DISCORD_CLIENT_ID"
dotnet user-secrets set "Authentication:Discord:ClientSecret" "YOUR_DISCORD_SECRET"
dotnet user-secrets set "Bootstrap:AdminDiscordId" "YOUR_DISCORD_ID"
```

`DatabaseConnectionResolver` probt zuerst `ProductionConnection` (5 s Reachability), fällt sonst auf `DefaultConnection` zurück → derselbe Build läuft lokal und auf dem Server.

**Build & Run** (alle Befehle aus dem Repo-Root)

```powershell
# Build
dotnet build NOOSE-Website.slnx

# Run  → http://localhost:5174
dotnet run --project NOOSE-Website/NOOSE-Website.csproj

# Run mit HTTPS-Profil  → https://localhost:7063
dotnet run --project NOOSE-Website/NOOSE-Website.csproj --launch-profile https

# Hot Reload
dotnet watch --project NOOSE-Website/NOOSE-Website.csproj run
```

---

## Datenbank & Migrationen

`dotnet-ef` ist ein **lokales** Tool (gepinnt auf 9.0.17). Vor jedem `dotnet ef` einmalig restoren:

```powershell
# MUSS vor jedem dotnet-ef-Aufruf laufen
dotnet tool restore

# Dev-Server vorher stoppen (bin-Lock), dann:
dotnet ef migrations add Phase23_<Name> --project NOOSE-Website/NOOSE-Website.csproj
```

`dotnet ef database update` ist i. d. R. **unnötig** — Migrationen werden beim App-Start automatisch via `db.Database.MigrateAsync()` (Program.cs) angewendet. Die Design-Time-Factory zwingt EF-Tools immer auf die lokale `DefaultConnection` → Migrationen können nie Produktion treffen.

---

## Deployment

Deploy aus **64-bit Windows PowerShell** (sonst OpenSSH WOW64-Redirect):

```powershell
.\deploy.ps1                # publish → tar → scp → Service-Swap → /health-Check
.\deploy.ps1 -SkipPublish   # vorhandenen ./publish-Ordner wiederverwenden
.\deploy.ps1 -NoPause       # ohne Pause (CI/Terminal)
```

Ziel: `root@195.20.225.12`, systemd-Service `noose`, App-Dir `/var/www/noose`. Publish wird mit `tar` gepackt (nie `Compress-Archive`), per `scp` hochgeladen, Service getauscht, `/health` geprüft.

**Prod-Gotchas**
- **`App_Data` beim Deploy nie löschen** — enthält Uploads **und** Data-Protection-Keys (`App_Data/keys`); Verlust loggt alle User bei jedem Restart aus. `deploy.ps1` schließt `App_Data` explizit aus.
- **`TZ=Europe/Berlin`** in `/etc/noose/noose.env` nötig — sonst sind alle `ToLocalTime()`-Zeiten verschoben. `TimeZoneInfo.Local` ist prozess-gecached → Restart nach Änderung.
- **Discord-Redirect** `https://noose.info/signin-discord` muss im Developer-Portal registriert sein.
- **Prod-Secrets** in `/etc/noose/noose.env` mit Doppel-Unterstrich: `ConnectionStrings__ProductionConnection`, `Authentication__Discord__ClientId`/`__ClientSecret`, `Bootstrap__AdminDiscordId`.
- **Health-Check:** `GET /health` (anonym, prüft DB-Konnektivität) → `200 Healthy`.

---

## Projektstruktur

```
NOOSE-Website.slnx
NOOSE-Website/
├── Components/        Razor-Pages + UI, je Feature ein Ordner
│   ├── Pages/         Account, Admin, Board, Calendar, Cases, Documents,
│   │                  Factions, Graph, Groups, Jobs, Laws, Legal,
│   │                  Operations, OrgChart, Parties, People, Personnel,
│   │                  Portal, Public, Recruiting, Search, Statistics,
│   │                  Taskforces, Watchlist
│   ├── Layout/
│   ├── Common/Shared/
│   └── Account/
├── Data/              AppDbContext, Entities/<Domain>/, Migrations/
├── Models/            DTOs, Enums/, Abstractions/
├── Services/          Business-Logik + Authorization (Graph/, Statistics/, Threat/)
├── Authorization/     Policies, Requirements, Handler, Extensions
├── Infrastructure/    Interceptors, Broadcaster, Worker, Audit, Storage
├── Theme/             NooseTheme.cs (Dark-Palette)
└── wwwroot/lib/       Quill, vis-network, FullCalendar (self-hosted)
deploy.ps1
```

---

## Weiterführende Docs

- [`CLAUDE.md`](CLAUDE.md) — Codebase-Konventionen, Architektur, Gotchas
- [`AGENTS.md`](AGENTS.md) — Agent-/Contributor-Hinweise
- [`Plan.md`](Plan.md) — Phasenplan: Status, Datenmodell, Rechte-Matrix, Glossar
- [`DEPLOYMENT.md`](DEPLOYMENT.md) — Server-Setup (nginx → Kestrel → MariaDB), systemd, Troubleshooting
- [`GoalOfTheSite.txt`](GoalOfTheSite.txt) — Original-Spec (Ränge, Feldlisten, Einstufungs-Stufen)

---

## Lizenz

Privates Fan-/RP-Projekt. **Keine** Open-Source-Lizenz — kein freies Nutzungs-, Kopier- oder Verteilungsrecht. Alle Rechte vorbehalten.
