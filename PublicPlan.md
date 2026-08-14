# PublicPlan.md — Öffentlicher Bereich (Public Area)

Planungsdokument für den öffentlichen Bereich der NOOSE-Website. Status: **beschlossen, nicht begonnen.**
Ergänzt `Plan.md` (interner Phasenplan) und `AlgoPlan.md` (Bedrohungs-Score).

## Kontext

Die Seite ist heute rein intern: außen gibt es nur Landing, Karriere und den Bewerber-Pfad.
Gleichzeitig laufen zwei öffentliche Dinge weiter über Discord — die **Fahndung** (Steckbriefe, Kopfgeld)
und das **öffentliche Ticket-System**. Beides ist dort unstrukturiert, nicht durchsuchbar, nicht
nachvollziehbar und nicht mit dem Aktenbestand verknüpft.

Ziel: ein öffentlicher Bereich, der (1) Fahndungen mit Kopfgeld publiziert und Hinweise entgegennimmt,
(2) das Discord-Ticketsystem als „Führungsebenen-Ticket" ersetzt, (3) die Arbeit der Behörde in Text und
Zahlen darstellt — und der (4) vollständig aus `/einstellungen` redigierbar und modulweise abschaltbar ist.
Zwei Dinge sind nicht verhandelbar: **Agenten bleiben nach außen immer anonym**, und **kein interner
Akteninhalt gelangt versehentlich nach draußen**.

Die Codebase ist halb vorbereitet — der Bewerber-Pfad ist das exakte Vorbild (externer Discord-Login,
eigener Status, eigene Policy, eigenes Portal, anonymisierter Nachrichtenaustausch).

---

## Getroffene Entscheidungen

| Thema | Entscheidung |
|---|---|
| Zivilisten-Konto | `AgentStatus.Civilian` + neue Tabelle `BuergerProfile` (Vor-/Nachname). **Nicht** in `Agent.RealName` |
| Zugang Bürgerbereich | **Jedes angemeldete Konto** darf ihn sehen (Bürger, Agent, Partner, Nur-Lese-Aufsicht, Bewerber). `Civilian` heißt nur: dieses Konto hat sonst nichts |
| Einreichen im Bürgerbereich | Braucht ein eigenes, vollständiges, ungesperrtes `BuergerProfil` **und** Schreibrecht — Nur-Lese-Aufsicht und Partner lesen nur |
| Öffentliche Fahndung | Eigene Entity mit **Publish-Snapshot**; nie aus `Person.IsWanted` abgeleitet |
| Lesen/Schreiben | Lesen anonym, Schreiben nur nach Discord-Login mit Vor-/Nachname |
| Publish-Recht | Rang ≥ `SeniorSpecialAgent` (3) direkt; Rang 1–2 über `Request`-Antrag an Führung |
| Kopfgeld | Anteile-Tabelle: behördlich (Kasse, Konto wählbar) + **privat von Agenten**; Historie; Staffel „bis X"; Split auf mehrere Hinweisgeber |
| Privates Geld | Wahlweise vorab in die Kasse einzahlen (gesichert) **oder** Zusage + selbst auszahlen mit Audit-Bestätigung |
| Öffentliche Anzeige Kopfgeld | **Nur Gesamtsumme** — keine Aufschlüsselung, kein Stifter-Name |
| Deckung | Warnen, nicht blockieren (nur behördlicher Anteil) |
| Hinweise | Ein Eingang, Bezug optional, **manuelle** Übernahme; Bild-Anhang; Rate-Limit; Sperrliste; anonym möglich; Dubletten + Priorisierung; Vertrauensstufe |
| Ticket | Nur `Fuehrungsebene`; intern nur Führung; Absender außen immer „NOOSE – Führungsebene" |
| Vorlagen | Eigene Tabelle + **eigener** Token-Satz (4. System, strikt getrennt) |
| Bürger-Info | Nur Website, Ungelesen-Zähler. Kein Bot, keine Mail |
| Gefahrenlisten | Nur explizit publizierte Einträge; **Score live als Zahl** (nur für Publizierte) |
| Steckbrief öffentlich | Foto, Name, Aliase, Vorwurfstext, Warnhinweis-Chips, Gefahrenstufe + Score, Kopfgeld-Gesamtsumme, öffentliches Aktenzeichen |
| Leck-Schutz | Snapshot · harte VS-Sperre · Warnbanner in der Akte · `PublicVisibility` + Coverage-Test · Ablaufdatum + Auto-Depublikation |
| Suche/NOOSEI | Intern suchbar **und** NOOSEI-lesbar; öffentliche Suche ist ein eigener, schmaler Pfad |
| Redaktion | Pressemitteilungen · öffentliche Lageberichte · FAQ/Zuständigkeiten · Gesetzesauszüge + Warnhinweise |
| Weitere Boards | GEFASST-Archiv · Vermisste/Zeugenaufruf · gesuchte Fahrzeuge & Waffen · verbotene/beobachtete Organisationen |
| Zahlen | Fahndungs-/Hinweis-Zähler · Gefahrenlage-Ampel · Gefahrenlage-Trend |
| Reichweite | Hinweis-Verfolgung · Druckposter · Discord-Push. **Kein** OpenGraph |
| Einstellungen | Modul-Schalter + Offline-Text je Modul · Entwurf/Veröffentlicht + Vorschau · Public-Nav pflegbar · globaler Kill-Switch |
| Extras | Einspruch gegen Ausschreibung · Bürgerkonto ↔ Personenakte · Gegenaufklärungs-Regel auf Hinweisgeber |
| Routen | `/gesucht`, `/gesucht/{Aktenzeichen}`, `/gefasst`, `/buerger`, `/presse`, `/lage`, `/warnungen`, `/organisationen`, `/recht`, `/info/{Slug}` — internes `/fahndung` bleibt unangetastet |
| Indexierung | Öffentliche Routen indexierbar, alles Interne per `robots.txt` **und** `noindex`-Header gesperrt |

Bewusst **nicht** dabei: Namensänderungs-Freigabe (die Sperre hängt am Discord-Konto, nicht am Namen —
Umbenennen umgeht nichts; Änderungen werden nur protokolliert), OpenGraph-Karten, Organigramm öffentlich,
Discord-DM/E-Mail an Bürger, öffentliche Termine.

**Offener Einwand, bewusst überstimmt:** Der Bedrohungs-Score öffentlich als Zahl legt die Mechanik aus
`AlgoPlan.md` offen — Spieler können ableiten, welche Handlungen Punkte bringen, und gezielt darunter
bleiben. Wurde abgewogen und so entschieden (nur für publizierte Einträge).

---

## Architektur-Leitsätze

1. **Öffentlich ist eine eigene Achse, kein Filter.** `Services/Public/PublicVisibility.cs` ist die einzige
   Wahrheit darüber, was außen sichtbar ist — Vorbild `Services/Visibility.cs` und `Services/AgentSelection.cs`.
   `PublicVisibilityCoverageTests` reflektiert über alle `DbSet`s: jede Entität ist entweder als öffentlich
   projizierbar eingetragen oder in `PublicVisibility.NeverPublic` **mit Begründung**. Muster:
   `SearchCoverageTests`. Das ist die Garantie — nicht die Sorgfalt beim Review.
2. **Snapshot statt Live-Lesen.** Öffentliche Datensätze halten eigene Textfelder. Einziger Live-Wert ist der
   Bedrohungs-Score publizierter Akten (so entschieden).
3. **Öffentliche Services sind eine eigene Schicht** (`Services/Public/`) und lesen nie über die internen
   Listendienste. Kein `ISearchProvider`-Reuse für die öffentliche Suche.
4. **Anonymität ist mechanisch.** Öffentliche Projektionen (`Models/Public/*`) haben baulich kein Feld für
   Codename/Klarname/Dienstgrad. Ein Datei-Scan-Test hält das fest (Muster `NooseiCostVisibilityTests`).
5. **Render-Modus:** öffentliche Lese-Seiten `[ExcludeFromInteractiveRouting]` + Memory-Cache (Muster
   `SystemSettingService`, 10 s). Nur Formulare, Hinweis-Dialog und Ticket-Chat sind `InteractiveServer` —
   sonst öffnet jeder anonyme Besucher einen SignalR-Circuit.
6. **Wartungsmodus greift nicht nach außen.** `MainLayout` erzwingt ihn intern, die öffentlichen Layouts nicht.
   Dafür der separate Kill-Switch.
7. **Modul-Aus wirkt im Service, nicht nur in der UI.** `IPublicModuleService.RequireEnabled(module)` wirft;
   die Route liefert den Offline-Text, der Nav-Tab verschwindet.
8. **Der Bürgerbereich ist für jedes angemeldete Konto lesbar.** Gate ist
   `AgentPrincipalExtensions.MayUseCitizenPortal()` / `Permission.RequireCitizenPortal` (angemeldet), **nicht**
   `IsCitizen()` — ein Agent, Partner, Bewerber oder eine Aufsicht hat auch eine Zivil-Identität, und eine
   Statusprüfung als Zugangsgate hätte jede neue Bürgerseite erneut falsch gebaut. `IsCitizen()` bleibt und
   beantwortet nur noch eine andere Frage: *muss* dieses Konto ein Profil anlegen (Profil-Zwang im Layout).
   Jede Bürgerseite, die Daten des Kontos zeigt, geht weiter über `BuergerProfilId` — wer keins hat, sieht die
   Seite ohne eigene Zeilen. Kein Recht am Aktenbestand hängt daran: der Bürgerbereich gibt nichts Internes her.
9. **Nachvollziehbarkeit:** Publizieren, Depublizieren, Kopfgeld-Änderung, Anonymitäts-Auflösung und
   Ticket-Antwort schreiben `ManualAudit.Row` **gegen die Personen-/Fraktionsakte** (⇒ Zeitstrahl + Chronik).
   Neue Kind-Tabellen brauchen ihren Fall in `TimelineService.AuditSourceAsync` **und** einen Titel in
   `TimelineDisplay.MapAudit`.

---

## Phasenübersicht

| # | Phase | Migration | Abhängt von | Status |
|---|---|---|---|---|
| 1 | Bürger-Login & Profil | `Phase61_BuergerKonto` | — | **fertig** |
| 2 | Modul-Schalter, Kill-Switch, Nav, Indexierung, `PublicVisibility` | `Oeffentlich02_Module` | 1 | **fertig** |
| 3 | CMS-Seiten (Text zur Arbeit, FAQ, Zuständigkeiten) | `Oeffentlich03_Seiten` | 2 | **fertig** |
| 4 | Fahndung Kern: publizieren, Board, Steckbrief | `Oeffentlich04_Fahndung` | 2 | **fertig** |
| 5 | Fahndung Ausbau: Warnhinweise, Ablauf, Archiv, Druck, Push | `Oeffentlich05_Warnhinweise` | 4 | **fertig** |
| 6 | Kopfgeld: Anteile, behördlich + privat, Deckung, Historie | `Oeffentlich06_Kopfgeld` | 4 | offen |
| 7 | Hinweise Kern: Formular, Eingang, Rückfrage, Verfolgung | `Oeffentlich07_Hinweise` | 4 | offen |
| 8 | Hinweise Ausbau: Dubletten, Priorität, Vertrauen, Übernahme | keine | 7 | offen |
| 9 | Belohnung: Split-Zuordnung, Auszahlung, Beleg | `Oeffentlich09_Belohnung` | 6, 7 | offen |
| 10 | Ticket-Chat (Führungsebene) | `Oeffentlich10_Tickets` | 2 | offen |
| 11 | Öffentliche Vorlagen + 4. Token-System | `Oeffentlich11_Vorlagen` | 7, 10 | offen |
| 12 | Organisationen + Gefahrenlisten | `Oeffentlich12_Fraktionsprofile` | 4 | offen |
| 13 | Gesuchte Fahrzeuge/Waffen + Einspruch | `Oeffentlich13_FahrzeugeEinspruch` | 4, 1 | offen |
| 14 | Presse, Lageberichte, Gesetzesauszüge, Warnungen | `Oeffentlich14_Redaktion` | 3 | offen |
| 15 | Zahlen: Gefahrenlage-Ampel, Trend, Zähler, Landing-Hero | keine | 4, 7, 14 | offen |
| 16 | Suche & NOOSEI-Anbindung + interne KPIs | keine | 4, 7, 10 | offen |

