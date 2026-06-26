# Design: Demo-Modus

> Stand: 2026-06-23 · Sprache: Deutsch · Live-Ziel: `demo.noose.info`

## 1. Ziel & Kontext

Es soll einen **Demo-Modus** geben, mit dem die komplette Website **ohne Login** durchstöbert
werden kann — mit **fiktiven Demo-Daten**. Zweck: **Portfolio-Showcase** des Autors, gehostet unter
`demo.noose.info` mit eigener Datenbank.

Die Codebase setzt durchgängig einen **authentifizierten, aktiven `Agent`** voraus: jede Seite erbt
`[Authorize(Policy = Policies.ActiveAgent)]` aus `_Imports.razor`, und Services lesen Rang, Flags und
Agent-Id direkt vom `ClaimsPrincipal`. Ein roher anonymer Zugriff würde überall mit Null-Principal
brechen. Lösung: anonyme Besucher werden bei aktivem Demo-Modus serverseitig als fester
**Demo-Agent** behandelt — alle bestehenden `[Authorize]`, Policies, Sichtbarkeits- und
Service-Checks laufen unverändert weiter.

## 2. Festgelegte Entscheidungen

| Thema | Entscheidung |
|---|---|
| Interaktion | **Nur Lesen.** Schreiben/Bearbeiten/Löschen serverseitig geblockt; Schreib-Buttons für Demo-Besucher ausgeblendet. |
| Sichtbarkeit | **Voller Lesezugriff** inkl. Verschlusssachen, Führungs-Bereiche, Klarnamen (Daten sind fiktiv). Admin-Panel bleibt **aus**. |
| Aktivierung | **Settings-Toggle** in `/admin/system`, nur für `IsAdmin`. Kein Env-Gate. |
| Einschalt-Schutz | **3-Stufen-Bestätigung** (siehe §6). |
| Ausschalten | **Ein Klick** — Beenden der Exposure ist sicher, keine Reibung. |
| Demo-Daten | **Reicher, idempotenter Seeder**, ausgelöst per **explizitem Admin-Button** (nicht Auto-Seed beim Start). |
| Identität anonymer Besucher | Synthetischer **Demo-Agent**-Principal (Active, Rang Director, TRU+HRB, `IsDemo`); **nicht** Admin, **nicht** OnlyReader. |
| Betrieb | Eigene Instanz/DB unter `demo.noose.info`. Seed-Daten nie in Prod. |

## 3. Architektur-Überblick

Anonyme Besucher erhalten bei aktivem Demo-Modus über **zwei** Eintrittspunkte denselben
synthetischen Principal (beide nötig, da Blazor Web App SSR und Interactive-Circuit getrennt
authentifiziert):

1. **`DemoModeMiddleware`** — für statisches SSR + Authorization-Middleware.
2. **`DemoAuthenticationStateProvider`** — für den Interactive-Server-Circuit (SignalR).

Der Principal ist read-only (`IsDemo`-Claim) und voll lesefähig (Rang Director + TRU + HRB,
nicht OnlyReader). Das Admin-Panel bleibt unzugänglich (kein `IsAdmin`).

```
Anonymer Request
  └─ UseAuthentication  (kein Cookie → anonym)
  └─ DemoModeMiddleware (Demo aktiv & anonym & kein Ausnahme-Pfad)
        → HttpContext.User = DemoPrincipal
  └─ UseAuthorization   (ActiveAgent-Policy: Status=Active ✓)
  └─ Statisches SSR     (sieht DemoPrincipal)
        └─ Interactive-Circuit startet
              └─ DemoAuthenticationStateProvider → DemoPrincipal
```

## 4. Setting & Konfiguration

Analog zum bestehenden Wartungsmodus (Key/Value in `SystemEinstellungen`, 10 s `IMemoryCache`).

- **`Models/Common/SystemConfiguration.cs`**
  - Neuer Key: `SystemSettingKeys.DemoModeActive = "DemoModusAktiv"`.
  - `SystemConfiguration`-Record: `bool DemoModeActive`.
  - `SystemConfigurationInput`: `bool DemoModeActive`.
- **`Services/SystemSettingService.cs`**
  - `GetAsync`: `DemoModeActive` aus Dictionary parsen (`"true"`).
  - `SaveAsync`: `DemoModeActive` upserten; weiterhin `Permission.RequireAdmin(actor)` als erste Anweisung; Cache invalidieren.
  - Kein Env-Flag.

