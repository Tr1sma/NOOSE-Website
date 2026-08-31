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
| 6 | Kopfgeld: Anteile, behördlich + privat, Deckung, Historie | `Oeffentlich06_Kopfgeld` | 4 | **fertig** |
| 7 | Hinweise Kern: Formular, Eingang, Rückfrage, Verfolgung | `Oeffentlich07_Hinweise` | 4 | **fertig** |
| 8a | Hinweise Triage: Dubletten, Priorität, Vertrauensstufe | `Oeffentlich08_HinweisTriage` (nur Indizes) | 7 | **fertig** |
| 8b | Hinweise Übernahme: Akte, Kontoverknüpfung, Gegenaufklärung | `Oeffentlich08_HinweisUebernahme` (nur Seed) | 8a | **fertig** |
| 9 | Belohnung: Split-Zuordnung, Auszahlung, Beleg | `Oeffentlich09_Belohnung` | 6, 7 | **fertig** |
| 10 | Ticket-Chat (Führungsebene) | `Oeffentlich10_Tickets` | 2 | **fertig** |
| 11 | Öffentliche Vorlagen + 4. Token-System | `Oeffentlich11_Vorlagen` | 7, 10 | **fertig** |
| 12 | Organisationen + Gefahrenlisten | `Oeffentlich12_Fraktionsprofile` | 4 | **fertig** |
| 13a | Sachfahndung: gesuchte Fahrzeuge und Waffen | keine | 4 | **fertig** |
| 13b | Einspruch gegen eine Ausschreibung | `Oeffentlich13_Einspruch` | 4, 1 | **fertig** |
| 14a | Presse: Mitteilungen, Discord-Push, Auto-Entwurf bei „gefasst" | `Oeffentlich14_Presse` | 3 | **fertig** |
| 14b | Warnungen und öffentliche Gesetzesauszüge | `Oeffentlich14_Warnungen` | 3 | **fertig** |
| 14c | Öffentliche Lageberichte | `Oeffentlich14_Lageberichte` | 3 | offen |
| 15 | Zahlen: Gefahrenlage-Ampel, Trend, Zähler, Landing-Hero | keine | 4, 7, 14a | offen |
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

## Phase 6 — Kopfgeld ✅

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

### Abweichungen vom Plan (bewusst)

- **Rang 1–2 bekommt einen echten Antragsweg**, nicht nur eine Rang-Sperre: `RequestType.Kopfgeld` mit
  eigener Spalte `KopfgeldAnteilId`, Posteingang in `/admin/freigaben`, Nav-Badge im selben Zähler wie die
  Veröffentlichungsanträge. Der Anteil entsteht als `Beantragt` — ein fünfter Status, den der Plan nicht
  vorsah, aber die Konstruktion spiegelt Phase 4 wörtlich (Ausschreibung wird `Beantragt`, der `Request`
  zeigt darauf).
- **Kein `ISoftDelete` auf `FahndungKopfgeldAnteil`.** Der Plan nannte es, aber Zurückziehen ist ein
  Status und die Historie append-only (steht so in den Plan-Tests) — ein Papierkorb-Eintrag für etwas,
  das nie gelöscht wird, wäre toter Code samt `TrashService`-Registrierung.
- **Discord nur bei Erhöhung**, nicht bei Senkung: eine Behörde, die „das Kopfgeld ist jetzt kleiner"
  postet, untergräbt die eigene Ausschreibung, und der alte Post steht ohnehin unkorrigierbar weiter.
- **`Konto` trägt eine Bedeutung für beide Herkünfte**: behördlich das Konto, aus dem gezahlt wird; privat
  das, in das eingezahlt wurde (erst beim Einzahlen gesetzt). Der Plan reservierte es für behördlich.
- **Die Summe steht nicht auf der Ausschreibungszeile**, sondern wird im Snapshot **hinter dem
  Unterdrückungsgürtel** summiert (fünfte Abfrage in `LoadAsync`, neben `HintsAsync`). Eine denormalisierte
  Summe driftet still; ein zweiter Lesepfad müsste den Gürtel wiederholen.
- **Der Anteil ist nicht beobachtbar** (`WatchlistRecordRollup` → „not watchable"): die Map ist statisch
  und ohne Datenbank, Anteil → Ausschreibung → Akte sind zwei Hops. Zeitstrahl und Chronik gehen über beide.

### Nachgewiesen

Am laufenden System, anonym gegen MariaDB (500.000 behördlich + 1.000.000 privat, dazu ein beantragter
Anteil über 250.000 und ein zurückgezogener über 999):

- `/gesucht`, `/gesucht/{az}` und `/gesucht/{az}/druck` zeigen **1.500.000** — der beantragte und der
  zurückgezogene Anteil zählen nicht.
- Obergrenze an ⇒ „bis 1.500.000 $" auf dem Steckbrief.
- Im ausgelieferten HTML kommen **Herkunft, Stifter, Konto, Status und die interne Aktennummer nicht vor**.
- Modul `Kopfgeld` aus ⇒ Summe verschwindet auf Board, Steckbrief und Poster, das Board bleibt online.
- Akte auf TRU-Verschlusssache ⇒ Karte, Steckbrief, Foto (404) **und Summe** verschwinden zusammen.
- Not-Aus ⇒ alle drei Seiten dunkel, `/buerger` bleibt erreichbar.
- Müll-Queries (`?betrag=quatsch`, `?kopfgeld=1&x=%00`, `?kopfgeld=ja` am Poster, erfundenes Aktenzeichen):
  **200, kein 500**.
- Intern gerendert: das Kopfgeld-Panel an der Personenakte (Summe, Aufschlüsselung, Status-Chips),
  der Deckungs-Abschnitt `/kasse?tab=kopfgeld` und der Antrags-Abschnitt in `/admin/freigaben`.
- Serverlog ohne Exception und ohne Rollup-Warnung (nur die bekannte https-Port-Warnung).

### In der Nachprüfung gefunden und behoben

1. **Ein gelöschter Entwurf ließ seinen Kopfgeld-Antrag im Posteingang stehen.** `PendingRequests` verlangte
   nur, dass der *Anteil* noch `Beantragt` ist, nicht dass die Ausschreibung existiert — anders als der
   Veröffentlichungs-Posteingang, der genau diese Klausel trägt. Genehmigen antwortete danach „Ausschreibung
   nicht gefunden", während das Nav-Badge die Zeile weiterzählte. Regressionstest
   (`DeletingTheNotice_TakesItsRequestOutOfTheInbox`) und live nachgemessen.
2. **Die „ausgeschrieben"-Regel stand vier Mal im Code** (EF-Prädikat im Snapshot, EF-Prädikat im
   Vorher/Nachher, In-Memory-Prädikat der Aufschlüsselung, dazu eine ungenutzte Liste im Display-Typ). Jetzt
   einmal in `Services/Public/BountyShares.cs`, Muster `AgentSelection`; die tote Liste ist weg.
3. **Ein Antrag klingelte als „Antrag entschieden".** Der Filing-Pfad benachrichtigte die Führung über
   `NotificationType.RequestDecided` — es gibt keinen Typ für „gestellt", und Phase 4 klingelt beim Stellen
   eines Veröffentlichungsantrags bewusst gar nicht. Die Benachrichtigung ist entfernt; das Badge ist das Signal.
4. **Kopfgeld setzen verwarf ungespeicherte Editor-Eingaben.** `BountyPanel` meldete jede Änderung an
   `PublicWantedEditor`, dessen `LoadAsync` `_input` aus dem Entwurf neu füllt — ein getippter, noch nicht
   gespeicherter Anzeigename war danach weg. Das Panel ist selbstgenügsam (der Editor rendert keine
   Kopfgeld-Daten), also ist die Rückmeldung entfallen.
5. **Karte und Summe konnten auseinanderlaufen**, wenn zwei sichtbare Zeilen dasselbe Aktenzeichen tragen: der
   Deduplizierungs-Pfad wählte die Zeile zweimal unabhängig. `LoadAsync` wählt sie jetzt einmal (`chosen`) und
   baut Karte, Chips und Summe daraus. Nur über den Unique-Index erreichbar, aber der Zweig existiert genau für
   den Fall, dass er verletzt ist.
6. **Zwei neue Abfrageformen waren nur gegen SQLite belegt** — der Posteingangs-Join über die
   `Wanted`-Navigation und die verschachtelte Dubletten-Prüfung. Beide kompilieren jetzt in
   `MySqlTranslationTests` gegen Pomelo; eine Übersetzungslücke wäre ein 500 auf einem Schreibpfad gewesen.
7. **Die Deckungswarnung im Panel las sich wie eine Aussage über diesen Kopf**, ist aber kassenweit — jetzt
   ausdrücklich „Deckungslücke der Kasse (alle Ausschreibungen)".

**Geprüft und in Ordnung:** der Posteingang liest **keine** Live-Aktendaten — `TargetDesignation` und
`AnzeigeName` sind beim Stellen bzw. Publizieren eingefrorene Kopien, eine später eingestufte Akte leakt also
nichts. Dass `RejectRequestAsync` (anders als `ApproveRequestAsync`) kein Aktengate hält, ist Absicht: wird die
Akte zur Verschlusssache, muss Genehmigen scheitern und Ablehnen weiter möglich sein, sonst hängt der Antrag
für immer. Und `PayInAsync` verlangt bewusst keine unklassifizierte Akte: der Anteil zählt bereits, ein Riegel
würde nur das Geld stranden lassen.

**Bekannte Grenze:** löscht die Führung eine Kassenbuchung, bleibt der zugehörige Anteil `Gesichert` und zeigt
kein Aktenzeichen mehr. Dieselbe Klasse wie `FinancingRequest.KassenBuchungId`; aufgelöst wird sie mit der
Auszahlung in Phase 9.

**Nicht geprüft:** die Spalte „Kopfgeld" in `/fahndung?tab=oeffentlich` — dieser Abschnitt hängt an
`Policies.InternalAgent`, den das Demo-Principal nicht erfüllt; dafür braucht es einen echten Discord-Login.
Ebenso ungeprüft im laufenden System: der Discord-Push (kein Webhook konfiguriert) und die Dialoge, die
einen Circuit brauchen. Ihre Logik hängt vollständig an den getesteten Diensten.

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

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Zwei Routen, ein Buchstabe Unterschied.** `/hinweis` ist das öffentliche Formular (Modul-Nav-Route,
   indexierbar), `/hinweise` der interne Eingang. `PublicRoutes.Matches` schneidet an der Segmentgrenze, die
   interne Seite ist also nicht öffentlich — festgehalten wie bei `/fahndung` vs. `/gesucht`. Der
   **NavCatalog-Key heißt `buergerhinweise`**: `hinweise` gehört seit Langem den algorithmischen
   *Ermittlungs*hinweisen (`/ermittlungshinweise`), und gespeicherte Favoriten zeigen darauf.
2. **Das Formular ist die erste interaktive Seite unter `Components/Pages/Public/`.** Leitsatz 5 erlaubt das
   für Formulare; damit `PublicPageScanTests` weiter jede *Lese*seite statisch hält, gibt es dort jetzt
   `InteractiveExempt` — eine Ausnahme je Datei mit Begründung, Muster `LayoutExempt`. Alle anderen Prüfungen
   der Datei gelten unverändert weiter.
3. **Der Knopf am Steckbrief ist ein Link, kein Dialog.** `/gesucht/{az}` ist `[ExcludeFromInteractiveRouting]`,
   ein `MudDialog` wäre dort *stumm* tot (dieselbe Klasse wie `PrintFrame` beim Poster). Er führt auf
   `/hinweis?fahndung={Aktenzeichen}` und erscheint nur bei eingeschaltetem Modul.
4. **Der Bezug wird über das Aktenzeichen aufgelöst, nie über eine Id vom Client.** `SubmitAsync` fragt
   `IPublicWantedService.GetByCaseNumberAsync` — also den Lesepfad *hinter* dem Unterdrückungsgürtel — und
   sucht die Zeilen-Id erst danach. Eine rohe `FahndungId` anzunehmen machte das Formular zum Existenz-Orakel
   für Entwürfe und zurückgezogene Ausschreibungen.
5. **Das Rate-Limit steht im Dienst, nicht in der Middleware.** Die Einreichung läuft über SignalR und erreicht
   `UseRateLimiter` nie. `TipRules.PerDay` wird in `SubmitAsync` gezählt — **mit `IgnoreQueryFilters`**, sonst
   kauft Löschen einen weiteren Versuch. Die Policy `noose-hinweis` hängt nur am Datei-Endpoint.
6. **Nur `SubmitAsync` hängt am Modul-Schalter**, weder `ReplyAsCitizenAsync` noch die Lesepfade des Bürgers.
   Derselbe Ruf wie bei `BuergerRegistrierung`: der Schalter stoppt Neues, er strandet keine laufende
   Unterhaltung — und die Rückfrage hat die Behörde selbst gestellt.
7. **Anonymität ist eine Projektion und eine Audit-Regel, keine UI-Bedingung.** `TipDetail.CitizenName` ist
   `null`, solange die Zusage gilt; und weil der Interceptor beim Einreichen das *einreichende Konto* stempelt —
   ein Agent kann über seine Zivil-Identität melden —, streichen Zeitstrahl und Chronik den Akteur einer
   `Hinweis`-Zeile. Die eine Regel steht in `Services/Public/TipAnonymity.cs`, beide Lesepfade nennen sie, und
   das Änderungsprotokoll auf `/nachweis` ruft sie **bewusst nicht**: dort ist das Konto die Missbrauchskontrolle.