> **Migrations-Präfix `Oeffentlich<Phase>_`.** Die interne Phasen-Zählung des Projekts steht schon bei
> `Phase69_*`; die ursprünglich geplanten Namen `Phase61`–`Phase74` hätten sechs bestehende Migrationen
> doppelt belegt (`Phase61_Finanzierungsantraege`, `Phase62_Feedback`, `Phase63_FeedbackStatus`,
> `Phase65_KiKontingente`, `Phase66_NooseiKurzbrief`, `Phase67_NooseiUnterhaltungen`, `Phase68`/`Phase69`).
> Der öffentliche Bereich zählt deshalb in einer eigenen Reihe, deren Nummer die **Planphase** ist.
> Ausnahme: Phase 1 heißt noch `Phase61_BuergerKonto` — sie war bereits angewendet, als die Kollision
> auffiel, und ein Umbenennen hätte die Migrations-Historie der Entwicklungs-DB von Hand korrigieren müssen.

Nach jeder Phase gilt derselbe Abschluss: `dotnet build` ohne Warnung, `dotnet test` grün,
`dotnet run` und die genannten Klickpfade wirklich durchgehen. Nach 3, 9 und 16 sind natürliche Deploy-Punkte.

---

## Phase 1 — Bürger-Login & Profil

**Ziel:** Ein Spieler kann sich per Discord als Bürger anmelden, gibt Vor- und Nachnamen an, und Agenten
können ein Konto sperren. Noch kein öffentlicher Inhalt.

**Daten** (`Phase61_BuergerKonto`)
- `Models/Enums/AgentStatus.cs`: `Civilian = 5`.
- `Data/Entities/Public/BuergerProfil.cs` → `BuergerProfile`: `UserId` (1:1 → `Agent`, `DeleteBehavior.Restrict`),
  `Vorname`, `Nachname`, `IstGesperrt`, `SperrGrund`, `GesperrtVonId`, `GesperrtAm`,
  `BestaetigteHinweise` (int, Vertrauensstufe), `LinkedPersonId` (nullable), `IAuditable`, `ISoftDelete`.

**Code**
- `Policies.CitizenPortal` + Registrierung in `AuthorizationRegistration` neben `ApplicantPortal`
  (`RequireAssertion(ctx => ctx.User.MayUseCitizenPortal())` — jedes angemeldete Konto, siehe Leitsatz 8).
- `AgentPrincipalExtensions.IsCitizen()` (Statusfrage) und `MayUseCitizenPortal()` (Zugangsfrage);
  `Permission.RequireCitizenPortal` ist der Service-Guard, `RequireWriteAccess` kommt beim Schreiben dazu.
- `IdentityComponentsEndpointRouteBuilderExtensions`: Zweig `source == "buerger"` →
  `CreateAgentAsync(..., AgentStatus.Civilian)`; im Status-`switch` Fall `Civilian` → SignIn + `/buerger`.
  Bestehende Zweige unangetastet.
- `IBuergerService`: Profil lesen/anlegen/ändern (Namensänderung → `ManualAudit.Row`), sperren/entsperren
  (`Permission.RequireLeadership`), `RequireNotBlocked` als Guard für spätere Schreibpfade.
- `Components/Layout/BuergerLayout.razor` (Kopie-Muster `ApplicantPortalLayout`) und
  `Components/Pages/Portal/BuergerPortal.razor` (`/buerger`) + `BuergerProfil.razor` (`/buerger/profil`).
  Profil-Zwang im **Layout**, nicht nur in der UI: ohne Vor-/Nachname wird auf `/buerger/profil` umgeleitet —
  aber **nur für `IsCitizen()`**. Ein Agent, Partner oder Bewerber sieht den Bereich ohne Zivil-Identität; eine
  Nur-Lese-Aufsicht könnte gar keine anlegen und liefe sonst in eine Umleitungsschleife. Das Layout trägt für
  diese Konten einen Rückweg (`/dashboard` bzw. `/portal`).
- `/einstellungen` → neue Gruppe „Öffentlicher Bereich", erster Abschnitt `PublicCitizensPanel`
  (Liste, Suche, Sperren mit Grund).
- `Privacy.razor` + `Nutzungsbedingungen.razor`: Bürgerdaten, Zweck, Aufbewahrung, Anonymitätszusage.
- `AgentSelection` **nicht** anfassen — `Civilian` ist nicht `Active`, fällt also überall automatisch heraus.
  Das ist mit einem Test festzuhalten, nicht mit Code.

**Tests** `CitizenLoginFlowTests` (Status-Weiche, bestehende Zweige unverändert) · `BuergerServiceTests`
(Sperre blockt Schreibpfade, Namensänderung erzeugt Audit-Zeile, Agent/Bewerber dürfen ein Profil anlegen,
Aufsicht/Partner/Anonym nicht) · `AgentSelectionTests` erweitern: ein `Civilian` erscheint in **keinem**
Picker, keiner Filterliste, keinem Roster.

**Fertig, wenn** ein zweiter Discord-Account sich anmelden, Namen setzen und wieder anmelden kann, im
Roster nirgends auftaucht, eine Sperre ihn beim nächsten Schreibversuch abweist — und ein angemeldeter
Agent `/buerger` öffnen kann, ohne zur Namenseingabe gezwungen zu werden.

---

## Phase 2 — Modul-Schalter, Kill-Switch, Nav, Indexierung, `PublicVisibility`

**Ziel:** Das Gerüst, in das alle späteren Module eingehängt werden. Ab hier ist jedes neue Modul
per Definition abschaltbar.

**Daten** (`Oeffentlich02_Module`)
- `Data/Entities/Public/OeffentlichesModul.cs` → `OeffentlicheModule`: `Schluessel` (unique),
  `IstAktiv`, `OfflineText`, `Reihenfolge`, `LabelUeberschreibung`, `IconUeberschreibung`, `IAuditable`.
  **Kein `ISoftDelete`:** der Katalog im Code besitzt die Schlüssel, die Zeile nur die Wahl des Betreibers —
  eine gelöschte Zeile würde das Modul stillschweigend auf seinen Standard zurückfallen lassen statt aus zu bleiben.
- `SystemSettingKeys`: `PublicAreaKillSwitch` (`OeffentlicherBereichNotAus`).

**Code**
- `Services/Public/PublicModules.cs` — statischer Katalog **aller 21** Modul-Schlüssel (auch der noch nicht
  gebauten), je Zeile Label, Beschreibung, Icon, Nav-Route, Gruppe, Reihenfolge, `DefaultEnabled`, `Available`.
  `PublicModuleSeeder` legt beim Start fehlende Zeilen an und **überschreibt nie** eine gespeicherte Wahl.
- Zwei Module sind sofort scharf, damit die Phase überhaupt nachweisbar ist: `Karriere` (Seite + Nav-Tab +
  Landing-Kachel + `source=bewerbung`-Login) und `BuergerRegistrierung`. Beide stehen auf „an", weil ein
  Standard-„aus" eine bestehende Funktion abgeschaltet hätte. **`BuergerRegistrierung` steuert nur die
  Neuanmeldung** — ein bestehendes Bürgerkonto behält seinen Zugang, sonst sperrt ein Versehen Leute aus
  ihren eigenen laufenden Hinweisen aus.
- `IPublicModuleService` + Impl: `GetAsync` (`IMemoryCache`, 10 s), `IsEnabledAsync`, `RequireEnabledAsync`
  (wirft), `OfflineTextAsync`, `NavEntriesAsync`, `SaveAsync` (`Permission.RequireAdmin`),
  `KillSwitchSetAsync`. Kill-Switch schlägt jedes Einzelmodul, **ohne** gespeicherte Wahlen zu verändern.
  `PublicModuleState` trägt Effektiv-Werte **und** die rohen Overrides — ein Eingabefeld, das mit einem
  gemergten Standard vorbelegt ist, macht beim ersten Speichern jeden Standard zum Override.
- **Icon-Overrides sind eine Allowlist, kein Freitext** (`PublicModules.IconChoices`, gespeichert wird der
  Name): MudBlazor rendert einen Icon-Wert als Markup, ein freies SVG liefe bei jedem anonymen Besucher.
- `Services/Public/PublicVisibility.cs` + `PublicVisibilityCoverageTests` — jetzt eingeführt, solange die
  `Publishable`-Liste noch (fast) leer ist. Jede Entität aus späteren Phasen muss sich eintragen, sonst roter Build.
- `Components/Common/Shared/PublicModuleGate.razor` — rendert Inhalt oder Offline-Text; sitzt **in der Seite**,
  nicht nur in der Nav, weil eine bekannte URL die Route trotzdem erreicht.
- `Components/Layout/PublicNav.razor`: hartkodiertes `Tabs`-Array raus, aus `IPublicModuleService` gespeist,
  nach `Reihenfolge`, mit Trenner zwischen den Gruppen (Fahndung / Behörde / Service). „Start" ist kein Modul
  und bleibt fest. `PublicSiteLayout` zeigt bei Not-Aus einen Banner — die Startseite bleibt erreichbar,
  ihre Inhalte sind weg.
- **Ein eingeschaltetes Modul ohne Seiten bekommt trotzdem keinen Tab** (`NavEntries()` filtert `Available`):
  Vorab-Einschalten ist der Sinn der Sache, ein Tab auf eine 404-Route nicht. Die Wahl bleibt gespeichert und
  wirkt, sobald die bauende Phase `Available` umstellt — also **jede Phase muss ihr Modul dort umstellen**.
- `/einstellungen?tab=oeffentliche-module`: `PublicModulesPanel` (Schalter, Offline-Text, Reihenfolge,
  Label/Icon, Not-Aus) — hängt an `Policies.AdminPage`; Module ohne Seiten tragen „in Vorbereitung".
- `Services/Public/PublicRoutes.cs` als einzige Wahrheit der öffentlichen Pfade (Nav-Routen **abgeleitet**
  aus dem Katalog + Extras), `wwwroot/robots.txt`, `PublicIndexingMiddleware` setzt
  `X-Robots-Tag: noindex, nofollow` außerhalb dieser Pfade. `/buerger` ist bewusst **nicht** dabei: das
  Konto eines Bürgers ist privat, nicht öffentlich.
- **Der Schalter gilt auch am Login-Endpoint:** `source=bewerbung` und `source=buerger` prüfen ihr Modul,
  bevor ein Konto entsteht — eine versteckte Schaltfläche lässt den POST offen.