> Ein schneller, hot-path-tauglicher Lese-Zugriff ist nötig (Middleware bei *jedem* Request).
> Der bestehende 10-s-Cache von `SystemSettingService.GetAsync()` deckt das ab.

## 5. Demo-Identität

### 5.1 Demo-Agent (DB-Zeile)
Der `DemoDataSeeder` legt einen echten `Agent` mit **fixer Id** an (Konstante, z. B.
`DemoIdentity.AgentId`), damit Audit-/FK-/Presence-Bezüge auflösen:

- Codename „Demo", Status `Active`, Rang `Director`, `IsTRU = true`, `IsHRB = true`,
  `IsAdmin = false`, `IsTeamLead = false`.
- Idempotent (Upsert per fixer Id).

### 5.2 Synthetischer Principal (`DemoIdentity`)
Statischer Builder `DemoIdentity.BuildPrincipal()` erzeugt einen `ClaimsPrincipal` mit denselben
Claim-Typen wie `AgentClaimsPrincipalFactory` plus dem Marker:

| Claim | Wert |
|---|---|
| `ClaimTypes.NameIdentifier` | `DemoIdentity.AgentId` |
| `AgentClaimTypes.Codename` | „Demo" |
| `AgentClaimTypes.Status` | `Active` |
| `AgentClaimTypes.Rank` | `Director` (int) |
| `AgentClaimTypes.IsTRU` | `true` |
| `AgentClaimTypes.IsHRB` | `true` |
| `AgentClaimTypes.IsAdmin` | `false` |
| `AgentClaimTypes.IsTeamLead` | `false` |
| `AgentClaimTypes.IsDemo` *(neu)* | `true` |

AuthenticationType gesetzt (→ `Identity.IsAuthenticated == true`).

## 6. Aktivierung — 3-Stufen-Bestätigung (UI)

Im Admin-Panel `Components/Pages/Admin/SystemManagement.razor`, nur sichtbar/ausführbar für `IsAdmin`.
Toggle ruft beim **Einschalten** einen `MudDialog`-Wizard auf:

1. **Stufe 1 — Rückfrage:** „Willst du das wirklich?" → Buttons *Ja, fortfahren* / *Abbrechen*.
2. **Stufe 2 — Konsequenzen:** Klartext-Warnung:
   *„Anonyme Besucher sehen ALLE Daten dieser Instanz — inkl. Verschlusssachen und Klarnamen — ohne Login."*
   Der **„Weiter"-Button ist 10 s gesperrt** (sichtbarer Countdown), danach klickbar.
3. **Stufe 3 — Wortprobe:** Ein **zufällig generiertes Wort** wird **nicht markier-/kopierbar**
   dargestellt (Rendering als Canvas/SVG-Bild, zusätzlich `user-select:none; pointer-events:none`).
   Der Besucher muss es **manuell exakt** in ein Textfeld tippen; erst bei Übereinstimmung wird
   „Aktivieren" frei. Vergleich case-sensitive, Wort serverseitig/komponenten-seitig erzeugt
   (kein `Math.Random`-Verbot-Problem, da Laufzeit-Komponente, nicht Workflow-Skript).

**Ausschalten** umgeht den Wizard: einfacher Toggle/Bestätigungs-Klick.

Speichern erfolgt über `SettingService.SaveAsync` (Admin-Guard greift erneut).

## 7. Durchsetzung: Lesen erlaubt, Schreiben hart aus

### 7.1 Eintrittspunkte
- **`Infrastructure/DemoModeMiddleware.cs`** (registriert nach `UseAuthentication`, vor `UseAuthorization`):
  - Wenn `config.DemoModeActive` **und** `!HttpContext.User.Identity.IsAuthenticated`
    **und** Pfad nicht in Ausnahmeliste → `HttpContext.User = DemoIdentity.BuildPrincipal()`.
  - **Ausnahme-Pfade** (Owner-Login bleibt möglich): `/Account`, `/signin-discord`, `/health`,
    Static Assets / `_blazor` / `_framework`.