8. **Eine Bürger-Zeile trägt baulich keinen Agenten.** `HinweisNachricht.AutorAgentId` wird nur auf
   `Intern`-Zeilen gesetzt, und `CitizenTipMessage` hat gar kein Autorfeld — nach außen ist der Absender
   konstant „NOOSE". Adressiert wird ein Hinweis draußen über sein **Aktenzeichen**, nie über die Zeilen-Id
   (`PublicWantedModelTests.OutwardModels_CarryNoBareRecordId` gilt für die drei neuen Records mit).
9. **Zwei Guards, nicht einer.** `Permission.RequireTipRead` lässt jeden internen Agenten samt Nur-Lese-Aufsicht
   in den Eingang (sie liest sonst alles — der Eingang wäre die einzige Ausnahme), `RequireTipHandling` verlangt
   zusätzlich Schreibrecht. Beide bauen auf der neuen `AgentPrincipalExtensions.IsInternalAgent()`; `Active`
   allein erledigt vier Ausschlüsse (Pending, Blocked, Bewerber, Bürger).
10. **Der Anhang ist nicht öffentlich.** Eigener Pfad `App_Data/uploads/hinweise`, eigener Storage-Dienst,
    Endpoint `/dateien/hinweise/{id}` mit `.RequireAuthorization()` — anders als die Fahndungs-Fotoroute, deren
    Autorisierung die Publikationsprüfung ist. Der Dienst gibt Eigentümer und Bearbeitern etwas, allen anderen
    `null` ⇒ eine `404`.
11. **Zwei nicht routbare `NotificationType`-Werte.** `NotifyManyAsync` pusht jede routbare Kategorie nach
    Discord; ein eingehender Bürgerhinweis im öffentlichen Kanal würde den Hinweisgeber outen. Benachrichtigt
    wird die Führung (der Eingang hat für alle anderen ein Nav-Badge) — ein Glockenschlag je Meldung für jeden
    Agenten erzieht das Haus dazu, die Glocke zu ignorieren.
12. **Kein Volltext, keine Suche, kein NOOSEI.** Beide Tabellen stehen in `SearchCatalog.NotSearchable` mit
    Begründung: ein Provider müsste die Anonymitätszusage mittragen, die bisher nur die Bearbeiter-Projektion
    kennt. Das kommt mit Phase 16.
13. **Fünf Registries, wie gehabt** — `PublicVisibility` (beide `NeverPublic`), `SearchCatalog`,
    `TrashService`/`TrashProjection` (die Papierkorb-Zeile nennt weder Bürger noch Text),
    `AuditEntityDisplay`, `WatchlistRecordRollup` (beide „not watchable": zwei Hops zur Akte, und jede Chatzeile
    würde feuern). Dazu die vier Zeitstrahl-Registrierungen und `MergedPageSections.Trash`.

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

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

Aufgeteilt in **8a** (Triage: Dubletten, Priorität, Vertrauensstufe) und **8b** (Übernahme, Kontoverknüpfung,
Gegenaufklärung). 8a ist reine Trage-/Sortierlogik in eigenen Dateien, 8b fasst `PersonService`/`CaseService`/
`ObservationService`/`LinkService` **und** die Gegenaufklärungs-Engine an — zwei Fertig-Kriterien, zwei Testwellen.

1. **Zwei Migrationen statt „keine".** `Oeffentlich08_HinweisTriage` legt **nur zwei Indizes** an
   (`{Status, Prioritaet}` für die neue Standard-Sortierung, `DublettenGruppeId` für Gruppen),
   `Oeffentlich08_HinweisUebernahme` **nur eine `InsertData`-Zeile** (die vierte Vorgabe-Regel). Am Datenmodell
   ändert sich nichts — die Felder standen schon aus Phase 7 bereit.
2. **Die Priorität multipliziert Bänder mit Untergrenze 1, nicht die Rohwerte.** „Kopfgeld × Gefahrenstufe ×
   Vertrauen" wörtlich genommen wäre ohne Kopfgeld immer 0, und ein `Critical`-Hinweis sortierte unter eine
   Bagatelle. `TipPriority` ist die einzige Wahrheit der Formel (1..100). Bewusste Folge: ein Hinweis **ohne**
   Fahndungsbezug bekommt `1 × 1 × Vertrauensband` und liegt unter jedem bezogenen — tragbar, weil der Eingang
   ohnehin nach Status in drei Reiter trennt.
3. **`Prioritaet` ist ein Cache, und `TipPriorityService` ist sein einziger Schreiber.** Er hängt **nur** am
   `IDbContextFactory` — genau deshalb dürfen `PublicWantedService` und `BountyService` ihn rufen, ohne einen
   DI-Zyklus zu bauen (`TipService → IPublicWantedService` besteht schon). Geschrieben wird per
   `ExecuteUpdateAsync` und nur für **offene** Hinweise: ein getrackter Write stempelte `GeaendertAm`, schriebe
   eine `AuditLog`-Zeile und schöbe den Hinweis bei jeder Kopfgeld-Erhöhung auf den Zeitstrahl der Personenakte.
   Vierter dokumentierter Fall neben Score-Writes, `FactionRecency.StampAsync` und `CountViewAsync`.
   `BountyService.SaveAsync` nimmt dafür die `wantedId` — ein Choke-Point für beide Folgen eines Anteil-Writes
   (Snapshot verwerfen **und** nachstempeln).
4. **Die Datei nennt `FahndungKopfgeldAnteile`, aber kein `SaveChangesAsync`** — deshalb greift
   `PublicSurfaceGuardTests.EveryWriterOfTheBountyTable_DropsThePublicSnapshot` bewusst nicht: sie *liest*
   Anteile und schreibt `Hinweise`. Wer dort je ein `SaveChangesAsync` ergänzt, braucht
   `InvalidatePublicViewAsync`, und der Wächter sagt es ihm.
5. **Dublettenerkennung ist ein symmetrisches Maß, nicht `PhraseSimilar`.** Das verlangt für *jedes* Wort einer
   Seite einen Partner — zwei Meldungen zum selben Vorfall mit ungleicher Länge fielen damit immer durch, und
   ein Zweizeiler „Der Gesuchte war am Hafen" schluckte einen ausführlichen Bericht. `TipDuplicates` mittelt
   deshalb beide Richtungen (Schwelle 0.6) und verlangt mindestens vier tragende Wörter je Seite. In-Memory,
   weil Pomelo keine Edit-Distanz übersetzt — dieselbe Begründung wie in der Suche.
6. **Gruppiert wird nach dem Commit und nie auf Kosten der Einreichung.** Kandidatenfenster: 30 Tage,
   **gleicher** Fahndungsbezug (beide `null` zählt als gleich, mit ausgeschriebenen Zweigen — ein `== null`
   gegen eine Variable übersetzt zu SQL-`NULL` und fände nichts), 300 neueste. Ein Fehlschlag der Erkennung
   wird geloggt und verworfen: der Hinweis ist schon gespeichert.
7. **Die Vertrauensstufe wird neu berechnet, nicht inkrementiert.** `TipRules.IsTransitionAllowed` erlaubt
   `Bestaetigt → InPruefung → Bestaetigt`; ein Inkrement zählte denselben Hinweis mehrfach und bliebe nach einem
   Rückzieher dauerhaft zu hoch. `RecomputeConfirmedTipsAsync` zählt die Zeilen und ist damit selbstheilend —
   auch beim Löschen und Wiederherstellen, die denselben Pfad rufen. `TipRules.PerDay` bleibt stehen und **ist**
   `TipTrust.DailyQuota(1)`; ein Test hält die Gleichheit, zwei Zahlen wären Drift.
8. **Die Vertrauens*stufe* geht auch bei anonymen Hinweisen nach innen, die exakte Zahl nicht.** Die Stufe
   steckt ohnehin als Faktor in der sichtbaren Priorität und ließe sich zurückrechnen; die Zusage gilt der
   Identität, nicht der Erfolgsbilanz. `CitizenConfirmedTips` bleibt gesperrt (ein Wiedererkennungsmerkmal),
   `CitizenName` unverändert `null`.