**Tests** (87 neue) `PublicModuleServiceTests` (Cache, Kill-Switch schlägt Einzelmodul und lässt Wahlen
unberührt, `RequireEnabled` wirft, Admin-only, Seeder idempotent und überschreibungsfrei, Icon-Allowlist,
Audit-Zeile für den Not-Aus, Nav-Reihenfolge) · `PublicModulesCatalogTests` (Konsistenz; „nur ein gebautes
Modul darf standardmäßig an sein") · `PublicVisibilityCoverageTests` (scharf über alle `DbSet`s) ·
`PublicRoutesTests` (Segmentgrenze, `/fahndung` bleibt intern neben `/gesucht`, robots.txt deckt jeden
öffentlichen Pfad) · `PublicIndexingMiddlewareTests`.

**Fertig, wenn** ein Modul im Panel ausgeschaltet werden kann und die zugehörige Route den Offline-Text
zeigt, der Tab verschwindet und der Kill-Switch alles auf einmal abschaltet.

**Nachgewiesen** (13.08.2026, laufende App, Modul in der DB umgeschaltet):
Karriere aus ⇒ `/karriere` Offline-Text, Nav-Tab und Landing-Kachel weg · Karriere an ⇒ alles zurück ·
Not-Aus ⇒ Banner, alle Tabs weg, Kachel weg, Discord-Formular weg, Route offline · Not-Aus aus ⇒ zurück.
`X-Robots-Tag` gesetzt auf `/dashboard`, `/personen`, `/einstellungen`, `/nachweis`, `/buerger`,
`/Account/Login`; **nicht** auf `/`, `/karriere`, `/robots.txt`. Seeder: 21 Zeilen, davon 2 aktiv.
**Nicht geprüft:** das Panel selbst (braucht Admin-Login über Discord) und der Bürger-/Bewerber-Login-Pfad.

---

## Phase 3 — CMS-Seiten ✅

**Ziel:** „Text zu unserer Arbeit" — Auftrag, Befugnisse, Zuständigkeiten, FAQ — redigierbar mit Entwurf,
Vorschau und bewusstem Veröffentlichen.

**Daten** (`Oeffentlich03_Seiten`)
- `OeffentlicheSeite` → `OeffentlicheSeiten`: `Slug`, `Titel`, `MenueTitel`, `Icon`, `Reihenfolge`,
  `InhaltHtml` (`longtext`, veröffentlicht), `EntwurfHtml` (`longtext`), `Status`
  (`Entwurf`/`Veroeffentlicht`), `ImMenue`, `VeroeffentlichtAm`/`VonId`, `IAuditable`, `ISoftDelete`.
- **Zwei Abweichungen von der Planzeile, beide bewusst:**
  - `Slug` ist **nicht unique indexiert**. Bei Soft-Delete würde ein Unique-Index die Adresse für immer
    blockieren — eine gelöschte Seite behält ihren Slug. Eindeutigkeit prüft der Dienst über die *lebenden*
    Zeilen. (Der Prüfausdruck ist ausgeschrieben, weil `Id != input.Id` mit `null` zu SQL-`NULL` übersetzt
    und die Prüfung damit stumm nichts gefunden hätte.)
  - `IstAktiv` heißt `ImMenue` und bedeutet **nicht** „veröffentlicht" — das entscheidet `Status`. Zwei
    Schalter für dieselbe Frage wären eine Fehlerquelle; `ImMenue = false` heißt „veröffentlicht, aber nur
    über den direkten Link erreichbar" (z. B. Nutzungshinweise, die ein Formular verlinkt).

**Code**
- `IPublicPageService`: `SaveDraftAsync` (Entwurf), `PublishAsync`, `RetractAsync`, `GetPreviewAsync`,
  `GetAsync`/`GetMenuAsync` (öffentlich, 10 s `IMemoryCache`), `GetAllAsync` (Panel), Papierkorb-Paar.
  **`EntwurfHtml` und `InhaltHtml` haben je eine Bedeutung:** der Entwurf ist die Arbeitskopie, der Inhalt
  wird *nur* vom Veröffentlichen geschrieben. Ohne diese Trennung wäre jedes Speichern eine Publikation.
  HTML läuft über `HtmlCleanup.Clean` — **nicht** `CleanAiPayload` — und beim Veröffentlichen **noch einmal**,
  weil das der Moment ist, in dem es anonym erreichbar wird.
- **Rechte:** Lesen (Panel + Vorschau) `Permission.RequireClassifiedRead` — die Nur-Lese-Aufsicht muss sehen,
  was die Behörde nach außen sagt. Schreiben `Permission.RequirePublicPageWrite` (Führung **und** `MayWrite`),
  neu nach dem Muster von `RequireMeetingWrite`.
- `Services/Public/PublicPageSlug.cs`: `Normalize` faltet einen deutschen Titel (Umlaute, `ß`, `§`) in einen
  Slug, `IsValid` erlaubt nur Kleinbuchstaben, Zahlen und einzelne Bindestriche. Der Dienst normalisiert
  **und** validiert — eine Adresse landet in einer Route, also wird sie beim Schreiben geprüft, nicht beim
  Lesen escaped.
- `Components/Pages/Public/InfoHub.razor` (`/info`) und `InfoPage.razor` (`/info/{Slug}`), beide
  `[AllowAnonymous]` + `[ExcludeFromInteractiveRouting]` + `PublicModuleGate`. Statt eines Nav-Tabs je Seite
  gibt es **einen** Tab „Information" auf den Hub — die Nav bleibt damit einzige Quelle: `PublicModules`.
  Geladen wird in `OnParametersSetAsync` mit Guard (dieselbe Instanz überlebt den Wechsel zwischen Slugs).
- **Der Inhalt wird als rohes `MarkupString` gerendert, nicht über `RichHtml`**: das löst `@{Typ:GUID}`-
  Erwähnungen auf und würde interne Aktennamen nach draußen schreiben.
- **`?vorschau=…` ist als `string` gebunden, nicht als `bool`.** Blazor antwortet auf einen Wert, den es nicht
  in ein `bool` parsen kann, mit **HTTP 500** — auf einer Route, an die jeder eine Query hängen kann. (Genau
  so aufgefallen: `?vorschau=1` war live ein 500.)
- `/einstellungen?tab=oeffentliche-seiten` → `PublicPagesPanel` (Liste mit Status, „Entwurf abweichend",
  „nicht verlinkt", Vorschau-Link, Veröffentlichen/Zurückziehen/Löschen; Editor mit `RichTextEditor`).
  **Ohne `Ai` und ohne `Mentions`** — beides ist Opt-in, und öffentlicher Text darf keine Erwähnung tragen.
- `PublicPageSeeder`: `auftrag`, `befugnisse`, `zustaendigkeiten`, `faq` als **Entwürfe** mit Platzhaltertext.
  Legt nur fehlende Slugs an, überschreibt nie, und **weckt keine gelöschte Seite wieder auf** (er zählt
  soft-gelöschte Zeilen als bekannt). `InhaltHtml` bleibt `NULL` — nichts geht durch ein Deploy online.
- `PublicModules`: `Infoseiten` bekommt `NavRoute = "/info"` und `Available = true`, bleibt aber
  `DefaultEnabled = false`. Einschalten ist eine Entscheidung, kein Deploy-Nebeneffekt.
- `TrashService` + `TrashProjection.PublicPage` (Adresse + Status als Detail). **Wiederherstellen bringt die
  Seite als Entwurf zurück** — ein Rückgängig darf nichts nebenbei wieder veröffentlichen.
- Achsen-Einträge: `PublicVisibility.Publishable`, `SearchCatalog.NotSearchable` (eigener Provider erst in
  Phase 16), `AuditEntityDisplay` (Label **und** Route auf den bearbeitenden Abschnitt),
  `MergedPageSections.Settings` + `.Trash`, `FeedbackPageTabs`.

**Tests** (+95, gesamt 5373 grün) — `PublicPageSlugTests` (Slug-Form, Umlaut-Faltung, Kürzen ohne
Trenner-Rest, Starter-Seiten haben gültige Slugs und Allowlist-Icons) · `PublicPageServiceTests`
(Seeder ×4 · Entwurf anonym unerreichbar · Speichern ändert die veröffentlichte Ausgabe **nicht** ·
Veröffentlichen kopiert · Slug case-insensitiv · Modul-Aus verbirgt auch Veröffentlichtes · Menü-Ordnung ·
nicht verlinkte Seite bleibt per Link erreichbar · Vorschau trotz Modul-Aus · Vorschau für die Aufsicht,
nicht für einfache Agenten · Doppel-Slug, Slug-Wiederverwendung nach Löschen · Icon-Allowlist gegen
`<script>` · Sanitizer beim Speichern **und** beim Veröffentlichen · leerer Entwurf nicht publizierbar ·
Zurückziehen behält den Entwurf · Wiederherstellen als Entwurf · Cache samt Invalidierung · Audit-Zeilen
ohne `ManualAudit`).

**Fertig, wenn** eine Seite als Entwurf geschrieben, als Agent vorab betrachtet und dann veröffentlicht
werden kann — und der Entwurf vorher anonym nicht erreichbar war.

**In der Nachprüfung gefunden und behoben** (Details in `CODE_REVIEW_TODO.md` nicht nötig, hier ist die Quelle):
1. **Wiederherstellen konnte den ganzen Bereich abschalten.** Adresse einer gelöschten Seite neu belegen ist
   erlaubt; danach die alte Seite aus dem Papierkorb holen ergab **zwei lebende Seiten auf einer Adresse**, und
   `ToDictionary` warf — vom `catch` verschluckt, also war `/info` **komplett** leer, nicht nur die eine Seite.
   Zwei Fixes: `RestoreAsync` weist die Kollision mit Begründung ab, und der Lesepfad dedupliziert ohnehin
   (`GroupBy`), damit ein Duplikat höchstens die strittige Seite kostet. Beides mit Regressionstest.
2. **Nur ein Bild war „leerer Entwurf".** Die Leer-Prüfung sah nur Klartext; eine Seite, die aus einem
   Organigramm besteht, war nicht publizierbar.
3. **`DraftHtml = null` löschte den Text still.** Jetzt heißt `null` „Entwurf unverändert", `""` heißt „leeren" —
   ein Aufruf, der nur den Titel ändert, kann den Text nicht mehr verlieren.
4. **Die Panel-Liste zog jeden Entwurf mit.** Bilder liegen als base64 im Body, also lud ein Tab-Wechsel
   potenziell Megabyte pro Seite. `PublicPageEdit` trägt kein HTML mehr; der Editor holt den einen Entwurf über
   `GetDraftAsync`.
5. **`GetAllAsync` lud den ganzen Identity-User** des Veröffentlichenden (inkl. `RealName`) in ein Panel, das die
   Nur-Lese-Aufsicht rendert. Jetzt projiziert auf den Codename.
6. **`/info` stand doppelt** in `PublicRoutes` — als Modul-Route und als „Extra", genau die Wiederholung, gegen
   die die Klasse geschrieben ist. Entfernt; `robots.txt` deckt `/info` weiter ab (nachgemessen).
7. Kleinere: fehlende Seite trägt jetzt `noindex` (`/info` ist indexierbar, die Route antwortet für jeden
   erfundenen Slug) · Vorschau-Link zeigt auf die **gespeicherte** Adresse, nicht auf das bearbeitete Feld ·
   `GetDraftAsync`-Fehler im Panel gefangen statt in den Circuit · „Entwurf abweichend"-Chip nur neben einer
   veröffentlichten Seite · Vorschau-Slug case-insensitiv wie der öffentliche Pfad.

**Nachgewiesen** (laufender Server, MariaDB, anonym):
- Migration angewendet, 4 Seiten als Entwurf geseedet (`InhaltHtml` = `NULL`), Modul `Infoseiten` bleibt aus.
- Modul aus: `/info` **und** `/info/auftrag` zeigen den Offline-Text; kein Tab in der Nav.
- Modul an, nichts veröffentlicht: Hub zeigt „Derzeit sind keine Informationsseiten veröffentlicht".
- Eine Seite veröffentlicht: Hub listet **genau** einen Link (`/info/auftrag`), die drei Entwürfe nicht;
  die Seite rendert Inhalt und Stand-Datum; `/info/faq` antwortet „Seite nicht gefunden".
- `?vorschau=1`/`=true`/`=quatsch` anonym: HTTP 200, kein Entwurf, kein Vorschau-Banner (vor dem Fix: 500).
- Not-Aus schlägt das Modul: veröffentlichter Inhalt weg, kein Tab, Banner auf der Startseite,
  `/buerger` weiter erreichbar.
- `X-Robots-Tag`: `/info`, `/info/auftrag` ohne Header; `/personen` mit `noindex, nofollow`.
- Erfundener Slug (`/info/gibtsnichtxyz`): HTTP 200 mit `noindex`-Meta und „Seite nicht gefunden".
- `robots.txt` deckt `/info` weiter ab, obwohl der Extra-Eintrag entfernt wurde (Ableitung aus dem Katalog greift).
- Serverlog ohne Exception (nur die bekannte https-Port-Warnung).
- **Nicht geprüft:** das Panel selbst und der Quill-Editor — dafür braucht es einen Discord-Login mit
  Führungsrang. Panel-Logik hängt vollständig am getesteten Dienst.

---

## Phase 4 — Fahndung Kern ✅

**Ziel:** Der eigentliche Kern. Eine Person wird bewusst publiziert, erscheint auf `/gesucht` und hat einen
Steckbrief. Noch ohne Kopfgeld, ohne Hinweis-Button.

**Daten** (`Oeffentlich04_Fahndung`)
- `OeffentlicheFahndung` → `OeffentlicheFahndungen`: `Aktenzeichen` (eigener Präfix **`FA`** über
  `CaseNumberCounter`), `Art` (`Fahndung`/`Vermisst`/`Zeugenaufruf`/`Fahrzeug`/`Waffe` — Enum jetzt komplett,
  genutzt zunächst nur `Fahndung`), `PersonId` (nullable), `FraktionId` (nullable),
  **Snapshot:** `AnzeigeName`, `AliaseText`, `FotoDateiname`, `FotoTyp`, `FotoQuellId`, `VorwurfHtml`
  (`longtext`), `LetzteGegend`, `FahrzeugText`, `OeffentlicheGefahrenstufe`; `Status`
  (`Entwurf`/`Beantragt`/`Veroeffentlicht`/`Gefasst`/`Zurueckgezogen`/`Abgelaufen`), `AblaufDatum` (nullable),
  `KopfgeldIstObergrenze`, `VeroeffentlichtAm`/`VonId`, `ZurueckgezogenAm`/`-Grund`, `GefasstAm`,
  `AufrufZaehler`, `IAuditable`, `ISoftDelete`.
- `Request`: eine neue nullable Spalte `VeroeffentlichungFahndungId` (kein FK, Muster der `Freigabe*`-Gruppe).
- `RequestType`: `Veroeffentlichung`.

**Sechs bewusste Abweichungen von der Planzeile**
1. **Präfix `FA`, nicht `F`.** `F` gehört seit `FactionService.cs` den Fraktionen, und `CaseNumberCounter` ist
   auf `(Präfix, Jahr)` verschlüsselt — geteilt benennte ein Aktenzeichen zwei Aktenarten.
2. **`Aktenzeichen` ist nullable** und wird erst bei der *ersten* Publikation geprägt: ein Entwurf, der nie
   rausgeht, verbrennt keine öffentliche Nummer. Der Index darauf ist **unique** — anders als beim Seiten-Slug,
   denn eine Zählernummer wird nie wiederverwendet.
3. **Kein Live-`ThreatScore` nach außen.** `OeffentlicheGefahrenstufe` ist ein `HazardLevel`, festgehalten beim
   Publizieren, mit einer Aktion „Stufe aktualisieren" im Panel. Der rohe 0–100-Wert wäre der einzige
   verbliebene Grund, `Personen` für *Inhalt* zu lesen — und die Score-Konfiguration steht in
   `PublicVisibility.NeverPublic` ausdrücklich als „Anleitung zur Umgehung".
4. **Der Foto-Endpoint ist `[AllowAnonymous]`**, nicht „autorisiert": ein Fahndungsfoto, das nur Agenten sehen,
   ist kein Fahndungsfoto. Die Autorisierung *ist* die Publikationsprüfung. Er liegt unter `/gesucht/{az}/foto`
   und liefert **eine** `404` für jeden Fehlschlag — unbekannt, Entwurf, zurückgezogen, Modul aus, Not-Aus,
   eingestufte Akte, fehlende Datei —, sonst wäre er ein Existenz-Orakel.
5. **Das Foto wird beim Publizieren kopiert** (`App_Data/uploads/fahndung`, eigener
   `IPublicWantedPhotoStorageService`). `PersonService.PhotoRemoveAsync` löscht die Datei hart, während die
   Zeile nur soft-gelöscht wird — eine Referenz zerrisse den Steckbrief lautlos. Getrennter Basispfad heißt
   außerdem: der anonyme Endpoint kann baulich keine interne Datei erreichen.
6. **Kein `ManualAudit.Row` gegen die Personenakte** (Leitsatz 9). `OeffentlicheFahndung` ist `IAuditable`, der
   Interceptor schreibt die Zeile selbst; eine zusätzliche `Person`-getypte Zeile fiele in
   `TimelineDisplay.MapAudit` durch den Schwanz und läse sich als generisches „Akte geändert". Stattdessen
   Fan-out über `TimelineService.AuditSourceAsync` + eigener `MapAudit`-Arm — dasselbe Muster wie bei
   `PublicPageService.PublishAsync`.

**Code**
- `Services/Public/IPublicWantedService`: Entwurf anlegen (Snapshot aus der Personenakte ziehen),
  Snapshot aktualisieren, publizieren, zurückziehen mit Grund, `CapturedAsync`, `RefreshHazardLevelAsync`,
  Papierkorb-Paar, `RetractForRecordAsync` und die vier Antrags-Methoden.
  **Publish-Guards in dieser Reihenfolge:** `Permission.RequirePublicWantedWrite` →
  `IPublicModuleService.RequireEnabledAsync` → **harte VS-Sperre** (alle drei VS-Flags ⇒
  `InvalidOperationException`, rangunabhängig, auch für Admin) → Vollständigkeit + Erwähnungs-/Platzhalter-Verbot
  → Rang ≥ 3 publiziert direkt, Rang 1–2 erzeugt `Request` mit `RequestType.Veroeffentlichung`.
  - **Zwei neue Guards.** `RequirePublicWantedWrite` ist **nicht** `RequireWriteAccess`: das blockt nur Aufsicht
    und Partner, ein angemeldeter Bürger trägt keinen Rang-Claim und wäre in den „Rang 1–2 ⇒ Antrag"-Zweig
    gefallen. `RequirePublicWantedRead` ist bewusst weiter als `RequireClassifiedRead` (Rang ≥ 4) — ein Rang-3
    publiziert direkt und muss seine eigenen Entwürfe öffnen können.
  - **Zurückziehen und Gefasst lassen das Modul-Gate aus:** Publizieren braucht ein lebendes Modul,
    *De*publizieren nie — sonst machte der Not-Aus das Zurückziehen unmöglich, genau verkehrt herum.
  - **Die VS-Meldung hängt vom Akteur ab:** wer keine eingestuften Akten lesen darf, bekommt wortgleich das
    „nicht gefunden" — sonst verriete der Publizieren-Knopf einem Junior die Einstufung.
  - **Der Lesepfad trägt einen Unterdrückungsgürtel** als korrelierte `Any`-Unterabfrage: eine Ausschreibung
    verschwindet, sobald ihre Akte eingestuft oder gelöscht ist. Bewusst keine Navigation — `f.Person` erbt den
    Soft-Delete-Filter und wäre für eine gelöschte Akte `null`, `f.Person == null || …` zeigte also genau die
    Zeilen, die es verbergen soll.
  - **Auto-Rückzug** über `RetractForRecordAsync` an drei Stellen: `PersonService.EditAsync` (der einzige
    `SecrecyLevel`-Schreibpfad), `PersonService.DeleteAsync` und `PersonMergeService` (das `IsClassified` direkt
    setzt und die Quelle soft-löscht). Ein Publikationsantrag folgt beim Zusammenführen **nicht** dem Ziel — sein
    Snapshot nennt weiter die Quelle — sondern wird mitgeschlossen.
- **`RequestService` bekommt drei Typ-Klauseln:** `DecideAsync` weist jeden Nicht-`Upgrade`-Antrag ab (die
  Genehmigung setzt bedingungslos eine Einstufung, mit `Classification.Unknown` wäre das eine stille
  Herabstufung), und `HasOpenRequestAsync` + der Dedup in `UpgradeRequestAsync` sind nicht mehr typblind.
  `GetOpenCountAsync` bleibt unangetastet — `DashboardService` liest es für *jeden* Agenten und beschriftet die
  Zahl „Hochstufung"; der Zähler kommt stattdessen in `NavMenu` dazu.
- `Models/Public/PublicWantedCard`/`PublicWantedDetail`/`PublicWantedBoard`/`PublicWantedPhoto` — enthalten
  baulich **kein** Feld für Codename/Klarname/Dienstgrad, keinen Aktenbezug, keine Zeilen-Id und keinen
  Zahlenwert. Intern getrennt: `PublicWantedEdit` (ohne HTML), `PublicWantedDraft`, `PublicWantedBanner`.
- Seiten: `WantedHub.razor` (`/gesucht`, Board, statisch, gecacht, Filter Art/Stufe als Query-Links) und
  `WantedProfile.razor` (`/gesucht/{CaseNumber}`). **Nicht** `WantedBoard` — `Services/WantedBoard.cs` ist eine
  global importierte statische Klasse. Filter sind als `string` gebunden und von Hand geparst; ein Enum-Binding
  wäre der `?vorschau=1`-Fehler an neuer Stelle.
- Intern: `PublicWantedEditor` (`Components/Common/Shared/`, von beiden Seiten benutzt),
  `PublicWantedPanel` in `PersonDetail` als eigener Abschnitt **außerhalb** von `einstufung` — den Slug listet
  `PartnerTabCatalog`, dort wäre alles einem freigegebenen Partner sichtbar. **Warnbanner** oben in
  `PersonDetail`, nur für `Veroeffentlicht`; ein `Beantragt` ist nicht draußen und verriete eine offene interne
  Entscheidung. `PublicWantedListPanel` auf `/fahndung?tab=oeffentlich` (in `AuthorizeView` mit
  `Policies.InternalAgent`, weil `/fahndung` nur `ActiveAgent` erbt und das ein Partner erfüllt).
  Antrags-Abschnitt in `Shares.razor`, **außerhalb** des Führungs-Blocks — entschieden wird auf Rang 3.
- `PublicVisibility`-Eintrag · `SearchCatalog.NotSearchable` (eigener Provider erst in Phase 16) ·
  `TrashService`+`TrashProjection` · `MergedPageSections.Trash`+`.Wanted` · `FeedbackPageTabs.Wanted` ·
  `AuditEntityDisplay` (Label **und** Route) · `TimelineService.AuditSourceAsync` + `TimelineDisplay.MapAudit` ·
  **`ChronikParentResolver`** (vierte Registry, die der Fan-out verlangt) · `DemoModeMiddleware` (`/gesucht`
  bleibt anonym, sonst trägt ein Besucher im Demo-Modus das Demo-Principal).
- `PublicModules`: `Fahndung` wird `Available`, bleibt `DefaultEnabled = false`.

**Tests** (+105, gesamt 5478 grün) — `PublicWantedServiceTests` (VS-Sperre rangunabhängig inkl. TRU/HRB und
soft-gelöschter Akte · Guard-Reihenfolge verrät die Einstufung nicht · Aufsicht/Bürger abgewiesen · Modul-Aus
und Not-Aus · Rang-Weiche 3 direkt / 2 erzeugt Antrag, Dedup, Begründungspflicht, Antrag ohne HTML ·
**Snapshot-Isolation** über Name, Vorwurf und Score · Entwurf zieht nur Name und Vorwurf · nachträgliche
Einstufung/Löschung räumt das Board über den Lesegürtel · `RetractForRecord` schließt auch den Antrag · jeder
nicht-öffentliche Status ist von „gibt es nicht" ununterscheidbar · Ablaufdatum ohne Worker · Erwähnung,
Platzhalter, leerer Vorwurf abgewiesen, reines Bild erlaubt, erneutes Bereinigen beim Publizieren ·
Zurückziehen behält Aktenzeichen und Text und geht auch bei Not-Aus · Löschen erst nach Zurückziehen ·
Wiederherstellen als Entwurf, nicht nach Einstufung · Aktenzeichen genau einmal · Foto wird kopiert und der
interne Dateiname nie gespeichert · Cache samt Invalidierung · Antrags-Genehmigung/-Ablehnung) ·
`PublicWantedModelTests` (positive Allowlist über die Nach-außen-Typen) · `PublicPageScanTests` (Datei-Scan
über die öffentlichen Seiten, Kommentare vorher entfernt) · `ChronikParentResolverTests` ·
Zeitstrahl-Fan-out · `RequestServiceTests` um die drei Typ-Klauseln erweitert.

**Fertig, wenn** eine Person publiziert ist, anonym auf `/gesucht` erscheint, ein Rang-2-Agent nur einen
Antrag erzeugt, eine VS-Akte gar nicht geht, und der Zeitstrahl der Akte die Publikation zeigt.

**In der Umsetzung gefunden und behoben:**
1. **`Request.VeroeffentlichungFahndungId` wurde `longtext`** — die Spalte stand in der Entity, aber nicht im
   `OnModelCreating`-Block von `Request`, und ohne `HasMaxLength` macht Pomelo daraus `longtext`. Beim Rollback
   der Migration aufgefallen, `HasMaxLength(64)` nachgezogen und die Migration neu erzeugt.
2. **`PublicModuleServiceTests.NavEntries_ExcludeAModuleWhosePagesDoNotExistYet` benutzte `Fahndung`** als
   Beispiel für ein noch nicht gebautes Modul — mit dieser Phase ist es gebaut. Steht jetzt auf
   `FahndungArchiv` (Phase 5).

**In der Nachprüfung gefunden und behoben** (fünf Prüf-Achsen, jeder Befund einzeln widerlegt oder bestätigt):
1. **Eine soft-gelöschte Ausschreibung ging anonym live.** `IgnoreQueryFilters()` gilt für die ganze
   Kompilierung, nicht für den Operanden — in der Gürtel-Unterabfrage benutzt, entfernte es `!IsDeleted` auch
   vom **äußeren** Set. Nachgemessen mit einer EF-Probe, nicht erschlossen. Der Gürtel ist jetzt eine eigene
   zweite Abfrage; `/gesucht` wurde gegen MariaDB mit einer live und einer soft-gelöschten Zeile nachgeprüft.
2. **Die internen Lesepfade hatten kein Aktengate.** `GetAllAsync`/`GetDraftAsync`/`GetOptionsAsync`/
   `GetForPersonAsync` gaben einem Rang-3-Agenten Name, Aktenzeichen und Vorwurf einer Verschlusssache, und
   `GetOptionsAsync` sogar deren **aktuelle** `PersonOrte` — live gelesen, nicht aus dem Snapshot. Alle vier
   filtern jetzt über `RecordVisibility`; eine nicht mehr auflösende Akte gilt als unsichtbar.
3. **Der Antragsweg war unerreichbar.** Das Panel gate den Editor auf „Rang ≥ 3 oder Aufsicht", also legte ein
   Rang-2-Agent einen Entwurf an und sah ihn nie wieder — `Beantragt`, der `Shares.razor`-Abschnitt und das
   Badge waren toter Code. Der Guard ist geteilt: `RequirePublicWantedRead` für die Querliste,
   `RequirePublicWantedRecordRead` für die Ausschreibung einer Akte.
4. **Genehmigen umging die Inhaltsprüfung.** Eine `Beantragt`-Zeile lässt sich zwischen Antrag und Entscheidung
   bearbeiten; Platzhalter, Erwähnung oder leerer Vorwurf gingen über die Genehmigung live. Die Prüfung sitzt
   jetzt im gemeinsamen Publish-Rumpf.
5. **Genehmigen hatte den falschen Guard.** `RequireHighestClassification` allein lässt die Nur-Lese-Aufsicht
   und das Demo-Principal durch, die Aktenzeichen und Fotokopie erzeugten, bevor der `ReadOnlyBarrierInterceptor`
   das Speichern verweigerte — auf einer Demo-Instanz anonym wiederholbar. `RequirePublicWantedWrite` läuft
   jetzt zuerst.
6. **Löschen verwaiste den Antrag.** Das Badge zählte eine Zeile, die der Posteingang nicht mehr fand und
   niemand entscheiden konnte. `DeleteAsync` schließt offene Anträge; Zähler und Liste kommen aus derselben
   Abfrage.
7. **Das Foto einer laufenden Ausschreibung ließ sich nicht wechseln.** Entfernen meldete Erfolg, das Bild
   blieb anonym abrufbar. `UpdateSnapshotAsync` kopiert bei einer laufenden Zeile jetzt neu und löscht die alte
   Kopie; ein fehlgeschlagenes Publizieren räumt die frische Kopie weg.
8. **Ablaufdatum in Ortszeit.** Der Datepicker liefert lokale Mitternacht, verglichen wurde gegen `UtcNow` —
   ein heute gewähltes Datum lief bis zu zwei Stunden zu früh ab. Gilt jetzt bis zum lokalen Tagesende.
9. **Testlücken:** `PublicPageScanTests` kannte `MentionText` nicht, nahm `Invite.razor` von allen Prüfungen
   statt nur vom Layout aus und verlor stillschweigend Hüllen-Dateien, die jemand verschiebt;
   `PublicWantedModelTests` listete `PublicModuleState` und `CareerRequirement` nicht; die Aktenzeichen-Prüfung
   konnte `??=` nicht von `=` unterscheiden. Alles nachgezogen, dazu je ein Regressionstest für 1–8.

Ausdrücklich **widerlegt** und nicht geändert: der Zeitstrahl unterscheidet Publizieren und Zurückziehen sehr
wohl (das `Status`-Paar des Interceptors rendert `AuditDisplay.Parse`), `HtmlCleanup` läuft auf jedem
Service-Schreibpfad vor der Prüfung, und `GetOpenCountAsync` war schon vorher typ-gebunden.

**Nachgewiesen** (laufender Server, MariaDB, anonym):
- Migration angewendet (`OeffentlicheFahndungen`, `Antraege.VeroeffentlichungFahndungId` als `varchar(64)`),
  Modul `Fahndung` bleibt nach dem Seed aus.
- Modul aus: `/gesucht` und `/gesucht/{az}` zeigen den Offline-Text, kein Tab in der Nav.
- Modul an, nichts publiziert: „Derzeit sind keine Ausschreibungen veröffentlicht", Tab erscheint.
- Eine Ausschreibung veröffentlicht: `/gesucht` listet die Karte, `/gesucht/NOOSE-FA-2026-0001` rendert
  Steckbrief mit Alias, Art, Gefahrenstufe „Kritisch", Warnhinweis, Vorwurf, Gegend und Fahrzeug.
- **VS nachträglich:** ein `IstVerschlusssacheTRU` auf der Akte leert das Board binnen eines Cache-Fensters,
  der Steckbrief antwortet mit „nicht gefunden" **plus** `noindex`-Meta, das Foto mit 404 — ohne dass die Zeile
  angefasst wurde (Lesegürtel allein).
- **Soft-gelöscht:** eine zweite, veröffentlichte Zeile mit `IstGeloescht = 1` erscheint **nicht** auf dem
  Board, ihr Steckbrief ist „nicht gefunden" mit `noindex`, ihr Foto 404 — während die lebende Zeile daneben
  normal ausgeliefert wird.
- Not-Aus: `/gesucht`, der Steckbrief und die Nav gehen dunkel, Banner auf der Startseite, `/buerger` bleibt
  erreichbar.
- `X-Robots-Tag`: `/gesucht`, `/gesucht/{az}` ohne Header; `/personen` und `/fahndung` mit `noindex, nofollow`.
- Erfundenes Aktenzeichen (`/gesucht/NOOSE-FA-9999-0001`): HTTP 200 mit `noindex`-Meta und „nicht gefunden".
- Müll-Queries (`?art=quatsch&stufe=99`, `?stufe=Critical`): HTTP 200, kein 500.
- Serverlog ohne Exception (nur die bekannte https-Port-Warnung).
- **Nicht geprüft:** die internen Panels, der Antrags-Posteingang und die Foto-Kopie im laufenden System —
  dafür braucht es einen Discord-Login. Ihre Logik hängt vollständig am getesteten Dienst.

---

## Phase 5 — Fahndung Ausbau ✅

**Ziel:** Warnhinweise, kein Vergessen, Archiv, Poster, Reichweite.

**Daten** (`Oeffentlich05_Warnhinweise`) — zwei Tabellen, **keine** `AddColumn`:
`GefasstAm`, `AblaufDatum` und `AufrufZaehler` kamen bereits mit Phase 4.
- `Warnhinweis` → `Warnhinweise`: `Bezeichnung` (60, unique), `Farbe` (Allowlist-**Name**), `Reihenfolge`,
  `IstAktiv`, `IAuditable`, **kein** `ISoftDelete` — Muster `Tag`: Hard-Delete hält den Unique-Index sauber.
- `FahndungWarnhinweis` → `FahndungWarnhinweise`: typisiert statt polymorph (`FahndungId` +
  `WarnhinweisId`, beide `Cascade`, unique Paar-Index), weder `IAuditable` noch `ISoftDelete`.

**Sechs bewusste Abweichungen von der Planzeile**
1. **Das Warnhinweis-Label wird live gelesen, nicht auf die Zuordnungszeile kopiert** — die einzige Stelle
   im öffentlichen Bereich, die von der Snapshot-Doktrin abweicht. Ein Warnhinweis ist ein redaktionelles
   Etikett der Behörde, kein Akteninhalt; eine Kopie hieße, ein korrigierter Tippfehler bliebe auf jedem
   laufenden Poster stehen, und `IstAktiv = false` müsste vierzig Ausschreibungen einzeln nacharbeiten.
   **Preis:** das Label geht ohne Publikationsschritt live, also prüft der Schreibpfad der Werteliste
   dieselben drei Regeln wie ein Vorwurf (Klartext, keine Erwähnung, kein `{{`-Platzhalter, ≤ 60 Zeichen).
2. **Das Archiv ist nur eine Liste** — Foto, Anzeigename, Art, „gefasst am". Kein Vorwurfstext, keine
   Gefahrenstufe, **keine Verlinkung**: `/gesucht/{az}` antwortet für eine gefasste Zeile weiter „nicht
   gefunden", ein Link dorthin wäre eine Sackgasse und suggerierte eine laufende Fahndung. Eigener
   Nach-außen-Record `PublicWantedArchiveCard`, damit das Board kein „gefasst am" und das Archiv keine
   Gefahrenstufe rendern *kann*. Gedeckelt auf die **100** jüngsten.
3. **Zwei neue `NotificationType`-Werte statt einem.** `PublicWantedPublished` ist routbar (eigener Webhook
   und eigene Rolle im Discord-Panel), `PublicWantedExpired` bewusst **nicht** —
   `NotificationService.NotifyManyAsync` pusht jede routbare Kategorie von selbst, ein routbarer
   Betriebs-Typ schriebe also jede abgelaufene Ausschreibung in den öffentlichen Kanal.
4. **Das Poster benutzt `PrintFrame` nicht.** Es druckt über `JS.InvokeVoidAsync` in `OnAfterRenderAsync`
   und `MudButton OnClick` — beides tot auf einer `[ExcludeFromInteractiveRouting]`-Seite, also *stumm*
   kaputt —, und es rendert „Gedruckt am … von {PrintedBy}", den VS-Stempel und „NOOSE-interne Akte".
   Stattdessen ein eigener statischer Rahmen mit rohem `onclick="window.print()"`, **ohne** Auto-Druck.
5. **Ein Cache-Schlüssel für Board und Archiv.** Beide stammen aus derselben Tabelle und werden von
   denselben Schreibpfaden ungültig; ein zweiter Key verdoppelte die zehn Invalidierungsstellen und
   schüfe eine neue Fehlerklasse. Vorher standen zehn `cache.Remove` verstreut — jetzt gibt es genau
   **einen** Speicherpfad (`SaveAndInvalidateAsync`), gehalten von einem Dateiscan.
6. **Kein QR-Code auf dem Poster** — es ist keine Generator-Bibliothek vendored, und eine anonyme Seite
   lädt keine Fremd-Ressource nach. Die gedruckte Klartext-URL leistet dasselbe.

**Code**
- `IWarnhinweisService` (Werteliste; Guards `RequireLeadership` **und** `RequireWriteAccess`, strenger als
  `TagService`, wo jeder Agent anlegen darf) · `WarnhinweisColours` als Farb-Allowlist — **nie
  `Enum.Parse`**: ein in der Spalte gelandeter Wert wäre auf einer `[AllowAnonymous]`-Seite ein HTTP 500,
  dieselbe Klasse wie `?vorschau=1` in Phase 3. Die Liste enthält keine der auf `#0E1116` unsichtbaren
  Farben. · `WarnhinweisSeeder` seedet **nur bei leerer Tabelle** (anders als `PublicModuleSeeder`, der pro
  Schlüssel nachlegt: ein Modul-Schlüssel steht im Code, ein Warnhinweis gehört dem Betreiber).
- `IPublicWantedService` wächst um `GetArchiveAsync`, `CountViewAsync`, `GetHintIdsAsync`, `SetHintsAsync`
  und `ExpireDueAsync`. Die Zuordnung liegt **hier** und nicht auf `IWarnhinweisService`: sie ändert, was
  draußen steht, muss also durch den einen Speicherpfad.
- **`RetractForRecordAsync` deckt jetzt auch `Gefasst` ab** (gemeinsame Statusmenge `PubliclyVisible`, auch
  von `RetractAsync` benutzt). Ohne das zeigte das Archiv Foto, Namen und Datum einer Person, die im
  August gefasst und im September als Informant eingestuft wird — der Hauptleckpfad des Archivs.
- **Der Foto-Endpoint bleibt unverändert**; `GetPublishedPhotoAsync` weitet intern auf `Gefasst`, prüft
  aber **je Menge** das eigene Modul (Archiv aus ⇒ Kopie 404, Board aus ⇒ laufende Kopie 404) und steigt
  weiter ausschließlich über den Snapshot ein, damit der Unterdrückungsgürtel entscheidet. Kein
  Query-Parameter: der wäre angreifer-kontrolliert und machte „gefasst?" getrennt von „veröffentlicht?"
  abfragbar — genau das Existenz-Orakel, das die eine `404` verhindert.
- `PublicWantedExpiryWorker` (45 s Startverzögerung, 15 min Takt) ruft **nur** `ExpireDueAsync` und hält
  keinen `DbContext`. Er ist **keine Sicherheitskontrolle** — der Lesepfad filtert `ExpiresAt > now`
  ohnehin; er macht den internen Zustand ehrlich. Der Statuswechsel ist der Idempotenz-Token, deshalb
  braucht die Migration keine `NotifiedAt`-Spalte. **Eine** Sammelmeldung je Lauf, `Take(200)`.
- Seiten `WantedArchiveHub` (`/gefasst`) und `WantedPoster` (`/gesucht/{az}/druck`, `PrintLayout`, zieht
  **beide** Modul-Gates selbst, weil `PrintLayout` weder `PublicNav` noch den Not-Aus-Banner trägt, und
  trägt **immer** `noindex`). Intern: `WarnhinweisPanel`/`-Dialog` unter `/einstellungen?tab=warnhinweise`,
  `WarnhinweisPickerDialog` (**ohne** Inline-Anlegen — die Liste ist redaktionell) und `WarnhinweisChips`.
- `PublicModules`: `FahndungArchiv` und `FahndungDruck` werden `Available`, bleiben `DefaultEnabled = false`.

**Tests** (+101, gesamt 5582 grün) — `WarnhinweisServiceTests` (Rechte, Dubletten, die drei Inhaltsregeln,
Farb-Allowlist, Seeder weckt einen gelöschten Wert nicht wieder) · `PublicWantedServiceTests` um Archiv,
Gefasst-Hook, Foto-Gate je Modul, Aufrufzähler, Chips und Discord erweitert · `PublicWantedCacheDisciplineTests`
(ein Speicherpfad, ein `cache.Remove`, ein Schlüssel) · `PublicSurfaceGuardTests` (vier Wächter, die es
vorher nicht gab) · `PublicWantedModelTests` von einer handgepflegten Liste auf **Reflection über den
ganzen Namensraum** umgestellt: jedes Modell steht in `Outward` oder in `Inward` — mit Begründung.

**Fertig, wenn** ein Eintrag mit Ablaufdatum von selbst offline geht, das Archiv gefüllt ist und ein Poster
druckbar ist.

**In der Umsetzung gefunden und behoben:**
1. **Kein einziger Typ des öffentlichen Bereichs stand im `WatchlistRecordRollup`** — seit Phase 1. Dessen
   Default-Arm warnt nur, also schrieb jede Publikation, jede Seitenänderung, jede Modulumschaltung und
   jede Profiländerung stillschweigend eine Warnzeile ins Log. Im Serverlog aufgefallen, nicht im Test.
   `OeffentlicheFahndung` rollt jetzt auf ihre `Person` (Publizieren ist die folgenreichste Änderung, die
   einer Akte passieren kann); `OeffentlicheSeite`, `OeffentlichesModul`, `BuergerProfil` und `Warnhinweis`
   sind „not watchable". Ein Wächter ruft den Rollup jetzt wirklich auf und zählt Warnungen.
2. **`AuditEntityDisplay`-Wächter zu breit gedacht.** Der Plan versprach ihn über *alle* auditierten
   Entitäten; tatsächlich laufen rund siebzig interne Kindtabellen seit jeher ohne Label. Der Wächter ist
   deshalb auf `Data/Entities/Public` verengt — mit dem Grund im Kommentar. Die interne Lücke bleibt offen
   und ist eigene Arbeit, kein Nebeneffekt einer Phase des öffentlichen Bereichs.
3. **Das Label-Kriterium `Label(name) != name` trägt nicht:** „Warnhinweis" liest sich in beiden Sprachen
   gleich, ein Wertvergleich hielte den korrekten Arm für einen Treffer. Der Wächter prüft jetzt die Quelle.
4. **Löschen eines Warnhinweises räumt seine Zuordnungen selbst weg**, statt sich auf die FK-Cascade zu
   verlassen — SQLite bildet sie im Test nicht nach, und eine nach außen wirkende Zuordnung darf nicht an
   der Referenz-Semantik der Datenbank hängen. Die Cascade bleibt als Netz.
5. `PublicModuleServiceTests` benutzte `FahndungArchiv` als Beispiel für ein ungebautes Modul — mit dieser
   Phase ist es gebaut. Steht jetzt auf `Organisationen` (Phase 12).

**In der Nachprüfung gefunden und behoben** (Durchgang über den ganzen Diff nach Logikfehlern):
1. **Ein deaktivierter Warnhinweis wurde beim nächsten Speichern still aus der Zuordnung gelöscht.**
   `SetHintsAsync` verengte die Zielmenge auf **aktive** Zeilen, der Picker bot einen inaktiven nicht an und
   der Editor zeigte ihn nicht — ein „Übernehmen" ohne einen einzigen Klick zerstörte die Zuordnung also
   unsichtbar, und ein späteres Reaktivieren brachte sie nicht zurück. Die Menge ist jetzt
   *aktiv **oder** bereits zugeordnet*: **Hinzufügen** bleibt auf aktive beschränkt (das ist der Riegel gegen
   einen manipulierten Dialog-Post), **Bestehendes** überlebt. Der Editor zeigt einen zugeordneten inaktiven
   Hinweis jetzt grau mit „(inaktiv, nicht öffentlich)", statt ihn zu verschweigen.
2. **`SetHintsAsync` prüfte die Aktensichtbarkeit nur bei einer laufenden Ausschreibung** — `GetHintIdsAsync`
   dagegen immer. Wer die Id eines Entwurfs zu einer Verschlusssache kannte, konnte ihn also bechippen und
   eine `ManualAudit`-Zeile gegen eine Akte schreiben, die er nicht öffnen darf. Schreib- und Lesepfad halten
   jetzt dasselbe Gate.
3. **Ein Fehler beim Lesen des *Poster*-Schalters blendete den ganzen Steckbrief aus.** In `WantedProfile`
   standen `GetByCaseNumberAsync` und die Modulabfrage im selben `try`, dessen `catch` `_entry = null` setzt.
   Die Modulabfrage hat jetzt ihr eigenes `try` — ein Fehler dort kostet höchstens den Druckknopf.
4. **Die neuen Abfragen waren nur gegen SQLite bewiesen.** Alle Integrationstests laufen auf SQLite, das ein
   anderer Übersetzer ist: eine Form, die dort durchgeht, kann auf Pomelo mit „could not be translated"
   werfen — zur Laufzeit, auf einer Seite, die ein anonymer Besucher geöffnet hat. Besonders heikel war
   `PubliclyVisible.Contains(f.Status)`, weil daran der Rückzugs-Hook hängt. `MySqlTranslationTests`
   kompiliert die acht neuen Formen jetzt über `ToQueryString()` gegen den **Produktions-Provider** — ohne
   Server, weil dafür keine Verbindung nötig ist.
5. **Der `WatchlistRecordRollup`-Eintrag prüft die Sichtbarkeit nicht selbst** — nachgesehen, weil
   `OeffentlicheFahndung` jetzt auf ihre `Person` rollt und ein Rückzug wegen Einstufung sonst genau die
   Follower benachrichtigt hätte, die die Akte nicht mehr sehen dürfen. `WatchlistFanout` filtert je
   Empfänger über `Visibility.IsRecordVisibleAsync`; kein Leck, ausdrücklich geprüft und nicht geändert.

**Nachgewiesen** (laufender Server, MariaDB, anonym, zwei Testzeilen — eine veröffentlicht, eine gefasst):
- Migration angewendet, beide Tabellen mit `varchar(64)`-FKs (nicht `longtext` wie in Phase 4), die vier
  Warnhinweise geseedet, alle drei Module bleiben nach dem Seed **aus**.
- Board zeigt nur die laufende Zeile samt beider Chips, das Archiv nur die gefasste mit „Gefasst am",
  der Steckbrief der gefassten antwortet „nicht gefunden".
- Poster rendert GESUCHT, Name, Chip, Klartext-URL und `window.print()` — und **kein** „Gedruckt am".
- **Modul-Gates einzeln:** Archiv aus ⇒ Archiv leer, Board bleibt · Board aus ⇒ Archiv bleibt, Board leer,
  Poster zu · Druck aus ⇒ Poster zu und der Knopf am Steckbrief verschwindet.
- **Unterdrückungsgürtel auf allen drei neuen Pfaden:** TRU auf die *gefasste* Akte leert das Archiv, ohne
  dass die Ausschreibungszeile angefasst wurde; eine soft-gelöschte Akte nimmt Board und Poster mit;
  ein Ablaufdatum in der Vergangenheit leert das Board sofort und das Zurücksetzen bringt es zurück.
- **Not-Aus** schließt Board, Archiv **und** Poster und lässt sich wieder einschalten.
- Aufrufzähler: 1 nach einem Steckbrief-Aufruf, **0** nach dem Poster-Aufruf, `GeaendertAm` bleibt `NULL`.
- `X-Robots-Tag` fehlt auf `/gefasst` und `/gesucht/{az}/druck`, steht auf `/personen`.
- Müll-Queries (`?art=quatsch&stufe=99`, `?qr=1`, `?vorschau=ja&x=%00`, erfundenes Aktenzeichen):
  **200, kein 500**; `/gesucht/../../etc/passwd/druck` ist 404; der Foto-Endpoint antwortet auf jeden
  Fehlschlag mit derselben 404.
- Serverlog ohne Exception (nur die bekannte https-Port-Warnung).
- **Nicht geprüft:** die internen Panels, der Warnhinweis-Picker und der Discord-Push im laufenden System —
  dafür braucht es einen Discord-Login bzw. einen konfigurierten Webhook. Ihre Logik hängt vollständig an
  den getesteten Diensten.

---

## Phase 6 — Kopfgeld

**Ziel:** Behördliches Geld aus der Kasse **und** privates Geld von Agenten auf einen Kopf; öffentlich nur
die Gesamtsumme.

**Daten** (`Oeffentlich06_Kopfgeld`)
- `FahndungKopfgeldAnteil` → `FahndungKopfgeldAnteile`: `FahndungId`, `Herkunft`
  (`NooseKasse`/`AgentPrivat`), `Betrag`, `Konto` (nur behördlich), `StifterAgentId`,
  `KassenBuchungId` (nullable), `Status` (`Zugesagt`/`Gesichert`/`Ausgezahlt`/`Zurueckgezogen`),
  `Zeitpunkt`, `IAuditable`, `ISoftDelete`.

**Code**
- `Services/Public/IBountyService`:
  - Anteil hinzufügen — behördlich nur mit Publish-Recht (Rang ≥ 3 bzw. Antrag), **privat jeder aktive Agent**.
  - Privaten Anteil einzahlen: `IKassenService.BookAsync(db, …)` als `Einzahlung` mit Zweck
    „privates Kopfgeld <Aktenzeichen>" ⇒ Anteil wird `Gesichert`, `KassenBuchungId` gesetzt.
  - Ändern/Zurückziehen mit Historie (jeder Vorgang ist eine neue Zeile bzw. ein Statuswechsel, nichts wird
    überschrieben) + `ManualAudit.Row` gegen die Personenakte.
  - `CoverageWarningAsync`: Σ offener **behördlicher** Anteile vs. `GetBalanceAsync(konto)` — Warnung, keine Sperre.
- Öffentlich: **nur** `Gesamtsumme` in der Projektion; `KopfgeldIstObergrenze` rendert „bis X".
  Kein Feld für Herkunft, Stifter oder Anzahl.
- Intern: Kopfgeld-Panel am Steckbrief-Editor (Anteile, Historie, Deckungswarnung), Kopfgeld-Übersicht +
  Deckungswarnung auf `/kasse`.
- Discord-Push bei Erhöhung/Senkung.
- `PublicModules`: `Kopfgeld`.

**Tests** Rechte-Matrix (Junior darf privat, nicht behördlich) · Einzahlung erzeugt genau eine
Kassenbuchung und setzt `Gesichert` · Deckungswarnungs-Arithmetik · **öffentliche Projektion enthält keine
Aufschlüsselung** (Datei-Scan + Unit) · Historie ist append-only.

**Fertig, wenn** 500.000 behördlich + 1.000.000 privat gesetzt sind, öffentlich „1.500.000" steht, der
private Anteil optional eingezahlt werden kann und `/kasse` die Deckung korrekt warnt.

---

## Phase 7 — Hinweise Kern

**Ziel:** Bürger können Hinweise abgeben, Agenten sie bearbeiten, Bürger den Stand verfolgen.

**Daten** (`Oeffentlich07_Hinweise`)
- `Hinweis` → `Hinweise`: `Aktenzeichen` (`NOOSE-H`), `BuergerProfilId`, `AnonymGewuenscht`,
  `FahndungId` (nullable), `Text`, `AnhangDateiname`/`Originalname`/`Typ`, `Status`
  (`Neu`/`InPruefung`/`Rueckfrage`/`Bestaetigt`/`Verworfen`/`FuehrteZurErgreifung`), `BearbeiterId`,
  `DublettenGruppeId` (nullable, Feld jetzt, Logik in Phase 8), `Prioritaet` (int, Phase 8),
  `AnonymitaetAufgeloestAm`/`VonId`, `ZuletztGelesenBuergerAm`, `IAuditable`, `ISoftDelete`.
- `HinweisNachricht` → `HinweisNachrichten`: `Zielgruppe` (`Buerger`/`Intern`), `Text`, `VonBuerger`,
  `AutorAgentId` (**nur intern**) — 1:1 Muster `BewerbungMessage`.

**Code**
- `Services/Public/ITipService`: Einreichen (`RequireEnabled`, `RequireNotBlocked`, Mindestlänge,
  Rate-Limit je Konto, Bild-Upload über `FileUploadOptions` + `FilePathHelper.SafePath` nach
  `App_Data/uploads`), Status setzen, Rückfrage an den Bürger, verwerfen.
- **„Bürger" heißt hier Konto mit `BuergerProfil`, nicht `Status = Civilian`.** Der Einreicher wird immer über
  `IBuergerService.RequireSubmittingCitizenAsync` aufgelöst (vollständig, nicht gesperrt) — ein Agent, der über
  seine Zivil-Identität meldet, ist ein normaler Hinweisgeber, und die Bearbeiter-Projektion kennt ohnehin nur
  das Profil. Einzige Stelle, an der die Zuordnung Konto ↔ Zivil-Identität sichtbar wird, bleibt der
  Führungs-Roster (`PublicCitizensPanel`, Discord-Name) — gewollt für die Missbrauchskontrolle, sonst nirgends.
- Rate-Limit: eigene Policy in `Program.cs` **plus** Zählprüfung im Service (der SignalR-Pfad umgeht die
  Middleware — genau die Lücke, vor der CLAUDE.md bei Schreibpfaden warnt).
- Anonymität: `AnonymGewuenscht` ⇒ Bearbeiter-Projektion enthält kein Bürgerfeld. Auflösen ist eine eigene
  Methode mit `Permission.RequireLeadership` + eigener Audit-Zeile; das Formular sagt dem Bürger vorher,
  dass eine Belohnung die Auflösung braucht.
- Seiten: Hinweis-Dialog am Steckbrief und freies Formular `/hinweis` (interaktiv);
  `/buerger/hinweise` (eigene Hinweise, Status, Rückfragen, Ungelesen-Badge);
  intern `/hinweise` (Eingang, Triage, Konversation, interne Notizen).
- Absender außen: konstant „NOOSE" — kein Agentenbezug in der Bürger-Projektion.
- `PublicVisibility` · `TrashService` · Zeitstrahl-Fan-out (Hinweis an einer publizierten Person).
- `PublicModules`: `Hinweise`.

**Tests** Rate-Limit greift auch ohne HTTP-Middleware · gesperrtes Konto wird abgewiesen · Anhang landet
außerhalb `wwwroot` und ist nur autorisiert abrufbar · anonymer Hinweis zeigt dem Bearbeiter kein Konto ·
Auflösung nur Führung + Audit · Bürger-Projektion enthält keinen Autor.

**Fertig, wenn** ein Bürger einen Hinweis mit Bild abgibt, ihn in `/buerger/hinweise` mit Status sieht, ein
Agent eine Rückfrage stellt und der Bürger sie beantwortet.

---

## Phase 8 — Hinweise Ausbau

**Ziel:** Der Eingang bleibt auch beim ersten großen Fall bedienbar, und ein Hinweis wird zur Akte.

**Migration:** keine (Felder stehen schon aus Phase 7).

**Code**
- Dublettengruppen über `TextSimilarity` (Levenshtein) beim Einreichen; Gruppe wird im Eingang
  zusammengefasst dargestellt.
- Priorisierung: Kopfgeldhöhe × öffentliche Gefahrenstufe × Vertrauensstufe → `Prioritaet`,
  Standard-Sortierung des Eingangs.
- Vertrauensstufe: `BuergerProfil.BestaetigteHinweise` wird bei `Bestaetigt`/`FuehrteZurErgreifung`
  hochgezählt und steuert das Rate-Limit-Kontingent.
- Übernahme: Hinweis → neue/bestehende Personenakte, → `Case` (Vorgang), → `Observation`. Jeweils
  `Link`-Eintrag zurück auf den Hinweis, damit die Herkunft an der Akte sichtbar bleibt.
- Bürgerkonto ↔ Personenakte: `BuergerProfil.LinkedPersonId` setzen; an der Personenakte erscheint die
  Hinweisgeber-Historie.
- Gegenaufklärung: `CounterIntelRule`-Variante „Hinweisgeber meldet über die eigene verknüpfte Fraktion".

**Tests** Dublettenerkennung gruppiert Ähnliches und trennt Unähnliches · Priorität sortiert wie
spezifiziert · Vertrauensstufe hebt das Kontingent · Übernahme erzeugt Akte **und** Rückverknüpfung ·
Gegenaufklärungs-Regel feuert nur im beschriebenen Fall.

**Fertig, wenn** zwei gleiche Hinweise als Gruppe erscheinen, ein Hinweis in eine Personenakte übernommen
wird und die Akte den Hinweisgeber-Bezug zeigt.

---

## Phase 9 — Belohnung & Auszahlung

**Ziel:** Geld fließt nachvollziehbar, auch bei mehreren Hinweisgebern.

**Daten** (`Oeffentlich09_Belohnung`)
- `HinweisBelohnung` → `HinweisBelohnungen`: `HinweisId`, `AnteilId`, `Betrag`, `KassenBuchungId` (nullable),
  `SelbstAusgezahltAm` (privater Anteil ohne Kassenbuchung), `BelegNummer`, `IAuditable`, `ISoftDelete`.

**Code**
- `IBountyService.PayoutAsync`: **ein** `db`-Scope, **eine** Transaktion — je behördlichem Teil
  `IKassenService.BookAsync(db, …)` als `Auszahlung`, Hinweis-Status → `FuehrteZurErgreifung`,
  Anteile → `Ausgezahlt`, Fahndung → `Gefasst`. Privater Anteil ohne Kassenbuchung als
  „selbst ausgezahlt" mit Audit-Zeile.
- Invariante im Service: Σ Belohnungen ≤ Σ Anteile; Split auf mehrere Hinweise mit Betrag je Hinweis.
- Anonymer Hinweis: Auszahlung erst nach Anonymitäts-Auflösung (Phase 7) — der Dialog verlangt sie explizit.
- Beleg: `/buerger/belohnung/{BelegNummer}/druck` im `PrintLayout`, Bearbeiter geschwärzt.
- `PublicModules`: `Belohnung`.

**Tests** Rollback lässt keinen halben Zustand (kein Geld ohne Statuswechsel, kein Statuswechsel ohne Geld) ·
Σ-Invariante wird verletzt abgewiesen · Split auf drei Hinweise summiert korrekt · Beleg enthält keinen
Agentennamen · anonymer Hinweis ohne Auflösung ist nicht auszahlbar.

**Fertig, wenn** eine Ergreifung mit zwei Hinweisgebern ausgezahlt ist, `/kasse` genau die erwarteten
Buchungen zeigt und beide Bürger ihren Beleg drucken können.

---

## Phase 10 — Ticket-Chat

**Ziel:** Das öffentliche Discord-Ticketsystem ist ersetzt.

**Daten** (`Oeffentlich10_Tickets`)
- `Ticket` → `Tickets`: `Aktenzeichen` (`NOOSE-T`), `Art` (`Fuehrungsebene = 0`, weitere Werte
  **vorbereitet, inaktiv**), `BuergerProfilId`, `Betreff`, `Status`
  (`Offen`/`InBearbeitung`/`WartetAufBuerger`/`Geschlossen`), `BearbeiterId`, `LetzteAktivitaetAm`,
  `ZuletztGelesenBuergerAm`, `ZuletztGelesenAgentAm`, `IAuditable`, `ISoftDelete`.
- `TicketNachricht` → `TicketNachrichten`: `Zielgruppe` (`Buerger`/`Intern`), `Text`, `VonBuerger`,
  `AutorAgentId` (**nur intern**).

**Code**
- `ITicketService`: Öffnen (jedes Konto mit vollständigem, ungesperrtem `BuergerProfil` — siehe Phase 7 —
  `RequireEnabled`, Rate-Limit), Antworten (`Permission.RequireLeadership`), interne Notizen,
  Schließen/Wiederöffnen, Lesestände.
- Außen-Absender ist die Konstante „NOOSE – Führungsebene". `AutorAgentId` existiert in **keiner**
  Bürger-Projektion — Datei-Scan-Test hält es fest.
- `Infrastructure/PublicChatBroadcaster.cs` — Singleton, Muster `TaskforceChatBroadcaster`, beide Seiten.
- Seiten: `/buerger/tickets`, `/buerger/tickets/{Id}` (interaktiv, Ungelesen-Badge) ·
  intern `/tickets` (`Policies.LeadershipPage`, Nur-Lese-Aufsicht liest, schreibt nicht).
- `PublicVisibility` · `TrashService` · `PublicModules`: `Tickets`.

**Tests** Junior-Agent sieht `/tickets` nicht · Nur-Lese-Aufsicht liest, `ReadOnlyBarrierInterceptor`
verhindert die Antwort · Bürger-Projektion ohne Autor · Broadcaster erreicht beide Seiten · Rate-Limit.

**Fertig, wenn** ein Bürger ein Ticket öffnet, die Führung live antwortet, der Absender außen konstant ist
und ein Junior-Agent nichts davon sieht.

---

## Phase 11 — Öffentliche Vorlagen (4. Token-System)

**Ziel:** Vorlagen für jede Art öffentlicher Kommunikation, ohne die drei bestehenden Token-Systeme zu berühren.

**Daten** (`Oeffentlich11_Vorlagen`)
- `OeffentlicheVorlage` → `OeffentlicheVorlagen`: `Art` (`TicketAntwort`, `Eingangsbestaetigung`,
  `HinweisRueckfrage`, `HinweisAblehnung`, `Belohnungszusage`, `Pressemitteilung`), `Titel`,
  `Html` (`longtext`), `IstAktiv`, `IAuditable`, `ISoftDelete`.

**Code**
- `Services/Public/PublicTemplateRenderer.cs` — eigene `GeneratedRegex`-Klasse, eigener Token-Satz:
  `BUERGER`, `AKTENZEICHEN`, `BETRAG`, `DATUM`, `UHRZEIT`; das Absender-Token wird zu `███████`.
  **Keine** Wiederverwendung von `BewerbungTemplateRenderer` (dessen Prüfungen hängen an Kontext
  `RecruitingTemplate` und würden Bürgertext falsch validieren) und **kein** Mischen mit
  `PlaceholderService` oder `MentionParser`.
- Anwendung: Vorlagen-Auswahl im Ticket-Antwortfeld und in der Hinweis-Rückfrage;
  Auto-Eingangsbestätigung beim Öffnen eines Tickets und beim Eingang eines Hinweises.
- `/einstellungen` → `PublicTemplatesPanel` (je Art, RichTextEditor, Vorschau mit Beispielwerten).

**Tests** Token-Ersetzung und Schwärzung · Tokens werden beim **Speichern der Vorlage** roh belassen und
nur beim Anwenden expandiert · ein Datei-Scan-Test verhindert, dass ein Consumer die Bewerbungs- oder
Dokument-Tokens im öffentlichen Pfad benutzt.

**Fertig, wenn** eine Ticket-Antwort per Vorlage entsteht, der Absender geschwärzt ist und die
Eingangsbestätigung automatisch kommt.

---

## Phase 12 — Organisationen & Gefahrenlisten

**Ziel:** „Gefährlichste Fraktionen" und „gefährlichste Personen" — nur aus Publiziertem.

**Daten** (`Oeffentlich12_Fraktionsprofile`)
- `OeffentlichesFraktionsprofil` → `OeffentlicheFraktionsprofile`: `FraktionId`, Snapshot `AnzeigeName`,
  `KurzbeschreibungHtml` (`longtext`), `Status` (`Beobachtet`/`Verboten`), `OeffentlicheGefahrenstufe`,
  `VeroeffentlichtAm`/`VonId`, `IAuditable`, `ISoftDelete`.

**Code**
- `IPublicFactionProfileService` — dieselben Guards wie `IPublicWantedService` (VS-Sperre, Rang-Weiche,
  Audit gegen die Fraktionsakte); Warnbanner in `FactionDetail`.
- Seiten `/organisationen`, `/gefahr/fraktionen`, `/gefahr/personen` — Ranking nach öffentlicher
  Gefahrenstufe, Score live als Zahl, ausschließlich publizierte Einträge.
- `PublicModules`: `Organisationen`, `Gefahrenlisten`.

**Tests** Unpubliziertes erscheint nirgends (auch nicht mit hohem Score) · VS-Fraktion nicht publizierbar ·
Snapshot-Isolation · Score-Zahl nur für publizierte Einträge.

**Fertig, wenn** beide Gefahrenlisten gefüllt sind und eine hoch bewertete, aber unpublizierte Fraktion
nachweislich fehlt.

---

## Phase 13 — Gesuchte Fahrzeuge/Waffen & Einspruch

**Ziel:** Zwei kleine, unabhängige Ergänzungen.

**Daten** (`Oeffentlich13_FahrzeugeEinspruch`)
- `FahndungEinspruch` → `FahndungEinsprueche`: `FahndungId`, `BuergerProfilId`, `Text`, `Status`,
  `Entscheidungsnotiz`, `EntschiedenVonId`/`Am`, `LinkedCaseId`, `IAuditable`, `ISoftDelete`.
- Fahrzeuge/Waffen brauchen keine neue Tabelle — `Art = Fahrzeug|Waffe` aus Phase 4 plus die vorhandenen
  Snapshot-Felder (`FahrzeugText`, `FotoDateiname`).

**Code**
- `IObjectionService`: einreichen (Bürger, Rate-Limit), entscheiden (`Permission.RequireLeadership`),
  optional Vorgang anlegen; Statusanzeige in `/buerger/einspruch`.
- Board-Variante `/gesucht?art=fahrzeug` mit eigener Kachel-Optik; Quelle sind `PersonVehicle`/
  `PersonWeapon` beim Snapshot-Ziehen.
- `PublicModules`: `Einspruch`, `FahndungFahrzeuge`.

**Tests** Einspruch nur zu veröffentlichten Einträgen · Entscheidung erzeugt Audit-Zeile an der Akte ·
Fahrzeug-Board zeigt keine Personendaten außer dem publizierten Snapshot.

**Fertig, wenn** ein Bürger widersprechen kann, die Führung entscheidet, und ein Kennzeichen-Steckbrief
ohne Personenbezug online ist.

---

## Phase 14 — Presse, Lageberichte, Recht, Warnungen

**Ziel:** Die Behörde spricht selbst.

**Daten** (`Oeffentlich14_Redaktion`)
- `Pressemitteilung` → `Pressemitteilungen`: `Titel`, `Teaser`, `Html` (`longtext`), `Status`
  (`Entwurf`/`Veroeffentlicht`), `VeroeffentlichtAm`/`VonId`, `DiscordGepushtAm`, `IAuditable`, `ISoftDelete`.
- `OeffentlicherLagebericht` → `OeffentlicheLageberichte`: `SituationReportId`, `FreigegebenHtml`
  (`longtext`), `Status`, `VeroeffentlichtAm`/`VonId`.
- `OeffentlicheWarnung` → `OeffentlicheWarnungen`: `Titel`, `Html`, `GueltigBis`, `Status`.
- `Law`: `IstOeffentlich` (bool).

**Code**
- `IPressReleaseService`, `IPublicSituationReportService` (Abschnitte des Monatsberichts einzeln freigeben),
  `IPublicWarningService`; Gesetzesauszüge über das bestehende `LawService` mit dem neuen Flag.
- Automatischer Pressemitteilungs-**Entwurf** bei `GefasstAsync` (Phase 4), vorbefüllt aus einer Vorlage
  (Phase 11) — nie automatisch veröffentlicht.
- Seiten `/presse`, `/presse/{Id}`, `/lageberichte`, `/warnungen`, `/recht`.
- `/einstellungen` → `PressPanel`, `PublicSituationReportPanel`, `PublicWarningsPanel`.
- Discord-Push beim Veröffentlichen einer Pressemitteilung.
- `PublicModules`: `Presse`, `Lageberichte`, `Warnungen`, `Recht`.

**Tests** Entwurf ist anonym nicht erreichbar · Freigabe eines Berichtsabschnitts veröffentlicht **nur**
diesen Abschnitt · nicht-öffentliche Gesetze erscheinen nicht · `GefasstAsync` erzeugt Entwurf, nicht
Veröffentlichung.

**Fertig, wenn** eine Pressemitteilung online ist, ein Lagebericht-Auszug freigegeben ist und ein
„gefasst"-Vorgang automatisch einen Entwurf hinterlässt.

---

## Phase 15 — Zahlen & Lage-Seite

**Ziel:** Statistik nach außen, und die Startseite wird zur echten Startseite.

**Migration:** keine (nur `SystemSettingKeys`: `PublicHazardLevel`, `PublicHazardNote`, `PublicHazardSince`).

**Code**
- `IPublicStatisticsService` — gecacht, **ausschließlich** aus öffentlichen Tabellen und publizierten
  Einträgen: offene/gefasste Fahndungen, Hinweise (eingegangen/bestätigt/führten zur Ergreifung),
  ausgezahlte Belohnungssumme, Gefahrenlage-Trend (aggregiert aus `ThreatScoreHistory`, ohne Aktenbezug).
- Gefahrenlage-Ampel: `PublicHazardPanel` in `/einstellungen` (`Policies.LeadershipPage`), groß auf `/lage`
  und im Landing-Hero.
- `Landing.razor` umbauen: Gefahrenlage-Ampel, Top-3 `/gesucht`, neueste Pressemitteilung, Hinweis-CTA,
  Karriere-Kachel bleibt. Der „Kein Zugriff"-Block bleibt bewusst so.
- `StatTile` wiederverwenden, nicht neu bauen.
- `PublicModules`: `Statistik`, `Gefahrenlage`.

**Tests** Jede Kennzahl stammt nachweislich nur aus öffentlichen Quellen (Test mit unpublizierten Daten in
der DB, die in keiner Zahl auftauchen dürfen) · Trend ist aggregiert und nicht auf eine Akte rückführbar ·
Cache-Verhalten.

**Fertig, wenn** `/lage` und die neue Startseite stehen und ein Test beweist, dass unpublizierte Akten in
keiner öffentlichen Zahl stecken.

---

## Phase 16 — Suche, NOOSEI, interne KPIs

**Ziel:** Der neue Bereich ist für Agenten und den Assistenten auffindbar; Führung sieht, ob er sich lohnt.

**Migration:** keine.

**Code**
- `SearchCatalog`: je eine Zeile für `OeffentlicheFahndung`, `Hinweis`, `Ticket`, `Pressemitteilung`,
  `OeffentlichesFraktionsprofil`, `FahndungEinspruch`, `OeffentlicheSeite` + je ein `ISearchProvider`
  in `Services/Search/Providers/`, registriert in `AddSearchProviders()`. Provider **benennen** ein
  Sichtbarkeits-Prädikat, schreiben keins.
- `NooseiRecordTypes`: `Uses`-Zeilen mit `Read`/`List`/`Chronicle` je Typ; Arme in
  `Visibility.IsRecordVisibleAsync` (Pflicht für alles mit `Read` — der Schwanz beantwortet Unbekanntes mit
  „sichtbar", das wäre hier ein Leck); `SearchParentResolver` um die neuen polymorphen Kinder erweitern.
- `SearchIndexBackfillWorker.Version` hochzählen.
- `IPublicSearchService` — eigener, schmaler Pfad nur über publizierte Inhalte, Seite `/suche-oeffentlich`.
- `/einstellungen` → `PublicKpiPanel`: Hinweis→Ergreifungs-Quote, Kosten pro Ergreifung,
  Reaktionszeit im Ticket, Aufrufe je Ausschreibung.
- `PublicModules`: `OeffentlicheSuche`.

**Tests** `SearchCoverageTests`/`SearchCatalogTests`/`NooseiRecordTypesTests` grün ·
`EveryReadableType_HasAnArmInTheVisibilityGate` grün · NOOSEI-Werkzeug liefert für eine unsichtbare Akte
`NotFound()`, nicht „kein Zugriff" · öffentliche Suche findet nichts Unpubliziertes.

**Fertig, wenn** ein Agent Hinweise und Tickets in `/suche` findet, NOOSEI „welche Hinweise kamen zu X"
beantwortet, und die öffentliche Suche nachweislich nur Publiziertes zeigt.

---

## Kritische Dateien

**Ändern:** `Data/AppDbContext.cs` · `Models/Enums/AgentStatus.cs` ·
`Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` ·
`Authorization/Policies.cs` + `AuthorizationRegistration.cs` + `AgentPrincipalExtensions.cs` ·
`Services/Permission.cs` · `Components/Layout/PublicNav.razor` · `Components/Pages/Public/Landing.razor` ·
`Components/Pages/Admin/Settings.razor` · `Components/Pages/People/PersonDetail.razor` ·
`Components/Pages/Factions/FactionDetail.razor` · `Services/TrashService.cs` + `TrashProjection.cs` ·
`Services/TimelineService.cs` + `Models/TimelineDisplay.cs` · `Services/Search/SearchCatalog.cs` ·
`Services/Llm/Tools/INooseiTool.cs` · `Services/Visibility.cs` ·
`Infrastructure/…/SearchIndexBackfillWorker.cs` · `Components/Pages/Legal/*` ·
`Program.cs` (DI, Worker, Rate-Limit-Policies, Upload-Endpoints, noindex-Middleware).

**Neu:** `Data/Entities/Public/*` · `Models/Public/*` · `Services/Public/*` ·
`Components/Pages/Public/*` · `Components/Pages/Portal/Buerger*` · `Components/Pages/Tips/*` ·
`Components/Pages/Tickets/*` · `Components/Pages/Admin/Shared/Public*Panel.razor` ·
`Infrastructure/PublicChatBroadcaster.cs` · `wwwroot/robots.txt` · Migrationen `Phase61`–`Phase74`.

**Wiederverwenden, nicht neu bauen:** `IKassenService.BookAsync(db, …)` · `CaseNumberCounter` ·
`TextSimilarity` · `FileUploadOptions` + `FilePathHelper.SafePath` · `PrintLayout` + `*Print`-Muster ·
`HtmlCleanup` + RichTextEditor · `NotificationService` + Broadcaster-Muster · `ManualAudit` ·
`RecordSectionRail`/`RecordSection`/`PageHeader`/`EmptyState`/`StatTile`/`QueryState` ·
`BewerbungMessage`-Zielgruppen-Muster · `ThreatScoreHistory` · `CounterIntelRule` ·
`ApplicantPortalLayout` als Vorlage für `BuergerLayout`.

---

## Verifikation

**Je Phase**
1. `dotnet build NOOSE-Website.slnx` — ohne Warnung. `SearchCoverageTests`, `SearchCatalogTests`,
   `NooseiRecordTypesTests`, `PublicVisibilityCoverageTests` sind die Wächter, die bei fehlenden Einträgen
   rot werden.
2. `dotnet test NOOSE-Website.slnx` — bestehende ~3.5k Tests grün plus die neuen Suiten der Phase.
3. Dev-Server stoppen → `dotnet tool restore` → `dotnet ef migrations add Phase6X_<Name> …` → neu bauen.
4. `dotnet run` und den „Fertig, wenn"-Pfad der Phase **wirklich klicken** — zwei Komponenten auf derselben
   `@page` werfen erst zur Laufzeit, nicht beim Kompilieren.

**Vor jedem Deploy** (nach Phase 3, 9 und 16)
- Anonym im abgemeldeten Browser: Board, Steckbrief, Archiv, Gefahrenlisten, Presse, Lage, CMS-Seite,
  Druckposter. Zusätzlich in Handy-Breite — das Board wird mobil gelesen.
- Modul aus- und wieder einschalten; Kill-Switch prüfen; interner Wartungsmodus darf den öffentlichen
  Bereich **nicht** abschalten.
- Publish-Versuch auf eine VS-Akte muss scheitern; mit Rang 2 muss ein Antrag entstehen.
- Kopfgeld: behördlich + privat setzen, privaten Anteil einzahlen, Split-Auszahlung, `/kasse` prüfen
  (Saldo, Ledger-Zeilen, Deckungswarnung), Beleg drucken.
- Ticket: als Bürger öffnen, als Führung antworten — Absender zeigt **nie** einen Agentennamen; als
  Junior-Agent darf `/tickets` nicht sichtbar sein.
- Bürgerbereich mit einem **Agenten-Konto** öffnen: `/buerger` und `/buerger/profil` sind erreichbar, keine
  Zwangsumleitung zur Namenseingabe, Rückweg ins Dashboard vorhanden — und mit einer Nur-Lese-Aufsicht
  scheitert das Speichern der Zivil-Identität mit Meldung statt mit einer Schleife.
- Zeitstrahl der Personenakte enthält Publizieren, Depublizieren und Kopfgeld-Änderung.
- `App_Data` bleibt beim Deploy unberührt; `?v=` bei JS-Änderungen gebumpt.