- **`Infrastructure/CurrentUser/DemoAuthenticationStateProvider.cs`**:
  - Custom `AuthenticationStateProvider` für den Circuit. Liefert echten User, falls vorhanden;
    sonst — bei aktivem Demo-Modus — den Demo-Principal; sonst anonym.
  - Ersetzt/umhüllt den bestehenden revalidierenden Identity-Provider in der DI-Registrierung
    (Server-Render-Mode). Revalidierung des Demo-Principals: konstant gültig (kein SecurityStamp).

### 7.2 Schreibschutz (zwei Ebenen)
- **UI-Ebene — `Authorization/AgentPrincipalExtensions.cs`:**
  - Neu: `bool IsDemo(this ClaimsPrincipal user)` (liest `AgentClaimTypes.IsDemo`).
  - `MayWrite()` erweitern: `!IsOnlyReader() && !IsPartner() && !IsDemo()`.
    → Alle über `Policies.WriteAccess` / `MayWrite()` gegateten Schaltflächen (Anlegen/Bearbeiten/
    Löschen) sind für Demo-Besucher **nicht sichtbar** → saubere Read-only-UX.
- **DB-Ebene (Defense-in-Depth) — `Infrastructure/CurrentUser/CurrentUserService.cs` + `ReadOnlyBarrierInterceptor`:**
  - `CurrentUserInfo` um `bool IsDemo` erweitern; `Build(...)` setzt `user.IsDemo()`.
  - `ReadOnlyBarrierInterceptor.Require(...)`: zusätzlich zu `IsOnlyReader`/`IsPartner` auch
    `IsDemo` als Schreib-Veto behandeln (Whitelist `AuditLog`/`AccessLog`/`Notification` bleibt).

> Folge: Demo-Besucher = Director-Rang (Führung) + TRU + HRB, also **volle Lese-Sicht** inkl.
> VS-Dokumente (`DocumentViewerScope.CanSee`) und Klarnamen (nicht OnlyReader). `AdminPage`-Policy
> verlangt `IsAdmin` → Demo bleibt vom Admin-Bereich ausgeschlossen.

## 8. Demo-Daten

- **`Infrastructure/DemoDataSeeder.cs`** — idempotent (Muster wie `RecruitingSeeder`: vorhandene
  Demo-Marker prüfen, nur Fehlendes einfügen). Markierung der Demo-Datensätze über eine erkennbare
  Konvention (z. B. fixe Demo-Ids / Namenspräfix), damit Re-Runs nichts duplizieren.
- **Auslösung:** **expliziter Admin-Button** „Demo-Daten einspielen" in `/admin/system`
  (ruft den Seeder über einen Service/Endpoint, Admin-Guard). **Kein** Auto-Seed beim App-Start
  → verhindert Fake-Daten-Injektion auf der Prod-Instanz.
- **Umfang (reich, für lebendige Statistik & Graph):**
  - Mehrere **Personen** mit Steckbrief, Aliasen, Fotos (Platzhalter), Personen-Doks,
    Beziehungen, Beobachtungen, Einstufungen.
  - Mehrere **Fraktionen** (Mitglieder, Ränge, Konflikte/Allianzen, Aktivitäten, Einstufung).
  - **Parteien**, **Personengruppen**, **Operationen**, **Taskforces**, **Vorgänge**.
  - **Dokumente** (versch. VS-Stufen), **Gesetze**.
  - **Personal-/Beförderungs-Historie** für ein paar Demo-Agents (für Organigramm/Statistik).
- Foto-/Datei-Uploads: Platzhalter unter `App_Data/uploads` (oder mitgelieferte Demo-Assets).
- Bedrohungs-Score: wird vom bestehenden `ThreatScoreSweepWorker` über die Demo-Daten berechnet
  (Worker nutzt `ExecuteUpdateAsync`, umgeht den Read-Only-Barrier korrekt).

## 9. UI-Hinweise

- **Banner** in `Components/Layout/MainLayout.razor`: persistenter Hinweis „Demo-Modus —
  schreibgeschützt" für Demo-Besucher (`user.IsDemo()`), Muster wie der Wartungsmodus-Alert.
- **Login-Affordanz bleibt** sichtbar (Owner kann sich via Discord als Admin einloggen; ist ein
  echter User da, gilt dessen Principal, nicht der Demo-Principal).
- Schreib-Buttons verschwinden automatisch über `MayWrite()` (§7.2) — keine pro-Seite-Edits nötig.