9. **Jede Übernahme endet in einer *manuellen* Verknüpfung.** `TimelineService`, `ChronikParentResolver` und
   `LinkPanel` filtern `!v.Automatic` — eine automatische Verknüpfung wäre auf dem Zeitstrahl unsichtbar, und
   genau dort ist sie der Herkunftsnachweis. Ein zweites `ManualAudit.Row` gegen die Personenakte gibt es
   deshalb **nicht** (Präzedenz Phase 4: eine `Person`-getypte Zusatzzeile liest sich als „Akte geändert").
10. **`TipTakeoverService` ruft nur Dienste, es baut keine Entität.** Vorbild `ApplicationCaseService`: die
    Klassifizierungs-Gates bleiben bei `PersonService`/`ObservationService`, die Sichtbarkeitsprüfung des Ziels
    bei `LinkService.CreateAsync`, und `ICaseNumberService.NextAsync` bekommt seine umschließende Transaktion
    von den Diensten. **Eine Ausnahme braucht eine eigene Prüfung:** `ObservationService.CreateAsync` gatet nur
    die `SecrecyLevel`, nicht die Einstufung der Akte, und die `personId` kommt vom Client — `ToObservationAsync`
    ruft daher selbst `Visibility.IsRecordVisibleAsync`, sonst schriebe ein Rang-2-Agent in eine
    Verschlusssache, die er nicht öffnen darf. Der Guard davor ist `RequireTipHandling` — `MayWrite` allein ließe ein angemeldetes
    Bürgerkonto durch. Ein `Neu`-Hinweis geht danach auf `InPruefung` (wie beim Übernehmen); bestätigen bleibt
    eine eigene Entscheidung, weil daran Vertrauensstufe und später die Auszahlung hängen.
11. **Doppelklick-Schutz statt Compare-and-swap.** Es gibt keine Anspruchsspalte auf `Hinweise`, also prüft
    `ToNewPersonAsync` vorab auf eine bestehende `Hinweis → Person`-Verknüpfung und weist mit Klartext ab;
    verliert ein zweiter Tab das Rennen doch, wird die frische Akte soft-gelöscht und die Verwerfung auditiert
    (wörtlich der Rollback aus `ApplicationCaseService`). Anders als bei Geld ist eine doppelte Akte ein
    Papierkorb-Eintrag, kein Schaden.
12. **Drei Registries, damit die Verknüpfung nicht als GUID erscheint.** `LinkService` (Auflösungs-Arm **und**
    `knownTypes`), `RecordsReference` (sonst steht auf dem Zeitstrahl „Akte") und `LinkPanel.TypeDisplay`.
    Ausgeliefert wird nur das Aktenzeichen — ein Hinweis trägt einen Bürger hinter sich. Für einen Partner
    fällt die Verknüpfung automatisch heraus: `releasedTargets` kennt den Typ nicht.
13. **Die Hinweisgeber-Historie an der Personenakte listet nur offengelegte Hinweise — auch nicht als Zähler.**
    Der Abschnitt ist über die Identität des Bürgers verschlüsselt, eine Zahl nannte ihn also durch Rechnen.
    Die Regel steht als `TipAnonymity.IsHidden` plus Query-Zwilling `TipAnonymity.Disclosable` an **einer**
    Stelle, und beide Lesepfade nennen sie. Der Abschnitt ist intern (`@if (!_isPartner)`), sein Slug steht
    **nicht** in `_tabs` und **nicht** in `PartnerTabCatalog`.
14. **Kontoverknüpfung ist Führungsarbeit und prüft mehr als ihr Vorbild.**
    `IBuergerService.LinkPersonAsync` verlangt `RequireLeadership` + `RequireWriteAccess` und prüft
    `Visibility.IsRecordVisibleAsync` gegen den Akteur — `BewerbungService.LinkPersonAsync` prüft nur
    `AnyAsync(p => p.Id == personId)` und ließe damit eine Verschlusssache verlinken, die der Akteur nicht
    öffnen darf. Das wurde bewusst nicht kopiert.
15. **Die Gegenaufklärung wurde als echte Bedingungskategorie gebaut, nicht als Nebenpfad.** Die Engine hat
    keine Regel-Art, sondern eine kombinierbare Bedingungsmenge; `ActorSharesOrgWithTarget` ist deshalb ein
    Tri-State im Akteur-Block (Idiom von `RequireTru`) mit **`null` als Default** — die geseedeten Regeln tragen
    ihr JSON als Literal aus der Migration und kennen die Eigenschaft nicht. Sieben koordinierte Stellen:
    Definition + `NeedsOrgLookup`, drei Felder auf `CounterIntelEvent`, Anreicherung im Loader, ein
    **fail-closed** Arm in `Matches`, ein `if`-Block in `Summary`, ein `Flag(...)` im Dialog samt `ActorLabel`,
    und die vierte Vorgabe-Regel in `CounterIntelRuleDefaults` + Migration.
16. **Aufgelöst wird über die Zivil-Identität des handelnden Kontos**, nicht über den Agentenstatus: ein Agent,
    der über sein Bürgerprofil meldet, ist derselbe Interessenkonflikt. Ziel-Person ist bei einem `Hinweis` die
    Person hinter der Ausschreibung, bei einem `Person`-Ereignis die Akte selbst; alles andere bleibt `null` und
    kann die Bedingung baulich nicht erfüllen. Meldung über die **eigene** Akte gilt als geteilt — die stärkste
    Form desselben Konflikts.
17. **Das Cockpit ist keine Hintertür um `ResolveAnonymityAsync`.** Sind **alle** gezählten Ereignisse einer
    Gruppe Hinweise mit gewahrter Zusage, heißt das Subjekt „Anonymer Hinweisgeber" und trägt **kein** `Href`;
    sonst zeigt ein Bürgerkonto auf `/einstellungen?tab=buerger` statt auf `/personal/{id}`, das für einen
    Zivilisten ins Leere führt. Gemeldet wird das Muster, der Name kommt weiter nur über den auditierten,
    führungsgebundenen Weg.
18. **Neue Wächter.** `MySqlTranslationTests` bekam fünf Formen dazu (Kandidatenfenster in beiden
    Bezugs-Zweigen, Gruppen-Zählung über eine nullable Spalte, Kopfgeld-Summe je Ausschreibung, die beiden
    `TipRules`-Prädikate). `TheSeededOwnCircleRule_MatchesTheCodeDefault` liest das JSON-Literal aus der
    Migration und vergleicht es mit `CounterIntelRuleDefaults` — vorher hätte eine Drift zwischen Code und
    Seed niemand bemerkt. Und `PublicWantedModelTests` verlangte für die zwei neuen `Models.Public`-Records
    je eine Begründung, wie vorgesehen.

---

## Phase 9 — Belohnung & Auszahlung ✅

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

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Eigener `IRewardService` statt `IBountyService.PayoutAsync`.** `BountyService` ist einaudienz-intern — jede
   Methode beginnt mit einem internen Guard. Die Belohnung hat zum ersten Mal auf dem Geldpfad **zwei** Audienzen:
   das Panel der Führung und den Beleg des Bürgers. Muster `TipService` (zwei Audienzen) bzw. `TipTakeoverService`
   (Orchestrator, ruft nur Dienste). Der neue Dienst besitzt `HinweisBelohnungen` und ist der einzige Schreiber von
   `BountyShareStatus.Ausgezahlt`.
2. **„Gefasst" bleibt eine eigene Handlung.** Die Auszahlung **verlangt** `Status == Gefasst` und weist sonst mit
   Klartext ab, statt die Ausschreibung selbst umzuschalten. Die Fahndungstabelle behält damit ihren *einen*
   Schreibpfad (`PublicWantedService`, `PublicWantedCacheDisciplineTests`), und die Reihenfolge ist die des
   Geschehens: erst die Ergreifung, dann das Geld. Das Panel schreibt es hin, statt einen toten Knopf zu zeigen.
3. **Eine Auszahlung je Ausschreibung, alles auf einmal.** Ein Dialog verteilt das gesamte ausgeschriebene Kopfgeld
   auf 1..n Hinweisgeber; danach sind **alle** beworbenen Anteile `Ausgezahlt`. Der Statuswechsel ist der
   Idempotenz-Token — dieselbe Konstruktion wie beim Ablauf-Worker aus Phase 5 —, deshalb existiert die Klasse
   „doppelt bezahlt" baulich nicht, und es gibt keine Restbetrags-Verfolgung je Anteil, die zwei Tabs sich teilen
   könnten. Gesetzt wird per Compare-and-swap (`ExecuteUpdate` mit `BountyShares.Advertised` im `Where`, betroffene
   Zeilenzahl geprüft), wörtlich das Muster aus `PayInAsync`, samt `ManualAudit.Row` je Anteil.
4. **`Ausgezahlt` heißt erledigt, nicht restlos geleert.** Wird weniger verteilt als ausgeschrieben, bleibt der Rest
   in der Kasse und der Anteil gilt trotzdem als abgeschlossen. Sonst zählte `GetCoverageAsync` das Geld eines
   abgeschlossenen Falls für immer als offene Verpflichtung.
5. **Kein `ISoftDelete`** — anders als im Plantext, und aus demselben Grund wie beim Kopfgeld-Anteil aus Phase 6:
   Geldhistorie ist append-only, und ein Soft-Delete-Filter könnte genau die Zahlungsspur verbergen, deren Nachweis
   der Beleg ist. Folge: keine `TrashService`-Registrierung, kein `MergedPageSections`-Eintrag. Eine Fehlbuchung wird
   in der Kasse gegengebucht, nicht gelöscht.
6. **Die Belegnummer trägt eine Gruppe, sie ist nicht eindeutig je Zeile.** Eine Zeile ist ein
   (Hinweis × Anteil)-Paar, weil ein Hinweis Geld aus zwei Anteilen mit verschiedenen Konten ziehen kann. Der Bürger
   bekommt **einen** Beleg je Hinweis, also ist `BelegNummer` je (Auszahlung, Hinweis) geprägt und der Index
   **nicht** unique; der Beleg summiert seine Zeilen. Eindeutig indexiert ist `KassenBuchungId` — eine Buchung deckt
   höchstens eine Zeile, wie beim Anteil. Präfix **`BEL`**, weil `B` den Bewerbungen gehört und `CaseNumberCounter`
   auf `(Präfix, Jahr)` verschlüsselt ist.
7. **Die Verteilregel steht einmal, in `Services/Public/RewardAllocation.cs`.** Zuerst wird Geld verwendet, das ohne
   persönliche Übergabe fließt (`Gesichert` liegt in der Kasse, `NooseKasse` ist behördlich), erst danach eine
   unbezahlte private Zusage — die verlangt, dass ein Agent physisch Bargeld übergibt, und ist damit die schwächste
   Deckung. Innerhalb jeder Gruppe ältester Anteil zuerst, `AnteilId` als Gleichstand-Entscheider: dieselbe
   Auszahlung muss immer dieselben Buchungen erzeugen. Dort sitzt auch die Σ-Invariante und die Ablehnung einer
   dritten Dezimalstelle — die Spalte hält zwei, die Datenbank würde die dritte wortlos abschneiden.
8. **`Permission.RequireRewardPayout` ist eine eigene Achse.** `RequireKassenBookingWrite` greift nur im
   Buchungszweig, eine vollständig aus privater Zusage bezahlte Belohnung liefe also **ohne** Führungsprüfung durch.
   Und der Schreib-Guard steht vor allem anderen (Präzedenz Phase 6): `RequireLeadership` allein lässt die
   Nur-Lese-Aufsicht und das Demo-Principal durch, die dann Belegnummern prägen, bevor der
   `ReadOnlyBarrierInterceptor` das Speichern verweigert.
9. **Der Verwendungszweck der Kassenbuchung nennt nur Aktenzeichen.** `/kasse` liest jeder Agent; ein Bürgername
   dort wäre die Anonymitätszusage über das Kassenbuch umgangen — auch bei aufgelöster Anonymität bleibt es bei
   `Belohnung {Hinweis-Az} · Fahndung {Fahndungs-Az}`. Eigener Test.
10. **Anonym bleibt unauszahlbar.** Ein Hinweis mit gewahrter Zusage wird abgewiesen (`TipAnonymity.IsHidden`), weil
    Geld einen Empfänger braucht und der Beleg ihn nennt; auflösen darf weiter nur die Führung, auditiert. Ein
    `Neu`-Hinweis ist ebenfalls nicht auszahlbar — `TipRules` erlaubt den Sprung nach `FuehrteZurErgreifung` bewusst
    nicht, ein Hinweis wird erst bearbeitet, dann belohnt. Der Dialog listet beide Fälle mit Grund statt sie zu
    verstecken.
11. **Die Statusregeln des Hinweises bleiben in `TipService`.** Die Auszahlung ruft zwei neue Methoden:
    `MarkRewardedAsync(db, …)` schreibt in den Kontext **und die Transaktion des Aufrufers** (Muster
    `IKassenService.BookAsync(db, …)`) und legt die Bürger-Nachricht ohne Autor an — **nicht** über
    `AskCitizenAsync`, das einen abgeschlossenen Hinweis abweist und auf `Rueckfrage` schalten würde; `AfterRewardAsync`
    läuft **nach** dem Commit und macht Vertrauenszähler, Eingangs-Sortierung, Benachrichtigung und Live-Update.
12. **`PublicRewardPaid` ist nicht routbar.** `NotifyManyAsync` pusht jede routbare Kategorie in den öffentlichen
    Discord-Kanal, und eine Belohnungsmeldung dort outet den Hinweisgeber.
13. **Das Modul-Gate sitzt auf den Bürger-Lesepfaden, nicht auf der Auszahlung.** Präzedenz Phase 4/5: Publizieren
    braucht ein lebendes Modul, *De*publizieren nie. Eine Auszahlung ist eine interne Geldbewegung — sie darf die
    Kasse nicht blockieren. `Available` steht damit auf `true`, `DefaultEnabled` bleibt `false`: eine frische
    Installation zeigt Belege erst, wenn jemand den Schalter umlegt.
    **Und gefragt wird die gespeicherte Wahl allein, nicht `RequireEnabledAsync`** — das faltet den Not-Aus ein, und
    der nimmt laut Leitsatz aus Phase 2 den privaten Kontobereich `/buerger` bewusst *nicht* mit. Ein Beleg ist der
    private Inhalt eines angemeldeten Bürgers; ein eigener Test hält fest, dass der Not-Aus ihn stehen lässt.
    Aus demselben Grund gibt `PartnerRoutes.IsAllowed` für `/buerger/**` jetzt `true` zurück: `BuergerLayout` fragt
    diese Liste gar nicht, `PrintLayout` schon — ohne die Zeile wäre die Druckseite die *einzige* Bürgerseite, die
    einem Partner mit Zivil-Identität „nicht freigegeben" meldet.
14. **Beleg: Eigentümer **und** Führung.** Beide lesen ihn, jeder andere bekommt `null` ⇒ „nicht gefunden" — nie
    „kein Zugriff", sonst wäre die Route ein Existenz-Orakel über Auszahlungen. Der Bearbeiter steht auf **keiner**
    Projektion: `CitizenRewardReceipt` kann ihn strukturell nicht tragen, und die Seite setzt `PrintedBy` nicht.
15. **Der Zeitstrahl braucht drei Hops und zwei Abfragen.** Belohnung → Anteil → Ausschreibung → Akte. In
    `TimelineService.AuditSourceAsync` gestaffelt statt verschachtelt, weil `IgnoreQueryFilters()` für die ganze
    Kompilierung gilt und in einer Unterabfrage den Soft-Delete-Filter auch außen entfernt (die Phase-4-Falle).
    Registriert sind alle vier Stellen: `AuditSourceAsync`, `TimelineDisplay.MapAudit`, `AuditEntityDisplay` und
    `ChronikParentResolver`. **Nicht** in der Beobachtungsliste (`WatchlistRecordRollup`): die statische Map hat
    keine Datenbank für drei Hops, und das beobachtbare Ereignis ist das Gefasst-Setzen der Ausschreibung.

---

## Phase 10 — Ticket-Chat ✅

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

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Ein Ticket hängt an keiner Akte, und das entscheidet fünf Registrierungen.** Es ist Schriftwechsel, kein
   Aktenmaterial: kein Fall in `TimelineService.AuditSourceAsync`, kein Titel in `TimelineDisplay.MapAudit`, kein
   Eintrag im `ChronikParentResolver`, keine Zeile in `RecordsReference`/`LinkService`. Registriert ist es dort, wo
   die Zugehörigkeit zu einer Akte keine Rolle spielt: `PublicVisibility` (beide Tabellen `NeverPublic`),
   `SearchCatalog` (`NotSearchable`, Provider kommt mit Phase 16), `AuditEntityDisplay` (Label **und** Route),
   `WatchlistRecordRollup` („not watchable"), `TrashService`/`TrashProjection` und `MergedPageSections.Trash`.
2. **Zwei Guards, und `MayClassifiedRead` ist die Service-Seite von `Policies.LeadershipPage`.**
   `Permission.RequireTicketRead` = interner Agent **und** `MayClassifiedRead()` — also Führung *oder*
   Nur-Lese-Aufsicht, exakt die Menge, die die Seiten-Policy hereinlässt. `RequireTicketHandling` legt `MayWrite()`
   darüber, und zwar **vor** der Rangprüfung (Präzedenz Phase 6/9): `RequireLeadership` allein ließe Aufsicht und
   Demo-Principal Aktenzeichen prägen, bevor der `ReadOnlyBarrierInterceptor` das Speichern verweigert.
   `IsInternalAgent()` steht davor, weil ein angemeldetes Bürgerkonto überhaupt keinen Rang-Claim trägt.
3. **Der Absender nach außen ist eine Konstante, keine Bedingung in der UI.** `TicketRules.AgencySender`
   („NOOSE – Führungsebene") steht einmal; eine Antwort der Führung wird mit `AutorAgentId = null` geschrieben, und
   `CitizenTicketMessage` hat **kein** Autorfeld. Zwei Schichten wie beim Kopfgeld: die bauliche und ein Dateiscan
   (`PublicSurfaceGuardTests.NoCitizenPage_NamesTheHandlerSideOfAConversation`) über `Components/Pages/Portal/` —
   eine Bürgerseite könnte den Dienst auch selbst nach der Bearbeiter-Projektion fragen. Der Scan matcht **ganze
   Bezeichner**: `CitizenTicketDetail` enthält `TicketDetail`, ein Teilstring-Treffer hätte genau die Typen
   gemeldet, die die Zusage einhalten. Deshalb liegt auch der Öffnen-Dialog unter `Pages/Portal/Shared/` und nicht
   in `Common/Shared` — der Ordner **ist** die Grenze des Wächters.
4. **Zwei unabhängige Deckel, und nur einer ignoriert den Soft-Delete.** `MaxOpen = 2` zählt lebende Zeilen
   (`TicketRules.OpenRows`), `PerDay = 3` zählt mit `IgnoreQueryFilters` über 24 Stunden. Die Asymmetrie ist
   gewollt: löscht die Führung ein missbräuchliches Ticket, bekommt der Bürger den Platz zurück, sein
   Tageskontingent bleibt verbraucht. Beide Deckel sitzen im Dienst, nicht in der Middleware — das Öffnen läuft
   über SignalR und erreicht `UseRateLimiter` nie (Präzedenz `TipRules.PerDay`).
5. **Geschlossen ist geschlossen.** Eine Antwort des Bürgers auf ein abgeschlossenes Ticket wird abgewiesen statt
   es wiederzueröffnen; `TicketRules.IsTransitionAllowed` erlaubt `Geschlossen → InBearbeitung` nur der Führung,
   und bewusst nicht zurück auf `Offen` — gelesen hat es zu diesem Zeitpunkt jemand. Wiedereröffnen räumt
   `GeschlossenAm` und `GeschlossenVonId` ab: die beiden Felder beschreiben die *aktuelle* Schließung, nicht
   deren Geschichte.
6. **Automatisch bewegen sich genau zwei Kanten.** Eine Antwort der Führung schaltet auf `WartetAufBuerger`, eine
   Antwort des Bürgers von dort zurück auf `InBearbeitung`. Ein **unangetastetes** Ticket bleibt `Offen`, auch wenn
   der Bürger nachträgt — sonst behauptete der Status, jemand arbeite daran. Ein Auto-Schluss wartender Tickets ist
   bewusst nicht gebaut; er wäre ein eigener Worker samt Idempotenz-Token (Präzedenz `PublicWantedExpiryWorker`).
7. **Lesestände per `ExecuteUpdateAsync`, `LetzteAktivitaetAm` getrackt.** Lesen ist keine Änderung (Präzedenz
   `MarkCitizenReadAsync` bei den Hinweisen); der Aktivitätsstempel dagegen reitet auf dem Statuswechsel mit, der
   ihn verursacht hat. Anders als bei einem Hinweis ist das unschädlich: ein Ticket hängt an keiner Akte, also
   verschmutzt ein `GeaendertAm` keinen Zeitstrahl, und `TicketNachricht` ist ohnehin `IAuditable`. Je Seite ein
   eigener Stempel — die Aufsicht setzt **keinen** (`MayWrite()`-Prüfung im Dienst, nicht erst im Interceptor).
8. **Beide Benachrichtigungen sind nicht routbar.** `NotifyManyAsync` pusht jede routbare Kategorie in den
   öffentlichen Discord-Kanal, und das Anliegen eines namentlich bekannten Bürgers gehört dort nicht hin. Beim
   Bürger klingelt es nur bei `WartetAufBuerger` und `Geschlossen`: ein interner Sprung von `Offen` auf
   `InBearbeitung` ist Nachricht an den Schalter, nicht an ihn.
9. **Die Papierkorb-Zeile nennt Betreff und Status, nicht den Bürger und nicht den Schriftwechsel** (Muster
   `TrashProjection.Tip`). `/papierkorb` hängt an `Policies.LeadershipPage`, liest also dieselbe Audienz — der
   Faden selbst gehört trotzdem den beiden Beteiligten.
10. **`TicketBroadcaster` trägt zwei Handles**, nicht eines: der Schalter kennt die Zeilen-Id, die Bürgerseite nur
    das Aktenzeichen. Ohne das zweite müsste jeder Bürger-Circuit bei **jeder** Ticketänderung im Haus neu laden,
    um herauszufinden, ob es das eigene war. Er heißt nach seiner Domäne (`Infrastructure/Chat/`, Muster
    `TipsBroadcaster`) und nicht `PublicChatBroadcaster` — ein Sammelname verspricht einen zweiten öffentlichen Chat.
11. **`TicketArt` hat genau einen Wert.** Die Spalte existiert, damit eine zweite Art ein Enum-Wert und keine
    Migration ist; Vorratswerte wie bei `PublicWantedKind` gibt es nicht, weil keine spätere Phase eine zweite Art
    nennt — sie wären toter Code hinter dem Fallback-Arm.
12. **Der Not-Aus stoppt neue Anliegen, er strandet keine laufenden.** `OpenAsync` fragt das Modul als **erstes**
    (ob dieses Konto einreichen dürfte, ist bei geschlossenem Schalter niemandes Sache), `GetOwnDetailAsync` und
    `ReplyAsCitizenAsync` fragen es nie — sonst schlösse ein Schalterdreh Menschen aus einem Gespräch aus, das die
    Behörde selbst begonnen hat (Präzedenz Phase 7).
13. **Der Nav-Eintrag ist der Zugangsschutz, nicht eine zweite Prüfung.** `NavSection.VerwaltungFuehrung` **ist**
    `Policies.LeadershipPage` (`NavSectionPolicy.For`), also ist „ein Junior-Agent sieht nichts davon" eine
    Katalog-Eigenschaft. Das Badge (`BadgeKey: "tickets"`) zählt laufende Tickets ohne Guard — die Zahl steht in
    einem Eintrag, den nur diese Policy überhaupt rendert.
14. **Das Änderungsprotokoll liest jeder interne Agent, Ticketinhalt kommt trotzdem nie dort an.** Der
    `AuditSaveChangesInterceptor` erfasst Feldwerte nur bei `Modified`/`Deleted`, nie bei `Created` — das Anlegen
    eines Tickets und **jede** Nachricht schreiben deshalb eine Zeile ohne `ChangesJson`, und an einem Ticket
    ändern sich später nur Status, Bearbeiter und Zeitstempel. Auf `/nachweis` steht damit „Ticket existiert,
    Status bewegte sich" plus das handelnde Konto: dieselbe bewusste Offenlegung wie bei `Hinweis`, wo sie sogar
    trotz Anonymitätszusage gilt, weil das Konto die Missbrauchskontrolle ist. Die Filterliste dort kommt aus
    `Distinct()` über die Daten, es gibt also keine zweite Registrierung — nur `AuditEntityDisplay` entscheidet
    das Label. **Chronik und Zeitstrahl** zeigen ein Ticket gar nicht: `GlobalChronikService.RecordTypes` ist eine
    geschlossene Liste von zehn Aktenarten, und ein Typ ohne Elternteil fällt beim Anker-Vergleich heraus — das
    ist die Kehrseite von Punkt 1 und der Grund, warum dort nichts registriert werden musste.
15. **`GetCountsAsync` ist bewusst nicht gebaut.** Der Plantext hatte es, `ITipService` hat es seit Phase 7 —
    **ohne einen einzigen Consumer**. Vier Reiter *sind* die Aufteilung; eine Zahl im Reiter-Label veraltete beim
    ersten Statuswechsel, solange die Seite kein Broadcaster-Abo dafür hält. Toter Interface-Platz wird nicht
    kopiert; wenn eine spätere Phase Zähler braucht, sind es fünf Zeilen.

---

## Phase 11 — Öffentliche Vorlagen (4. Token-System) ✅

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

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Klartext, nicht HTML — und das ist der eigentliche Grund, `BewerbungTemplateRenderer` nicht
   wiederzuverwenden.** Beide Zielspalten (`HinweisNachricht.Text`, `TicketNachricht.Text`) sind Klartext und
   werden außen als Text gerendert; ein RichTextEditor verspräche Formatierung, die beim Anwenden immer
   verloren geht. Daraus folgt die konkrete Unverträglichkeit: der Bewerbungs-Renderer `HtmlEncode`t jede
   Ersetzung, weil ein Anschreiben Markup ist — hier hätte das dem Bürger „Müller &amp;amp; Sohn"
   zugestellt. Deshalb `PublicTemplateRenderer` mit eigenem Satz und **ohne** Encoding, und ein Test hält
   genau diese Abweichung fest.
2. **Fünf Arten, jede mit Konsument.** `TicketEingang`, `TicketAntwort`, `HinweisEingang`,
   `HinweisRueckfrage`, `HinweisAblehnung`. Der Plantext nannte sechs: `Belohnungszusage` bräuchte einen
   neuen Schreibpfad in Phase 9 (`RewardService` schickt dem Bürger heute keine Nachricht, nur Glocke und
   Beleg), `Pressemitteilung` baut Phase 14, die den Entwurfs-Pfad ohnehin mitbringt. Präzedenz `TicketArt`
   mit einem Wert: keine Vorratswerte, die nichts setzt. Aus demselben Grund fehlt das Token `BETRAG` —
   ohne Belohnungszusage gäbe es keinen Betrag, und ein nie ersetztes Token geht als Literal nach draußen.
   Statt der einen `Eingangsbestaetigung` zwei Arten: „Ihr Anliegen" und „Ihr Hinweis" sind zwei Texte.
3. **Ein Fallback für `BUERGER`, und der anonyme Hinweis ist der Grund dafür.** `TipDetail.CitizenName` ist
   unter der Zusage `null`; der Renderer bekommt damit baulich nichts, was er ausplaudern könnte, und setzt
   „Bürger/in". Die Auto-Bestätigung eines anonymen Hinweises fragt dafür `TipAnonymity.IsHidden` — die
   Zusage gilt **auch gegen die eigene Bestätigung der Behörde**, nicht nur gegen die Bearbeiter-Projektion.
4. **`NAME` wird geschwärzt, obwohl der Absender außen schon eine Konstante ist.** Die Schwärzung greift auf
   dem *Textkörper*, in den ein Redakteur „Mit freundlichen Grüßen, NAME" schreibt. Sie läuft **zuerst**,
   wie im Bewerber-Pfad: dann kann nichts Eingesetztes nachträglich von ihr getroffen werden (ein Bürger
   namens „NAME Nachname" hat einen Test).
5. **Die Lesepfade tragen bewusst keinen Guard.** Eine Vorlage ist Behörden-Textbaustein, kein Akteninhalt —
   und die Auto-Bestätigung wird gelesen, während ein **Bürger** handelt, nicht ein Agent. Ein Guard hätte
   die Eingangsbestätigung mit `UnauthorizedAccessException` beantwortet. Muster
   `IDocumentTemplateService.GetActiveAsync` und `ITicketService.GetOpenCountAsync`: der Aufrufer ist das
   Gate. Geschrieben wird nur über `Permission.RequirePublicTemplateWrite` (interner Agent + Schreibrecht +
   Führung, Schreibprüfung **vor** der Rangprüfung — Präzedenz Phase 6/9/10).
6. **Tokens bleiben beim Speichern roh; Fremdtokens werden abgewiesen, nicht halb expandiert.** Sie *sind*
   hier die Nutzlast (Hausregel, gilt schon für alle drei bestehenden Systeme). Beim Speichern prüft
   `HasForeignToken` auf `{{…}}`, Erwähnungen, `BEWERBER` und `DIENSTGRAD` — `DATUM`/`UHRZEIT` teilt sich der
   Satz bewusst mit dem Bewerber-Pfad, gleiche Schreibweise, gleiche Bedeutung, und dieser Renderer füllt
   sie. **`MentionParser.Parse` ist dabei der richtige Aufruf**, wie in `WarnhinweisService` und
   `PublicWantedService`: verboten ist das *Auflösen* im öffentlichen Pfad, nicht das Ablehnen.
7. **Die Auto-Bestätigung bewegt keinen Status und läutet nicht.** Ein Ticket bleibt `Offen`, ein Hinweis
   bleibt `Neu` — sonst behauptete der Status Arbeit, die niemand getan hat (Phase-10-Regel).
   `PublicTicketAnswered`/`PublicTipAnswered` sind für eine echte Antwort; der Bürger steht in derselben
   Sekunde auf der Seite, und der Ungelesen-Zähler zeigt die Bestätigung von selbst. Geschrieben wird sie in
   **derselben** Transaktion wie Akte und erste Nachricht: eine Bestätigung ohne Vorgang ist damit baulich
   unmöglich. Die Vorlage wird **vor** der Transaktion gelesen (ein Lesen gehört nicht hinein).
8. **Ohne aktive Vorlage passiert nichts.** Kein Fallback-Text im Code, keine leere Zeile — deshalb ist der
   Aktiv-Schalter auch der Aus-Schalter für die Bestätigung. `PublicTemplateSeeder` legt je Art eine deutsche
   Startvorlage an, aber **nur solange die Tabelle ganz leer ist**: Muster `WarnhinweisSeeder`, nicht
   `PublicModuleSeeder` — ein Modulschlüssel lebt im Code und ist nicht löschbar, eine Vorlage gehört dem
   Betreiber, und per-Art-Seeding würde eine gelöschte bei jedem Neustart wiederbeleben. Die geseedeten Texte
   laufen im Test durch dieselben Prüfungen wie ein handgeschriebener.
9. **`PublicTemplateRules.MaxLength` ist abgeleitet, nicht gesetzt:** `TicketRules.MaxMessageLength` minus
   Reserve für die Ersetzungen. Was gespeichert wird, muss gerendert noch in eine Nachricht passen; zwei
   Zahlen wären Drift (Präzedenz `TipRules.PerDay == TipTrust.DailyQuota(1)`).
10. **Kein `PublicModules`-Schlüssel.** Vorlagen sind interne Konfiguration, keine öffentliche Fläche. Ein
    Modul-Gate hätte die Bestätigung an einen Schalter gehängt, der schon den ganzen Kanal abschaltet.
11. **`FeedbackPageTabs` ist die sechste Registry, die ein neuer `/einstellungen`-Abschnitt braucht.** Der
    Plan zählte fünf (`PublicVisibility`, `SearchCatalog`, `AuditEntityDisplay`, `MergedPageSections`,
    `WatchlistRecordRollup`); rot wurde `FeedbackPageTabsTests`, das jeden `MergedPageSections`-Slug im
    Feedback-Picker verlangt. Kein Papierkorb-Eintrag dagegen: Konfigurationstabelle, Präzedenz
    `Warnhinweis` und `DocumentTemplate` (beide `ISoftDelete` und trotzdem nicht im globalen Papierkorb) —
    der gedachte Weg zum Zurückziehen ist `IstAktiv = false`, und der Dialog sagt es.
12. **Der Datei-Scan streicht Kommentare, bevor er sucht.** Die Regel gilt für Code; die Sätze, die
    *erklären*, warum der Bewerbungs-Renderer nicht wiederverwendet wird, müssen ihn nennen dürfen — sonst
    löscht der nächste Leser den Kommentar, um den Test grün zu machen. Aus demselben Grund steht
    `MentionParser` nicht auf der Liste (siehe 6.), und gesucht wird nach den *Diensten*
    (`IDocumentTemplateService`, `IPlaceholderService`, …) statt nach dem Entitätsnamen `DocumentTemplate`,
    der in `PublicVisibility` als Registrierungs-Schlüssel steht.

---

## Phase 12 — Organisationen & Gefahrenlisten ✅

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

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Nach außen geht die Gefahrenstufe, nie der rohe Score.** Der Plantext („Score live als Zahl") stammt
   von vor Phase 4, die das Gegenteil festgelegt hat: `PublicPageScanTests.InternalMarkers` enthält wörtlich
   `"ThreatScore"`, eine öffentliche Seite, die ihn nennt, macht den Build rot — und die Score-Konfiguration
   steht in `PublicVisibility.NeverPublic` als „Anleitung zur Umgehung". Die Zahl ließe die Formel aus
   `AlgoPlan.md` rückwärts rechnen. Festgehalten wird die Stufe beim Publizieren
   (`HazardLevelLogic.From(faction.ThreatScore)`), aktualisiert nur auf Knopfdruck („Stufe aktualisieren").
2. **Keine Rang-Weiche, kein `Request`.** Anlegen, Bearbeiten und Publizieren ab `SeniorSpecialAgent(3)` —
   dieselbe Schwelle, die eine Ausschreibung publiziert. Bei der Fahndung existiert die Antrags-Weiche nur,
   weil Rang 1–2 dort Entwürfe anlegen darf und sonst nie wieder an sie herankäme; hier gibt es diesen
   Zweig nicht, also auch keinen fünften `RequestType`, keine Spalte auf `Antraege`, keinen
   Posteingangs-Abschnitt und kein Nav-Badge. Folge: **zwei** Guards statt drei — ein
   `RequirePublicFactionProfileRecordRead` hätte niemanden einzulassen.
3. **Zwei Statusachsen, nicht eine.** Der Plantext nannte ein Feld `Status` (`Beobachtet`/`Verboten`).
   Gebaut sind `Status` (`PublicProfileStatus`: Entwurf/Veröffentlicht/Zurückgezogen) und `Einordnung`
   (`PublicFactionStanding`: beobachtet/verboten). Ein Feld für beides könnte eine Publikation nicht
   zurückziehen, ohne die Einordnung zu verlieren — und die Phase-4-Regel „`Status` allein entscheidet die
   öffentliche Sichtbarkeit" wäre nicht mehr wahr.
4. **Die Einordnung ist ein redaktionelles Etikett, nicht `Faction.Classification`.** Sie wird von Hand
   gesetzt und niemals abgeleitet: die interne Einstufung (Prüffall/Verdachtsfall/gesichert
   staatsgefährdend) nach außen zu spiegeln wäre eine Veröffentlichung dessen, woran die Behörde arbeitet.
   Erlaubt ist nur, was in `PublicFactionStandingDisplay.All` steht — `RequireKnownStanding` weist alles
   andere ab, denn ein Wert neben dem Enum stünde draußen als rohe Zahl.
5. **Nur ein Hub, keine Detailseite.** Eine Karte trägt Name, Einordnung, Gefahrenstufe und
   Kurzbeschreibung; eine Detailseite hätte exakt dieselben vier Felder wiederholt. Damit kein neuer
   Aktenzeichen-Präfix, kein `CaseNumberCounter`-Eintrag, und die Ausschreibung verlinkt weiter **nicht**
   auf die Organisation: `OeffentlicheFahndung.FraktionId` ist ein interner FK, und den Namen der Fraktion
   beim Rendern nachzulesen wäre ein Live-Blick in die Akte statt eines Snapshots.
6. **Eigener Cache-Schlüssel, ein Speicherpfad.** `PublicFactionProfileService.SaveAndInvalidateAsync` ist
   die einzige Stelle mit `SaveChangesAsync` **und** `cache.Remove`; ein Dateiscan
   (`PublicFactionProfileCacheDisciplineTests`) hält das fest, samt „kein zweiter Produktionsdateiname kennt
   den Schlüssel" und „wer die Profiltabelle schreibt, ist dieser eine Dienst". Eigener Schlüssel neben dem
   Fahndungs-Snapshot, weil es eine andere Tabelle ist, die von anderen Schreibpfaden ungültig wird — das
   Phase-5-Argument gegen einen zweiten Schlüssel galt Board **und** Archiv aus *derselben* Tabelle.
7. **Der Unterdrückungsgürtel ist wieder eine zweite Abfrage.** `IgnoreQueryFilters()` gilt
   kompilierungsweit, nicht für den Operanden: in einer Unterabfrage benutzt entfernt es den
   Soft-Delete-Filter auch vom äußeren Set — genau so ging in Phase 4 eine gelöschte, veröffentlichte
   Ausschreibung anonym live. `p.Faction` als Navigation ist ebenso unbrauchbar: sie erbt den Filter und ist
   für eine gelöschte Akte `null`, also zeigte `p.Faction == null || …` genau die Zeilen, die es verbergen
   soll. Zusätzlich zieht `RetractForRecordAsync` die Zeile selbst offline, gerufen aus
   `FactionService.RefreshAsync` (sobald die Akte VS wird) und `.DeleteAsync` — der Gürtel ist der Gurt, der
   Hook der Airbag. Einen `FactionMergeService` gibt es nicht, also auch keine dritte Aufrufstelle.
8. **`/gefahr/personen` hat keinen eigenen Lesepfad.** Die Liste projiziert
   `IPublicWantedService.GetBoardAsync().Cards` — dort steht die Gefahrenstufe schon, hinter demselben
   Unterdrückungsgürtel. Eine zweite Abfrage müsste den Gürtel wiederholen, und genau das ist die
   Phase-4-Falle. Damit kein neues Datum, kein neuer Dienst, kein zweiter Cache.
9. **Die Ranking-Regel steht einmal**, in `Services/Public/HazardRanking.cs`: Stufe absteigend,
   Publikationsdatum als Gleichstand-Entscheider, `HazardLevel.No` fällt heraus, gedeckelt auf 25. Zwei
   Oberflächen lesen sie (Organisationen aus dem eigenen Snapshot, Personen aus dem Fahndungs-Board);
   ausgeschrieben würde sie driften, und eine Rangliste, die sich selbst widerspricht, ist schlechter als
   keine. Der **Deckel wird auf der Seite genannt** („die 25 höchsten"), weil ein stiller Schnitt sich wie
   Vollständigkeit liest.
10. **Gates je Datenmenge, verschachtelt.** `/organisationen` hängt an `Organisationen`,
    `/gefahr/fraktionen` an `Gefahrenlisten` **und** `Organisationen`, `/gefahr/personen` an
    `Gefahrenlisten` **und** `Fahndung` — Präzedenz `GetPublishedPhotoAsync`, „das Modul der Menge, in der es
    die Zeile gefunden hat". `PublicModuleGate` nimmt genau ein Modul; zwei verschachtelt zeigen je den
    eigenen Offline-Text, was genau richtig ist: „Die öffentliche Fahndung ist derzeit nicht verfügbar." auf
    der Personenliste sagt, welcher Schalter es war. Zurückziehen und Löschen fragen das Modul **nie**
    (Phase-4-Regel).
11. **Kein Discord-Push, und das ist eine Entscheidung.** Ein Organisationsprofil ist kein Handlungsaufruf,
    und ein Kanal-Post wäre eine bleibende Anschuldigung gegen eine ganze Fraktion, die ein Rückzug nicht
    zurückruft — Präzedenz: eine *Senkung* des Kopfgelds bleibt aus demselben Grund still. Damit auch kein
    neuer `NotificationType`.
12. **`PublicRoutes` und `robots.txt` brauchten nichts.** `/gefahr` steht seit Phase 2 in `ExtraPrefixes`
    („die zweite Gefahrenliste"), `/organisationen` ist eine Modul-Nav-Route und damit schon in `Prefixes` —
    Modul-Routen stehen dort unabhängig von `Available`. Beide Zeilen waren in `robots.txt` vorhanden.
13. **`FeedbackPageTabs` ist auch für einen `/fahndung`-Abschnitt Pflicht.** Der Wächter verlangt jeden
    `MergedPageSections`-Slug im Feedback-Picker, nicht nur die von `/einstellungen` — er wurde rot, bevor
    jemand daran gedacht hatte. Ebenso ordnet `TrashServiceTests` die Papierkorb-Slugs **der Reihenfolge
    nach** gegen `TrashService.Kinds`: die neue Zeile muss dort stehen, wo ihre `Source` steht.
14. **Die Inhaltsprüfung sitzt im Publish-Rumpf, nicht beim Speichern.** Anders als in Phase 4 gibt es hier
    nur *einen* Eingang (keine Genehmigung), also genügt eine Stelle: Klartext vorhanden, keine
    `@{Typ:GUID}`-Erwähnung, kein `{{` (der **bare** Opener, wie `WarnhinweisService`). Ein Entwurf darf eine
    Erwähnung tragen — er ist intern; er lässt sich nur nicht publizieren. Eine *laufende* Publikation wird
    beim Speichern erneut geprüft, sonst ginge eine Erwähnung nachträglich live.
15. **Der Schreibpfad prüft die Aktensichtbarkeit immer**, nicht nur bei laufender Publikation — sonst
    könnte, wer die Id eines Entwurfs kennt, gegen eine Verschlusssache schreiben und eine Audit-Zeile auf
    ihr hinterlassen (Präzedenz Phase 5, `SetHintsAsync`).
16. **Wiederherstellen prüft, ob die Fraktion inzwischen ein anderes Profil hat.** Es gibt keinen
    Unique-Index auf `FraktionId` — mit Soft-Delete würde er die Fraktion für immer sperren (Phase-3-Lektion
    vom Seiten-Slug) —, also ist „ein lebendes Profil je Fraktion" eine Dienst-Regel, und
    Wiederherstellen ist der zweite Weg, sie zu verletzen. Zurück kommt es als **Entwurf**, damit ein
    Rückgängig nichts nebenbei wieder veröffentlicht.
17. **Fünf Registries für den Zeitstrahl, wie gehabt** (`TimelineService.AuditSourceAsync`,
    `TimelineDisplay.MapAudit`, `AuditEntityDisplay` mit Label **und** Route, `ChronikParentResolver` mit
    Map **und** Fan-in) plus `WatchlistRecordRollup` (rollt auf `Faction`: Publizieren ist die
    folgenreichste Änderung an einer Fraktionsakte). Kein zweites `ManualAudit.Row` gegen die Akte — die
    Zeile ist `IAuditable`, und eine zusätzliche, `Faction`-getypte Zeile läse sich als „Akte geändert".

---

## Phase 13a — Sachfahndung ✅

**Ziel:** Ein Kennzeichen oder eine Waffe lässt sich ausschreiben — auf demselben Board, ohne Namen und
ohne Foto.

**Migration:** keine.

**Code**
- `Art = Fahrzeug|Waffe` aus Phase 4 plus die vorhandenen Snapshot-Felder; Entwurf entsteht aus einer
  `PersonVehicle`- oder `PersonWeapon`-Zeile des Steckbriefs.
- `Services/Public/WantedKinds.cs` als einzige Art-Achse; `PublicWantedBoard.WithoutItems()` als das, was
  der Modul-Schalter besitzt.
- Panel `PublicItemNoticePanel` im Abschnitt „Öffentlich" der Personenakte; Board, Steckbrief, Archiv und
  Poster art-bewusst.
- `PublicModules`: `FahndungFahrzeuge` ⇒ `Available`.

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Keine Migration, und das ist ein Fund, keine Sparmaßnahme.** Geplant war eine Quell-Spalte auf die
   `PersonVehicle`-/`PersonWeapon`-Zeile. `PersonService.EditAsync` ersetzt die Steckbrief-Kinder aber
   **vollständig** (`db.PersonVehicles.RemoveRange(person.Vehicles)` + `ChildrenMap`) — jede Id ist nach
   dem nächsten Speichern der Akte eine neue GUID. Ein gespeicherter Verweis wäre danach toter Zeiger, und
   als FK mit `Restrict` hätte er jeden Steckbrief-Edit einer ausgeschriebenen Person **blockiert**. Die
   Quellzeile ist deshalb reine Vorbefüllung: einmal gelesen, nie gespeichert. Genau die Snapshot-Doktrin,
   hier vom Datenmodell erzwungen statt bloß bevorzugt.
2. **„Ohne Personenbezug" gilt nach außen, nicht intern.** `PersonId` bleibt auf der Zeile, weil daran
   Unterdrückungsgürtel, Zeitstrahl, Chronik und `RetractForRecordAsync` hängen — eine als Verschlusssache
   eingestufte Halterin zieht ihr Kennzeichen **ohne eine Zeile neuen Code** offline. Eine trägerlose
   Ausschreibung wäre die einzige öffentliche Zeile, hinter der keine Akte steht, und damit die einzige,
   die kein Gürtel schützt. `PublishAsync` behält den Riegel gegen `PersonId is null`, jetzt als dauerhafte
   Fail-closed-Regel statt als Phasen-Marker.
3. **Kein Foto, in drei Schichten.** Der einzige Fotospeicher im Haus ist `PersonPhotos`, und
   `PhotoSourceSetAsync` löst über `row.PersonId` auf — mit gesetzter `PersonId` **würde** ein Lichtbild der
   Halterin an einem Kennzeichen auflösen. Also: `GetOptionsAsync` bietet keine an, `UpdateSnapshotAsync`
   **weist** ein `PhotoSourceId` **ab** statt es still auf `null` zu setzen (Riegel gegen einen
   manipulierten Dialog-Post, Präzedenz `SetHintsAsync`), und `PhotoCopyAsync` räumt bedingungslos.
4. **Der Vorwurf wird bewusst nicht vorbefüllt.** `Person.WantedReason` ist ein Vorwurf *gegen die Person*
   und nennt sie im Freitext meist beim Namen — auf einer Kennzeichen-Karte wäre das genau der Bezug, den
   die Phase verspricht nicht zu veröffentlichen. `RequirePublishableContent` verlangt den Text ohnehin,
   der Autor schreibt ihn also, bevor irgendetwas live geht.
5. **Ein Unterschalter, ein Snapshot.** `FahndungFahrzeuge` ist ein *Unter*schalter von `Fahndung`:
   `/gesucht` hängt am Board-Modul, aus ⇒ alles dunkel; nur die Sachfahndung aus ⇒ die Kennzeichen fallen,
   die Personen bleiben. Umgesetzt über `PublicWantedBoard.WithoutItems()`, das Karten, Steckbriefe,
   Archiv **und** Kopfgeld-Wörterbuch in einem Zug räumt — kein zweiter Cache-Schlüssel, aus demselben
   Grund, aus dem Board und Archiv sich schon einen teilen. `GetByCaseNumberAsync` und `GetBountyAsync`
   gehen darüber und brauchten deshalb keine Änderung; `GetPublishedPhotoAsync` bekam das Gate trotzdem
   ausdrücklich, weil ein Endpoint sich nicht auf eine Regel verlassen darf, die in einer anderen Datei
   steht.
6. **Kein eigener Nav-Tab.** Die `art=`-Chips auf `/gesucht` existieren seit Phase 4 und erzeugen sich aus
   den Arten, die tatsächlich auf dem Board liegen — ein Tab auf `/gesucht?art=…` wäre eine zweite Wahrheit
   über dieselbe Seite. `NavRoute` bleibt `null`, Muster `Kopfgeld`/`Fahndungsposter`.
7. **Der Personen-Pfad wurde art-eng gezogen.** „Eine Ausschreibung je Akte" galt bis dahin für *jede* Art;
   ohne die Einschränkung auf `WantedKinds.PersonRows` hätte ein ausgeschriebenes Kennzeichen die
   Personenfahndung derselben Akte gesperrt. Aus demselben Grund ignorieren `GetForPersonAsync` und
   `GetBannerForPersonAsync` Sach-Zeilen: der rote Banner behauptet, diese **Person** sei öffentlich
   ausgeschrieben, was ein Kennzeichen nicht wahr macht.
8. **Dedupliziert wird auf dem Text, nicht auf der Quelle** (Folge von 1): keine zweite lebende Zeile
   derselben Art an derselben Akte mit demselben Anzeigenamen. Das Kennzeichen ist ohnehin das, was die
   Ausschreibung draußen benennt.
9. **Keine Registry-Runde.** Es entsteht keine neue Entität, also kein `AuditEntityDisplay`, kein
   `WatchlistRecordRollup`, kein Papierkorb, kein Zeitstrahl-Eintrag, kein `SearchCatalog`, keine
   `MergedPageSections`, keine Route, kein Aktenzeichen-Präfix, kein `NotificationType`. Geändert wurden
   genau zwei Zeilen: der `Available`-Schalter und der `PublicVisibility`-Text der Ausschreibung.
10. **Publizieren gatet auf beide Schalter.** `RequireModulesAsync(kind)` verlangt immer `Fahndung`, für
    eine Sach-Art zusätzlich `FahndungFahrzeuge`. Ohne das ginge ein Kennzeichen bei ausgeschaltetem
    Sach-Modul auf `Veroeffentlicht`, der Lesepfad striche es weg, und der nicht zurückrufbare
    Discord-Post verlinkte auf eine 404. Das Gate sitzt deshalb **hinter** dem Laden der Zeile — welches
    Modul greift, hängt an der Art —, während der Schreib-Guard davor bleibt. *De*publizieren gatet nie.
11. **Die Gefahrenstufe kommt weiter aus dem Score der Akte.** Sie ist die nach außen zulässige Form des
    Werts und sagt an einem Kennzeichen, wie gefährlich die Annäherung ist; `HazardLevel.No` auf jeder
    Sach-Karte wäre die schlechtere Aussage. In `/gefahr/personen` taucht sie trotzdem nicht auf — die
    Liste filtert seit Phase 12 auf `Kind == Fahndung`, und dieser Filter wird jetzt erst scharf.
12. **Im Archiv heißt es „Sichergestellt".** Ein Fahrzeug wird nicht gefasst. Route und Überschrift behalten
    das gemeinsame Wort, die Karte nicht.

---

## Phase 13b — Einspruch gegen eine Ausschreibung ✅

**Ziel:** Ein Bürger kann einer Ausschreibung widersprechen, die Führung entscheidet.

**Daten** (`Oeffentlich13_Einspruch`)
- `FahndungEinspruch` → `FahndungEinsprueche`: `Aktenzeichen` (Präfix `EIN`), `FahndungId`,
  `BuergerProfilId`, `Text`, `Status` (`ObjectionStatus`), `Entscheidungsnotiz`, `EntschiedenVonId`/`Am`,
  `VorgangId`, `IAuditable`, `ISoftDelete`.

**Code**
- `IObjectionService`: einreichen (Bürger, zwei Deckel), lesen (eigene Liste / Abschnitt), entscheiden
  (Führung), Vorgang anlegen, Papierkorb.
- `ObjectionRules` als einzige Wahrheit über Längen, Kontingent und erlaubte Statuswechsel.
- Seiten: `/buerger/einspruch` (Bürger) und Abschnitt `/fahndung?tab=einsprueche` (Behörde).
- `PublicModules`: `Einspruch` ⇒ `Available`.

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Stattgeben setzt voraus, dass die Ausschreibung schon offline ist.** Wörtlich das Phase-9-Muster
   („`Gefasst` ist Vorbedingung, keine Nebenwirkung"): `RequireNoticeOfflineAsync` weist ab, solange der
   Status in `PublicWantedService.PubliclyVisible` steht — `Gefasst` eingeschlossen, denn eine gefasste
   Ausschreibung steht weiter draußen, im Archiv. Der Mensch zieht sie also zuerst mit einem echten Grund
   zurück, und ein Grund, den ein Mensch gewählt hat, ist besser als einer, den dieser Dienst erfunden
   hätte. Nebeneffekt, der genauso wichtig ist: die Fahndungstabelle behält ihren **einen** Schreibpfad.
   Die Statusmenge wird **benannt**, nicht kopiert — `PubliclyVisible` ist dafür von `private` auf
   `internal` gehoben, Präzedenz `RequirePublishableRecordAsync`.
2. **Der Einspruch hängt an der Ausschreibung, nicht an der Akte.** Er bestreitet, was die Behörde
   *veröffentlicht* hat, und der Snapshot ist das Einzige, was der Bürger je gesehen hat. Zeitstrahl und
   Chronik gehen deshalb über **zwei** Hops (Einspruch → Ausschreibung → Akte), gestaffelt wie beim
   Kopfgeld-Anteil.
3. **Kein Nachrichten-Thread.** Die Behörde antwortet genau einmal, in `Entscheidungsnotiz`, und der Bürger
   liest sie zusammen mit dem Status. Alles Längere ist ein Ticket — dafür gibt es Phase 10. Eine
   Entscheidung **ohne** Begründung wird abgewiesen: der Text ist das, was der Bürger bekommt.
4. **Zwei Deckel, nur einer ignoriert den Soft-Delete.** `PerDay = 3` zählt mit `IgnoreQueryFilters` —
   Löschen gibt das Tageskontingent nicht zurück; „ein offener Einspruch je Ausschreibung und Konto" zählt
   lebende Zeilen, also gibt eine Entscheidung die Ausschreibung für einen neuen Einspruch frei. Beide im
   Dienst, nicht in der Middleware: die Einreichung läuft über SignalR.
5. **Die Ausschreibung wird über das Aktenzeichen aufgelöst, nie über eine Id von außen** — durch
   `IPublicWantedService.GetByCaseNumberAsync`, also hinter dem Unterdrückungsgürtel. Ein Entwurf und eine
   zurückgezogene Zeile lesen sich damit wortgleich als „gibt es nicht" (Präzedenz `TipService`).
6. **Der Schalter weitet über den Soft-Delete-Filter, und zwar aus einem schärferen Grund als die Bürgerliste.**
   Die Projektion dereferenziert die Pflicht-Navigation `Wanted`, EF joint sie deshalb **INNER** — mit
   aktivem Filter fällt ein Einspruch, dessen Ausschreibung gelöscht wurde, komplett aus `GetListAsync`
   heraus, während `GetCountsAsync` ihn weiterzählt, weil es keine Navigation berührt. Ergebnis wäre ein
   offener Einspruch, den niemand findet, neben einem Reiter, der auf seiner Existenz besteht — genau die
   Klasse, gegen die der Veröffentlichungs-Posteingang schon einen Wächter hat (nachgemessen, nicht
   vermutet). `GetListAsync`, `GetCountsAsync` und `GetAsync` lesen deshalb dieselbe Menge:
   `IgnoreQueryFilters()` an der Wurzel, `!IsDeleted` von Hand zurück.
7. **`GetOwnAsync` weitet aus dem verwandten Grund** und schreibt `!IsDeleted` ebenso wieder hin:
   `IgnoreQueryFilters` gilt kompilierungsweit und hebt den Filter damit auch von `Wanted` — hier gewollt,
   denn der Bürger muss weiter lesen können, **wogegen** er Einspruch erhoben hat, auch nach Rückzug oder
   Löschung. Sein eigener gelöschter Einspruch bleibt trotzdem verborgen.
8. **Wiederherstellen prüft die Invariante nach.** „Ein offener Einspruch je Ausschreibung und Konto" ist
   eine Dienst-Regel ohne Index dahinter, und der Papierkorb ist ihre zweite Tür: der Bürger darf nach dem
   Löschen einen neuen einlegen, ein Zurückholen des alten ergäbe zwei offene. Geprüft wird nur für
   **offene** Zeilen — eine entschiedene belegt nichts und gehört als Historie zurück in die Akte
   (Präzedenz Phase 3/12: „Wiederherstellen ist der zweite Weg, die Regel zu verletzen").
9. **Der Vorgang wird gerufen, nicht gebaut** (Muster `TipTakeoverService`): `ICaseService.CreateAsync`
   behält Aktenzeichen-Transaktion, Einstufungs-Gate und Audit-Zeile. Die Zuordnung selbst ist ein
   **Compare-and-swap** über `ExecuteUpdateAsync` (Muster `PayInAsync`) — zwei Tabs würden sonst zwei
   Vorgänge anlegen und der letzte Schreiber gewinnt, einer bliebe verwaist zurück. Der Verlierer verwirft
   seinen Vorgang. `ExecuteUpdate` umgeht den Interceptor ⇒ `ManualAudit.Row` von Hand.
10. **Zwei Guards mit unterschiedlicher Breite.** `RequireObjectionRead` ist die Menge, die auch die
   Ausschreibungsliste arbeitet (Rang ≥ 3 oder Aufsicht) — wer eine Ausschreibung veröffentlicht hat, muss
   sehen können, dass ihr widersprochen wird; ein Einspruch ist Fahndungsarbeit, keine
   Führungs-Korrespondenz. `RequireObjectionHandling` legt Schreibrecht **und** Führung darüber, in dieser
   Reihenfolge: `RequireLeadership` allein ließe Nur-Lese-Aufsicht und Demo-Principal bis zum Prägen einer
   Vorgangsnummer laufen.
11. **Ein Bürger-Aktenzeichen mit einem Zweck.** `EIN` existiert, weil eine Entscheidung zitierbar sein
   muss — wie Hinweis, Ticket und Beleg. Der Schalter adressiert über die Zeilen-Id, der Bürger über das
   Aktenzeichen; eine rohe Id von außen wäre ein Existenz-Orakel.
12. **Beide Benachrichtigungen sind nicht routbar.** `PublicObjectionReceived` nennt einen Bürger, der eine
    öffentliche Anschuldigung bestreitet, `PublicObjectionDecided` ist an genau einen Bürger adressiert —
    im öffentlichen Discord-Kanal hätte beides nichts zu suchen.
13. **Keine Vorlage.** Phase 11 gilt „eine Art, ein Konsument": es gibt keinen Thread, in den eine
    automatische Bestätigung geschrieben werden könnte, und der Status ist sofort sichtbar.
14. **Die anonyme Variante der Bürgerseite ist Absicht.** `/buerger/einspruch` wird von einem öffentlichen
    Steckbrief aus verlinkt, also landet dort ein nicht angemeldeter Besucher; eine Umleitung auf die
    Startseite hätte ausgesehen wie ein kaputter Link. Stattdessen der Discord-Login mit `returnUrl`,
    Muster `TipForm`.
15. **Registriert** in `PublicVisibility` (`NeverPublic`), `SearchCatalog` (`NotSearchable`, Provider mit
    Phase 16), `AuditEntityDisplay` (Label **und** Route), `MergedPageSections` (`Wanted` **und** `Trash`),
    `FeedbackPageTabs`, `WatchlistRecordRollup` („not watchable" — zwei Hops, und die Map hat keine
    Datenbank), `TrashService`/`TrashProjection` (nennt weder Bürger noch Text) und den drei
    Zeitstrahl-/Chronik-Stellen. `GetOpenCountForNoticeAsync` wurde **wieder entfernt**, weil kein Aufrufer
    sie brauchte, ebenso `ObjectionRow.DecidedByCodename` und `ObjectionDetail.WantedId` — was nichts
    rendert, ist der Vorratswert, den die Hausregeln ablehnen, und eine Identität, die niemand anzeigt, hat
    auf einer Listenzeile nichts zu suchen. `PublicSurfaceGuardTests.DeskInternals` kennt jetzt
    `ObjectionRow`, `ObjectionDetail` und `DecidedByCodename`: die Liste ist handgepflegt, also wusste sie
    von der neuen Phase nichts, und der Wächter existiert gerade dafür, dass keine Bürgerseite an die
    Schalter-Projektion greift.

---

## Phase 14a — Presse ✅

**Ziel:** Die Behörde spricht selbst — datiert, zitierbar, mit einem Entwurfsschritt davor.

**Daten** (`Oeffentlich14_Presse`)
- `Pressemitteilung` → `Pressemitteilungen`: `Aktenzeichen` (nullable, unique, Präfix `PM`), `Titel`,
  `Teaser` (Klartext), `InhaltHtml`/`EntwurfHtml` (`longtext`), `Status`, `VeroeffentlichtAm`/`VonId`,
  `DiscordGepushtAm`, `IAuditable`, `ISoftDelete`.

**Code**
- `IPressReleaseService` nach dem Vorbild `PublicPageService`: zwei HTML-Spalten mit je einer Bedeutung,
  ein Speicherpfad, ein Cache-Schlüssel.
- `Services/Public/PressDraftText.cs` — festes Skelett des Auto-Entwurfs, HTML-encodend.
- Seiten `/presse` und `/presse/{Aktenzeichen}`, `PressPanel` in `/einstellungen?tab=presse`.
- Discord-Push beim Veröffentlichen, genau einmal je Mitteilung.
- `PublicModules`: `Presse` ⇒ `Available`.

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Ein Aktenzeichen statt eines Slugs, und ein Entwurf hat deshalb gar keine Adresse.** Der Slug aus
   Phase 3 trägt die Lektion, dass er **nicht** unique indexiert werden darf — eine soft-gelöschte Zeile
   behält ihre Adresse. Eine Zählernummer wird dagegen nie wiederverwendet, also darf `Aktenzeichen`
   unique sein, und `RestoreAsync` braucht keine Adressprüfung. Geprägt wird es bei der **ersten**
   Publikation (Präzedenz Phase 4): ein Entwurf ist damit baulich nicht erreichbar, nicht bloß von einem
   Status versteckt. Präfix **`PM`**, geprüft gegen die 22 vergebenen.
2. **Titel und Teaser sind mitgeschnappt, anders als bei einer redaktionellen Seite.** `OeffentlicheSeite`
   hält nur den Rumpf in zwei Spalten und liest den Titel live — dort ist das folgenlos. Eine
   Pressemitteilung ist eine **datierte Aussage**: wer später einen Tippfehler im Text korrigiert und dabei
   die Schlagzeile anfasst, hätte mit „Entwurf speichern" die Überschrift geändert, die längst draußen ist,
   ohne einen Publizieren-Klick. Deshalb `InhaltTitel`/`InhaltTeaser` neben `InhaltHtml`, alle drei nur von
   `PublishAsync` geschrieben — und `DraftDiffers` vergleicht alle drei, sonst nennt das Panel eine veraltete
   Schlagzeile aktuell. (Beim Seiten-Titel ist derselbe Effekt vorhanden und bleibt für 14b/14c zu prüfen.)
3. **Der Auto-Entwurf kommt aus einem festen Skelett im Code, nicht aus einer Phase-11-Vorlage.** Der
   4. Token-Satz ist für **Klartext-Bürgernachrichten** gebaut, und die Fehlpassungen sind nicht kosmetisch:
   `PublicTemplateRenderer` encodet bewusst nichts (eine Pressemitteilung ist HTML), schwärzt `NAME` zu
   `███████` (eine Festnahmemeldung will den Namen *zeigen*), lässt `BUERGER` auf „Bürger/in" zurückfallen
   (es gibt keinen Bürger) und leitet `MaxLength` aus `TicketRules.MaxMessageLength` ab. Ein neuer Token im
   geteilten Renderer ginge in **jeder** Bürgervorlage unexpandiert nach draußen. `PressDraftText` encodet
   je Einsetzung mit `WebUtility.HtmlEncode` — dasselbe, was der Bewerbungs-Renderer tut — und wickelt jede
   Zeile in ein `<p>`; das ist die eine Grenze, an der Text zu Markup wird.
4. **`DiscordGepushtAm` ist der Idempotenz-Token, und hier gibt es keinen Statuswechsel, der die Rolle
   übernehmen könnte.** Beim Ablauf-Worker und bei der Belohnung ist der Statuswechsel selbst der Token;
   Zurückziehen → Korrigieren → Wiederveröffentlichen ist dagegen ein legitimer Rundgang, der den Kanal
   nicht ein zweites Mal beschicken darf. Der Push sitzt nach dem Commit (eine Discord-Nachricht lässt sich
   nicht zurückrufen) und nimmt **nur** einen `PublicPressCard` — dieser Record kann keinen Autor, keinen
   Aktenbezug und keine interne Id tragen, also die Nachricht auch nicht.
5. **Erste routbare öffentliche Kategorie ohne Vorbehalt.** `PublicWantedPublished` und
   `PublicWantedBountyRaised` sind routbar, alles aus Hinweisen, Tickets und Einsprüchen bewusst nicht —
   dort nennt die Meldung einen Bürger. Eine Pressemitteilung ist eine amtliche Verlautbarung, nennt keinen
   Bürger, und ihr Link ist eine dauerhafte öffentliche Adresse.
6. **Der Auto-Entwurf hängt an der gespeicherten Modul-Wahl, nicht an `RequireEnabledAsync`.** Letzteres
   faltet den Not-Aus ein, und der ist eine vorübergehende Störung der öffentlichen Seiten — kein Grund,
   einen internen Entwurf zu verlieren (Präzedenz: der Beleg-Lesepfad aus Phase 9 fragt aus demselben Grund
   die Wahl allein). Mit dauerhaft **ausgeschaltetem** Modul entsteht dagegen gar kein Entwurf: er
   existiert, um veröffentlicht zu werden, und einer je Festnahme, den niemand veröffentlichen kann, ist
   Rauschen und kein Sicherheitsnetz. Der Aufruf steht nach dem Commit in `CapturedAsync` und in `try/catch`
   — eine ausgefallene Bequemlichkeit darf keine Festnahme zurücknehmen.
7. **Der Auto-Entwurf wird von der Autorität bewacht, die die Ausschreibung schließt — nicht von
   `RequirePressWrite`.** Der erste Anlauf hing ihn an die Führung, und das war falsch:
   `RequirePublicWantedWrite` hat **keine Rangschwelle**, jeder schreibberechtigte Agent darf „gefasst"
   setzen. Der Hook wird bewusst geschluckt (`try/catch`), also hätte die Führungsprüfung den versprochenen
   Automatismus für jede Festnahme unterhalb Rang 4 **lautlos** ausfallen lassen — genau das
   „Fertig, wenn"-Kriterium der Phase. Der Entwurf bleibt intern und nur die Führung kann ihn
   veröffentlichen; das ist die Absicht, nicht ein Nebeneffekt. In der Prüfung mit einem Rang-2-Akteur
   nachgewiesen, nicht vermutet.
8. **Keine Vorschau-Route, und das ist die Folge von Punkt 1.** Eine redaktionelle Seite hat ihren Slug von
   Anfang an, `?vorschau=1` kostet dort nichts. Für eine Pressemitteilung wäre es die **einzige**
   öffentliche Route, die für eine unveröffentlichte Zeile antwortet. Der Editor rendert denselben Stand,
   der veröffentlicht würde; das Panel sagt es dazu.
9. **Zurückziehen behält Inhalt *und* Nummer**, Löschen erst danach. Die Sichtbarkeit hängt allein am
   `Status`, Wiederveröffentlichen ist ein Klick auf derselben Adresse, und Löschen einer laufenden
   Mitteilung wäre eine stille Depublikation ohne Grund. Wiederherstellen kommt als **Entwurf** zurück.
   Zurückziehen und Löschen fragen das Modul nie.
10. **`RequirePressWrite` ist ein eigener Guard**, Reihenfolge `IsInternalAgent()` → `MayWrite()` →
   `IsLeadership()`: ein angemeldetes Bürgerkonto trägt überhaupt keinen Rang-Claim, und die Rangprüfung
   allein ließe Nur-Lese-Aufsicht und Demo-Principal bis zum Prägen einer Nummer laufen. Gelesen wird mit
   `RequireClassifiedRead` — die Aufsicht muss sehen, was die Behörde öffentlich sagt, sie darf es nur nicht
   sagen. Rang 3 publiziert Ausschreibungen, aber die Stimme der Behörde bleibt bei der Führung; eine
   Antrags-Weiche wie in Phase 4 gibt es deshalb nicht.
11. **Nebenbefund, hier mitgenommen: `/lageberichte` war als indexierbar deklariert.** Intern liegen dort
    `LegacyRouteRedirect` und die führungs-only Druckseite `/lageberichte/{Id}` („contains classified
    aggregates"), gleichzeitig nannte `PublicModules.SituationReports` die Route als `NavRoute` — und
    `PublicRoutes.Prefixes` sammelt die Nav-Routen **ohne** `Available`-Filter ein. Damit stand
    `Allow: /lageberichte` in `robots.txt`, der `noindex`-Header fehlte, `DemoModeMiddleware` schloss die
    Route aus und `PartnerRoutes.IsAllowed` gab `true`. Kein Datenleck (die Seite trägt
    `Policies.LeadershipPage`), aber eine Falschaussage über eine interne Seite. Die öffentliche Route heißt
    jetzt `/berichte`; das Label bleibt „Lageberichte", die internen Seiten bleiben unangetastet.
12. **Registriert** in `PublicVisibility` (`Publishable` — was genau rausgeht), `SearchCatalog`
    (`NotSearchable`, Provider mit Phase 16), `AuditEntityDisplay` (Label **und** Route),
    `WatchlistRecordRollup` („not watchable" — außendarstellender Text ohne Aktenbezug),
    `TrashService`/`TrashProjection` (die Zeile nennt Titel und Status, nie den Rumpf — er kann Bilder
    tragen), `MergedPageSections` (`Settings` **und** `Trash`), `FeedbackPageTabs`, `NotificationType` +
    `DiscordRouting` + `DiscordWebhookService`. **Nicht** registriert und mit Grund: die vier
    Zeitstrahl-/Chronik-Stellen und `RecordsReference`/`LinkService` — eine Pressemitteilung hängt an keiner
    Akte, es gibt keinen Elternteil, auf den sie fan-in könnte (Präzedenz `Ticket`).
13. **Neuer Wächter `PressCacheDisciplineTests`**, dritter seiner Art: ein `SaveChangesAsync(`, ein
    `cache.Remove(`, ein `cache.Set(`, kein zweiter Produktionsdateiname kennt den Schlüssel, und wer
    `db.Pressemitteilungen` schreibt, ist dieser eine Dienst. Der eigene Schlüssel neben Fahndung und
    Organisationsprofilen ist Absicht: andere Tabelle, andere Schreibpfade.
14. **`PublicModuleServiceTests.NavEntries_ExcludeAModuleWhosePagesDoNotExistYet` nimmt sein Beispiel jetzt
    aus dem Katalog** statt es zu nennen. Der Test hing an `Presse` als „noch nicht gebaut" — jedes benannte
    Beispiel wird irgendwann gebaut, und `First` wird laut rot, wenn keins mehr übrig ist.

---

## Phase 14b — Warnungen & Gesetzesauszüge ✅

**Ziel:** Kurze amtliche Aussagen: eine Warnung mit Gültigkeitsdatum, und die Gesetze, die nach außen dürfen.

**Daten** (`Oeffentlich14_Warnungen`)
- `OeffentlicheWarnung` → `OeffentlicheWarnungen`: `Titel`, `EntwurfHtml`/`InhaltTitel`/`InhaltHtml`
  (`longtext`), `GueltigBis`, `Status`, `VeroeffentlichtAm`/`VonId`, `IAuditable`, `ISoftDelete`.
- `Law`: `IstOeffentlich` (bool, indiziert).

**Code**
- `IPublicWarningService` nach dem Vorbild `PressReleaseService`: zwei Spalten mit je einer Bedeutung,
  ein Speicherpfad, ein Cache-Schlüssel.
- `IPublicLawService` — eigener Dienst, obwohl dünn; er liest die Tabelle selbst.
- `Services/Public/PublicExpiry.cs` — die Ablaufregel, aus `PublicWantedService` herausgezogen.
- Seiten `/warnungen` und `/recht`, `PublicWarningsPanel` und `PublicLawPanel` in `/einstellungen`.
- `PublicModules`: `Warnungen`, `Recht` ⇒ `Available`.

### Gebaut — was daran anders ist als am Rest des öffentlichen Bereichs

1. **Ein Hub, keine Detailseite, kein Aktenzeichen.** Präzedenz Phase 12: eine Warnung besteht aus Titel
   und Text, eine Detailseite würde genau diese zwei Felder wiederholen. Die ganze Karte trägt den Rumpf,
   also fällt der Aktenzeichen-Präfix weg, der `CaseNumberCounter`, die Nichtgefunden-Route und ihr
   `noindex`. Gedeckelt auf 20 (nicht 50 wie die Presse) — jede Karte trägt ihren Rumpf mit, und eine
   stehende Warnung, zu der niemand herunterscrollt, ist eine Warnung, die nicht gewirkt hat. **Der
   Deckel wird auf der Seite genannt**, ein stiller Schnitt liest sich wie Vollständigkeit.
2. **Titel und Rumpf sind mitgeschnappt, das Gültigkeitsdatum bewusst nicht.** Die 14a-Lektion gilt
   unverändert: neben einem Publizieren-Knopf darf „Entwurf speichern" nichts ändern, was schon draußen
   steht. `GueltigBis` ist die begründete Ausnahme und hat **keine** zweite Spalte — eine Warnung zu
   verlängern ist keine neue Aussage, und ein Republizieren-Zwang ließe sie sterben, weil niemand den
   zweiten Knopf drückt. Präzedenz ist das live gelesene Warnhinweis-Label aus Phase 5.
3. **Der Filter ist die Kontrolle, nicht ein Worker** — und anders als bei der Fahndung gibt es hier
   gar keinen. Der Ablauf-Worker aus Phase 5 existiert, damit der *interne* Status ehrlich bleibt und
   offene Anträge geschlossen werden; eine Warnung hat weder das eine noch das andere. Der Preis ist
   sichtbar und akzeptiert: eine abgelaufene Warnung steht bis zu ein Cache-Fenster (10 s) zu lange, was
   ein in Tagen gemessener Ablauf nicht merkt, und ihr Status sagt weiter „veröffentlicht". Das Panel
   sagt „abgelaufen" dazu, statt sie live aussehen zu lassen. Löschen verlangt trotzdem erst das
   Zurückziehen — der Status ist die Aussage, nicht der Filter.
4. **Publizieren mit einem Ablaufdatum in der Vergangenheit wird abgewiesen.** Der Lesepfad filtert auf
   dasselbe Feld, die Aktion meldete also Erfolg und änderte nichts, was jemand sehen kann.
5. **`PublicExpiry` statt einer zweiten Kopie.** „Ein gewählter Tag zählt noch mit" stand privat in
   `PublicWantedService`; wörtlich abgeschrieben wären es zwei Regeln, und eine Warnung, die mittags
   stirbt, liest sich wie ein Fehler statt wie eine Entscheidung. Herausgezogen nach
   `Services/Public/PublicExpiry.cs`, Muster `BountyShares`/`TipAnonymity`.
6. **Kein Discord-Push, und das ist die Umkehrung von 14a.** Eine Pressemitteilung ist die erste
   routbare öffentliche Kategorie ohne Vorbehalt, weil ihr Link eine dauerhafte Adresse ist. Eine
   Warnung läuft ab — der Kanal-Post behauptete danach weiter Gefahr, unkorrigierbar, genau das
   Argument, das `PublicWantedExpired` von der Routing-Allowlist fernhält. Damit kein neuer
   `NotificationType`.
7. **`OeffentlicheWarnung` ist nicht `Warnhinweis`.** Zwei Tabellen, zwei Bedeutungen: die eine ist eine
   Durchsage mit Rumpf und Ablauf, die andere die Chip-Werteliste an einer Ausschreibung aus Phase 5.
   Sie stehen als Nachbarn in `/einstellungen` („Öffentliche Warnungen" neben „Warnhinweise"), und beide
   Panel-Texte sagen, was sie nicht sind. Der Slug ist `warnungen`, der alte bleibt `warnhinweise`.
8. **`/recht` bekommt einen eigenen Dienst, obwohl er dünn ist.** Leitsatz 3: ein öffentlicher Lesepfad
   liest nie über einen internen Listendienst. `LawService.GetListAsync` beantwortet eine andere Frage
   (inklusive Partner-Achse) und darf sich jederzeit weiten, ohne dass jemand an die Öffentlichkeit
   denkt. `PublicLawService` liest `db.Laws` selbst, gefiltert auf `IstOeffentlich`.
9. **Die Gesetzestabelle ist die erste öffentliche Datenquelle mit einem zweiten Schreiber.** Bei
   Fahndung, Organisationsprofilen und Presse lautet die Regel „ein Dienst schreibt die Tabelle";
   `Gesetze` gehört dagegen `ILawService`, das den Text pflegt. Die Regel heißt hier deshalb **jeder
   Schreiber verwirft den Snapshot**: `LawService` ruft nach `CreateAsync`, `RefreshAsync` und
   `DeleteAsync` `InvalidatePublicViewAsync()` — sonst stünde ein korrigierter oder gelöschter Paragraf
   ein volles Cache-Fenster lang draußen. Auch nach `CreateAsync`, wo es beweisbar unnötig ist: „dieser
   Paragraf kann nicht öffentlich sein" ist eine Aussage über heute. Kein DI-Zyklus, `PublicLawService`
   kennt `ILawService` nicht.
10. **`PublicLawCacheDisciplineTests` ist deshalb anders gebaut als seine drei Geschwister.** Ein Scan
    „wer die Tabelle nennt und speichert, ist der eine Dienst" wäre hier dreifach falsch-rot:
    `SearchIndexBackfillWorker`, `LinkService` und `PartnerShareService` **lesen** Paragrafen und
    schreiben anderes. Sie stehen deshalb als **Leser mit Begründung** in einer Liste, die Schreiber in
    einer zweiten — Muster `PublicVisibility`. Eine neue Datei, die `db.Laws` und `SaveChangesAsync`
    nennt, macht den Build rot, bis jemand entschieden hat, was sie ist.
11. **Kein Deckel auf `/recht`**, anders als auf jedem anderen öffentlichen Hub. Ein Gesetzbuch soll
    vollständig sein; ein „gezeigt werden die 50 jüngsten Paragrafen" wäre bei Recht eine falsche
    Aussage, keine Bequemlichkeit. Gruppiert wird nach Gesetzbuch, sortiert nach Paragraf.
12. **Der Gesetzestext geht als Klartext nach draußen, nie als `MarkupString`.** `Law.Text` ist
    Klartext — Zeilenumbrüche sind seine Formatierung, also `white-space: pre-wrap` und Blazors
    Encoding. Präzedenz `OeffentlicheVorlage.Text` aus Phase 11. Der Warnungs-Rumpf ist dagegen HTML
    und läuft wie überall über `HtmlCleanup.Clean` beim Speichern **und** beim Publizieren, gerendert
    als rohes `MarkupString` (nie `RichHtml`, das löste `@{Typ:GUID}` auf).
13. **Freigeben braucht ein lebendes Modul, Zurückziehen nie** — dieselbe Asymmetrie wie beim
    Publizieren einer Mitteilung, damit der Not-Aus das Zurücknehmen nicht unmöglich macht.
14. **Zwei eigene Guards mit heute identischer Bedingung.** `RequireWarningWrite` und
    `RequireLawReleaseWrite` sind beide `IsInternalAgent()` → `MayWrite()` → `IsLeadership()`, in dieser
    Reihenfolge (ein Bürgerkonto trägt gar keinen Rang-Claim, und die Rangprüfung allein ließe Aufsicht
    und Demo-Principal durch). Getrennt, weil die Meldung den Bereich nennt und zwei Bereiche, die
    heute dieselben Leute einlassen, es morgen nicht müssen. Gelesen wird mit `RequireClassifiedRead`.
15. **Registriert:** `AppDbContext` (DbSet, Fluent, `IstOeffentlich`-Index), `PublicVisibility` — dort
    wandert **`Law` von `NeverPublic` nach `Publishable`**, was die eigentliche Aussage der Phase ist —,
    `SearchCatalog` (`NotSearchable`, Provider mit Phase 16), `AuditEntityDisplay` (Label **und**
    Route), `WatchlistRecordRollup` („not watchable"), `TrashService`/`TrashProjection` (die Zeile nennt
    Titel und Status, nie den Rumpf), `MergedPageSections` (`Settings` **und** `Trash`),
    `FeedbackPageTabs`, `Program.cs`, `Settings.razor`. **Nicht** registriert und mit Grund: die vier
    Zeitstrahl-/Chronik-Stellen und `RecordsReference`/`LinkService` — eine Warnung hängt an keiner
    Akte (Präzedenz `Ticket`, `Pressemitteilung`); `PublicRoutes`/`robots.txt` — `/warnungen` und
    `/recht` kommen seit Phase 2 aus der Modul-`NavRoute`; `NotificationType` — siehe Punkt 6.

**Tests** (+50) — `PublicWarningServiceTests` (Entwurf anonym unerreichbar · Speichern lässt die
Publikation in Ruhe · Ablauf fällt aus dem Lesepfad, Status bleibt · gewählter Tag zählt noch mit ·
Vergangenheitsdatum nicht publizierbar · leerer Entwurf abgelehnt, Nur-Bild akzeptiert · Zurückziehen
ohne Modul · Rechte-Matrix) · `PublicLawServiceTests` (nichts ist öffentlich, bis jemand es sagt ·
Gruppierung · Löschen und Korrigieren wirken sofort · Freigeben braucht das Modul, Zurücknehmen nicht ·
Rechte-Matrix) · `PublicWarningCacheDisciplineTests` · `PublicLawCacheDisciplineTests` (Punkt 10) ·
`MySqlTranslationTests` +3.

**Fertig, wenn** eine Warnung mit Ablaufdatum online ist und `/recht` genau die freigegebenen Paragrafen
zeigt. ✅

---

## Phase 14c — Öffentliche Lageberichte

**Ziel:** Ein Monat in Worten, freigegeben von der Führung.

**Daten** (`Oeffentlich14_Lageberichte`)
- `OeffentlicherLagebericht` → `OeffentlicheLageberichte`: `SituationReportId`, `FreigegebenHtml`
  (`longtext`), `Status`, `VeroeffentlichtAm`/`VonId`, `IAuditable`, `ISoftDelete`.

**Code**
- `IPublicSituationReportService`; Seite **`/berichte`** (nicht `/lageberichte`, siehe 14a Punkt 11);
  `PublicSituationReportPanel` in `/einstellungen`.
- `PublicModules`: `Lageberichte` ⇒ `Available`.

**Entschieden: der Bericht gibt Text frei, keine Zahlen.** „Abschnitte des Monatsberichts einzeln freigeben"
war so nicht baubar. Der interne `SituationReport` ist kein Text, sondern ein **gefrorener
JSON-Statistik-Snapshot**: `DashboardMetrics` trägt eine `Classified`-Zahl (wie viele Verschlusssachen es
gibt), `StatisticsTopEntry` trägt Name + internes Aktenzeichen + `/personen/{id}`-Href, und alle
Verteilungen sind über den **ganzen** Bestand gerechnet (`GetReportAsync(isLeadership: true)`). Ein
Abschnitt daraus mechanisch nach außen zu geben wäre genau die Zahl, die Phase 15 mit ihrer Regel
„ausschließlich aus öffentlichen Tabellen und publizierten Einträgen" verbietet. `FreigegebenHtml` ist
deshalb ein von der Führung geschriebener Text; die `SituationReportId` ist Anker und Datum, nie Datenquelle.

**Tests** Entwurf anonym nicht erreichbar · kein Feld des JSON-Snapshots erreicht eine öffentliche
Projektion (Dateiscan-Muster `PublicPageScanTests.InternalMarkers`, das `ThreatScore` schon so hält).

**Fertig, wenn** ein freigegebener Monatstext auf `/berichte` steht und `/lageberichte/{Id}` weiter intern und
`noindex` ist.

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