## 10. Sicherheit & Risiken

- **Größtes Risiko:** Einschalten auf der **Prod-Instanz** würde echte Daten anonym offenlegen.
  Bewusst gewählter Schutz statt Env-Gate: Admin-only + 3-Stufen-Wizard (Rückfrage → Konsequenz +
  10-s-Lock → manuelle Wortprobe). Konsequenz-Text macht die Wirkung explizit.
- **Mitigation Betrieb:** Demo läuft als **separate Instanz mit eigener DB**; Seed-Daten nur dort.
- **Schreibschutz doppelt** (UI via `MayWrite`, DB via Interceptor) → kein Mutations-Pfad für Demo.
- **Admin-Bereich** bleibt unzugänglich (Demo ist nicht `IsAdmin`).
- **Login-Pfade** von der Middleware ausgenommen → kein Hijack des OAuth-Callbacks.

## 11. Betrieb

- `demo.noose.info` als eigener systemd-Service/Instanz mit eigener Connection-String-Konfiguration
  und eigener DB. Deploy-Details (zweites Ziel) außerhalb dieser Spec — App-seitig keine
  Host-Erkennung nötig (Demo-Modus rein über Setting der Demo-Instanz).
- Optional: Discord-Redirect `https://demo.noose.info/signin-discord` registrieren, falls Owner-Login
  auf der Demo-Instanz gewünscht.

## 12. Betroffene Dateien (Übersicht)

**Neu**
- `Infrastructure/DemoIdentity.cs` — Konstanten + `BuildPrincipal()`.
- `Infrastructure/DemoModeMiddleware.cs`.
- `Infrastructure/CurrentUser/DemoAuthenticationStateProvider.cs`.
- `Infrastructure/DemoDataSeeder.cs`.
- ggf. `Components/Pages/Admin/DemoModeConfirmDialog.razor` (3-Stufen-Wizard).

**Geändert**
- `Models/Common/SystemConfiguration.cs` — Key + Record + Input.
- `Services/SystemSettingService.cs` — Get/Save.
- `Authorization/AgentPrincipalExtensions.cs` — `IsDemo()` + `MayWrite()`.
- `Authorization/AgentClaimTypes.cs` — Claim-Konstante `IsDemo`.
- `Infrastructure/CurrentUser/ICurrentUserService.cs` + `CurrentUserService.cs` — `IsDemo` in `CurrentUserInfo`.
- `Infrastructure/Authorization/ReadOnlyBarrierInterceptor.cs` — Demo-Veto.
- `Program.cs` — Middleware-Registrierung (nach `UseAuthentication`, vor `UseAuthorization`),
  `AuthenticationStateProvider`-Registrierung, Seeder-Service.
- `Components/Pages/Admin/SystemManagement.razor` — Toggle + Wizard + „Demo-Daten einspielen".
- `Components/Layout/MainLayout.razor` — Demo-Banner.

## 13. Verifikation (manuell, kein Test-Projekt)

1. Demo-Modus aus → anonymer Zugriff auf `/personen` ⇒ Redirect zu Login (unverändert).
2. Wizard: Einschalten nur mit allen 3 Stufen möglich; „Weiter" erst nach 10 s; falsches Wort ⇒ kein „Aktivieren".
3. Demo-Modus an, anonym → komplette Site sichtbar inkl. VS/Klarnamen; **keine** Schreib-Buttons;
   `/admin/*` nicht erreichbar.
4. Schreibversuch über direkten Pfad ⇒ `UnauthorizedAccessException` (Interceptor).
5. Interactive-Aktion (z. B. Graph, Suche, Strg+K) funktioniert read-only im Circuit.
6. „Demo-Daten einspielen" zweimal ⇒ keine Duplikate (Idempotenz).
7. Owner-Login via Discord trotz aktivem Demo-Modus möglich; danach voller Admin-Principal.
8. Demo-Modus aus (ein Klick) ⇒ anonym wieder ausgesperrt.

## 14. Nicht im Scope

- Sandbox/Schreibzugriff mit periodischem Reset (bewusst Read-only).
- Env-Gate / Host-basierte Aktivierung.
- Zweites Deploy-Ziel/Automatisierung für `demo.noose.info` (Infra-seitig).
- Read-only-Variante des Admin-Panels.
