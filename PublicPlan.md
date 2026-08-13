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
8. **Nachvollziehbarkeit:** Publizieren, Depublizieren, Kopfgeld-Änderung, Anonymitäts-Auflösung und
   Ticket-Antwort schreiben `ManualAudit.Row` **gegen die Personen-/Fraktionsakte** (⇒ Zeitstrahl + Chronik).
   Neue Kind-Tabellen brauchen ihren Fall in `TimelineService.AuditSourceAsync` **und** einen Titel in
   `TimelineDisplay.MapAudit`.

---

## Phasenübersicht

| # | Phase | Migration | Abhängt von |
|---|---|---|---|
| 1 | Bürger-Login & Profil | `Phase61_BuergerKonto` | — |
| 2 | Modul-Schalter, Kill-Switch, Nav, Indexierung, `PublicVisibility` | `Phase62_OeffentlicheModule` | 1 |
| 3 | CMS-Seiten (Text zur Arbeit, FAQ, Zuständigkeiten) | `Phase63_OeffentlicheSeiten` | 2 |
| 4 | Fahndung Kern: publizieren, Board, Steckbrief | `Phase64_OeffentlicheFahndung` | 2 |
| 5 | Fahndung Ausbau: Warnhinweise, Ablauf, Archiv, Druck, Push | `Phase65_FahndungWarnhinweise` | 4 |
| 6 | Kopfgeld: Anteile, behördlich + privat, Deckung, Historie | `Phase66_Kopfgeld` | 4 |
| 7 | Hinweise Kern: Formular, Eingang, Rückfrage, Verfolgung | `Phase67_Hinweise` | 4 |
| 8 | Hinweise Ausbau: Dubletten, Priorität, Vertrauen, Übernahme | keine | 7 |
| 9 | Belohnung: Split-Zuordnung, Auszahlung, Beleg | `Phase69_HinweisBelohnung` | 6, 7 |
| 10 | Ticket-Chat (Führungsebene) | `Phase70_Tickets` | 2 |
| 11 | Öffentliche Vorlagen + 4. Token-System | `Phase71_OeffentlicheVorlagen` | 7, 10 |
| 12 | Organisationen + Gefahrenlisten | `Phase72_Fraktionsprofile` | 4 |
| 13 | Gesuchte Fahrzeuge/Waffen + Einspruch | `Phase73_FahrzeugeEinspruch` | 4, 1 |
| 14 | Presse, Lageberichte, Gesetzesauszüge, Warnungen | `Phase74_Redaktion` | 3 |
| 15 | Zahlen: Gefahrenlage-Ampel, Trend, Zähler, Landing-Hero | keine | 4, 7, 14 |
| 16 | Suche & NOOSEI-Anbindung + interne KPIs | keine | 4, 7, 10 |

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
  (`RequireAssertion(ctx => ctx.User.GetStatus() == AgentStatus.Civilian)`).
- `AgentPrincipalExtensions.IsCitizen()`.
- `IdentityComponentsEndpointRouteBuilderExtensions`: Zweig `source == "buerger"` →
  `CreateAgentAsync(..., AgentStatus.Civilian)`; im Status-`switch` Fall `Civilian` → SignIn + `/buerger`.
  Bestehende Zweige unangetastet.
- `IBuergerService`: Profil lesen/anlegen/ändern (Namensänderung → `ManualAudit.Row`), sperren/entsperren
  (`Permission.RequireLeadership`), `RequireNotBlocked` als Guard für spätere Schreibpfade.
- `Components/Layout/BuergerLayout.razor` (Kopie-Muster `ApplicantPortalLayout`) und
  `Components/Pages/Portal/BuergerPortal.razor` (`/buerger`) + `BuergerProfil.razor` (`/buerger/profil`).
  Profil-Zwang im **Layout**, nicht nur in der UI: ohne Vor-/Nachname wird auf `/buerger/profil` umgeleitet.
- `/einstellungen` → neue Gruppe „Öffentlicher Bereich", erster Abschnitt `PublicCitizensPanel`
  (Liste, Suche, Sperren mit Grund).
- `Privacy.razor` + `Nutzungsbedingungen.razor`: Bürgerdaten, Zweck, Aufbewahrung, Anonymitätszusage.
- `AgentSelection` **nicht** anfassen — `Civilian` ist nicht `Active`, fällt also überall automatisch heraus.
  Das ist mit einem Test festzuhalten, nicht mit Code.

**Tests** `CitizenLoginFlowTests` (Status-Weiche, bestehende Zweige unverändert) · `BuergerServiceTests`
(Sperre blockt Schreibpfade, Namensänderung erzeugt Audit-Zeile) · `AgentSelectionTests` erweitern:
ein `Civilian` erscheint in **keinem** Picker, keiner Filterliste, keinem Roster.

**Fertig, wenn** ein zweiter Discord-Account sich anmelden, Namen setzen und wieder anmelden kann, im
Roster nirgends auftaucht, und eine Sperre ihn beim nächsten Schreibversuch abweist.

---

## Phase 2 — Modul-Schalter, Kill-Switch, Nav, Indexierung, `PublicVisibility`

**Ziel:** Das Gerüst, in das alle späteren Module eingehängt werden. Ab hier ist jedes neue Modul
per Definition abschaltbar.

**Daten** (`Phase62_OeffentlicheModule`)
- `Data/Entities/Public/OeffentlichesModul.cs` → `OeffentlicheModule`: `Schluessel` (unique),
  `IstAktiv`, `OfflineText`, `Reihenfolge`, `LabelUeberschreibung`, `IconUeberschreibung`, `IAuditable`.
- `SystemSettingKeys`: `PublicAreaKillSwitch`.

**Code**
- `Services/Public/PublicModules.cs` — statischer Katalog aller Modul-Schlüssel (eine Zeile je Modul,
  auch für noch nicht gebaute); Seeding beim Start wie `ThreatScoreSweepWorker` seine Fraktionen seedet.
- `IPublicModuleService` + Impl: `GetAsync` (`IMemoryCache`, 10 s), `IsEnabled`, `RequireEnabled` (wirft),
  `SaveAsync` (`Permission.RequireAdmin`), Kill-Switch schlägt jedes Einzelmodul.
- `Services/Public/PublicVisibility.cs` + `PublicVisibilityCoverageTests` — jetzt einführen, solange die
  Liste noch leer ist. Jede Entität aus späteren Phasen muss sich eintragen, sonst roter Build.
- `Components/Common/Shared/PublicModuleGate.razor` — rendert Inhalt oder Offline-Text.
- `Components/Layout/PublicNav.razor`: hartkodiertes `Tabs`-Array raus, aus `IPublicModuleService` gespeist,
  nach `Reihenfolge`; Gruppierung vorbereiten (Fahndung / Behörde / Service), damit 10+ Tabs später tragen.
- `/einstellungen`: `PublicModulesPanel` (Schalter, Offline-Text, Reihenfolge, Label/Icon) — hängt an
  `Policies.AdminPage`.
- `wwwroot/robots.txt` erlaubt öffentliche Routen, sperrt alles andere; Middleware setzt
  `X-Robots-Tag: noindex` für alles außerhalb der Public-Routen.

**Tests** `PublicModuleServiceTests` (Cache, Kill-Switch schlägt Einzelmodul, `RequireEnabled` wirft) ·
`PublicVisibilityCoverageTests` (scharf, noch leer) · `PublicNavTests` (nur aktive Module, Reihenfolge) ·
`RobotsTests` (öffentliche Route ohne noindex, interne mit).

**Fertig, wenn** ein Modul im Panel ausgeschaltet werden kann und die zugehörige Route den Offline-Text
zeigt, der Tab verschwindet und der Kill-Switch alles auf einmal abschaltet.

---

## Phase 3 — CMS-Seiten

**Ziel:** „Text zu unserer Arbeit" — Auftrag, Befugnisse, Zuständigkeiten, FAQ — redigierbar mit Entwurf,
Vorschau und bewusstem Veröffentlichen.

**Daten** (`Phase63_OeffentlicheSeiten`)
- `OeffentlicheSeite` → `OeffentlicheSeiten`: `Slug` (unique), `Titel`, `MenueTitel`, `Icon`, `Reihenfolge`,
  `InhaltHtml` (`longtext`, veröffentlicht), `EntwurfHtml` (`longtext`), `Status`
  (`Entwurf`/`Veroeffentlicht`), `IstAktiv`, `VeroeffentlichtAm`/`VonId`, `IAuditable`, `ISoftDelete`.

**Code**
- `IPublicPageService`: Entwurf speichern, Vorschau (agentenseitig), veröffentlichen, zurückziehen.
  HTML immer über `HtmlCleanup.Clean` — **nicht** `CleanAiPayload`.
- `Components/Pages/Public/Seite.razor` (`/info/{Slug}`), statisch gerendert, gecacht.
- `/einstellungen` → `PublicPagesPanel` (Liste, RichTextEditor, Vorschau-Link, Veröffentlichen,
  „geändert von/am"), `Policies.LeadershipPage`.
- Seed: `auftrag`, `befugnisse`, `zustaendigkeiten`, `faq` als Entwürfe mit Platzhaltertext.
- `TrashService` + `TrashProjection`: eine Zeile für `OeffentlicheSeite`.

**Tests** `PublicPageServiceTests` (Entwurf ändert die veröffentlichte Ausgabe nicht; Zurückziehen macht die
Route unsichtbar; Sanitizer greift) · `TrashCoverage` bleibt grün.

**Fertig, wenn** eine Seite als Entwurf geschrieben, als Agent vorab betrachtet und dann veröffentlicht
werden kann — und der Entwurf vorher anonym nicht erreichbar war.

---

## Phase 4 — Fahndung Kern

**Ziel:** Der eigentliche Kern. Eine Person wird bewusst publiziert, erscheint auf `/gesucht` und hat einen
Steckbrief. Noch ohne Kopfgeld, ohne Hinweis-Button.

**Daten** (`Phase64_OeffentlicheFahndung`)
- `OeffentlicheFahndung` → `OeffentlicheFahndungen`: `Aktenzeichen` (eigener Präfix `NOOSE-F` über
  `CaseNumberCounter`), `Art` (`Fahndung`/`Vermisst`/`Zeugenaufruf`/`Fahrzeug`/`Waffe` — Enum jetzt komplett,
  genutzt zunächst nur `Fahndung`), `PersonId` (nullable), `FraktionId` (nullable),
  **Snapshot:** `AnzeigeName`, `AliaseText`, `FotoDateiname`, `VorwurfHtml` (`longtext`), `LetzteGegend`,
  `FahrzeugText`, `OeffentlicheGefahrenstufe`; `Status`
  (`Entwurf`/`Beantragt`/`Veroeffentlicht`/`Gefasst`/`Zurueckgezogen`/`Abgelaufen`), `AblaufDatum` (nullable),
  `KopfgeldIstObergrenze`, `VeroeffentlichtAm`/`VonId`, `AufrufZaehler`, `IAuditable`, `ISoftDelete`.
- `RequestType`: `Veroeffentlichung`.

**Code**
- `Services/Public/IPublicWantedService`: Entwurf anlegen (Snapshot aus der Personenakte ziehen),
  Snapshot aktualisieren, publizieren, depublizieren mit Grund, `GefasstAsync`.
  **Publish-Guards in dieser Reihenfolge:** `Permission.RequireWriteAccess` →
  `IPublicModuleService.RequireEnabled` → **harte VS-Sperre** (`person.IsClassified` ⇒
  `InvalidOperationException`, rangunabhängig, auch für Admin) → Rang ≥ 3 publiziert direkt, Rang 1–2 erzeugt
  `Request` mit `RequestType.Veroeffentlichung` → `ManualAudit.Row` gegen die Personenakte.
- `Models/Public/PublicWantedCard`/`PublicWantedDetail` — enthalten baulich **kein** Feld für
  Codename/Klarname/Dienstgrad. Score wird beim Rendern live aus `Person.ThreatScore` gelesen, nur für
  publizierte Einträge.
- Foto: Auswahl aus `PersonPhoto`, Auslieferung über autorisierten Endpoint aus `App_Data/uploads` — der
  öffentliche Endpoint prüft, dass genau dieses Foto publiziert ist, nicht nur, dass es existiert.
- Seiten: `/gesucht` (Board, statisch, gecacht, Filter Art/Stufe), `/gesucht/{Aktenzeichen}` (Steckbrief).
- Intern: Publish-Dialog in `PersonDetail`, **Warnbanner** „Diese Akte ist öffentlich ausgeschrieben" oben
  in `PersonDetail`, Antrags-Eintrag in der bestehenden Anträge-Inbox.
- `PublicVisibility`-Eintrag · `TrashService`+`TrashProjection` · `TimelineService.AuditSourceAsync` +
  `TimelineDisplay.MapAudit` für Publizieren/Depublizieren.
- `PublicModules`: `Fahndung`.

**Tests** VS-Akte ist rangunabhängig nicht publizierbar · Rang-Weiche (3 direkt, 2 erzeugt Antrag) ·
**Snapshot-Isolation**: Änderung am Namen/Vorwurf in der Personenakte verändert die öffentliche Ausgabe nicht ·
Foto-Endpoint liefert unpublizierte Fotos nicht aus · Anonymitäts-Datei-Scan über `Models/Public/*` und
`Components/Pages/Public/*` · Zeitstrahl-Eintrag entsteht.

**Fertig, wenn** eine Person publiziert ist, anonym auf `/gesucht` erscheint, ein Rang-2-Agent nur einen
Antrag erzeugt, eine VS-Akte gar nicht geht, und der Zeitstrahl der Akte die Publikation zeigt.

---

## Phase 5 — Fahndung Ausbau

**Ziel:** Warnhinweise, kein Vergessen, Archiv, Poster, Reichweite.

**Daten** (`Phase65_FahndungWarnhinweise`)
- `FahndungWarnhinweis` → `FahndungWarnhinweise`: `FahndungId` + Werteliste-Eintrag (n:m).
- Werteliste „Warnhinweise" nach dem Muster der vorhandenen Wertelisten in `BaseDataPanel`
  („bewaffnet", „gewaltbereit", „flieht mit Fahrzeug", „nicht selbst eingreifen").

**Code**
- `PublicWantedExpiryWorker` (Muster `FollowupDueWorker`): abgelaufene Einträge → `Abgelaufen` +
  Führungs-Notification über `NotificationService`.
- `/gefasst`: Archiv der Einträge mit Status `Gefasst`, mit Stempel und Datum.
- `/gesucht/{Aktenzeichen}/druck`: Poster im vorhandenen `PrintLayout`, `[AllowAnonymous]`.
- Aufrufzähler: `ExecuteUpdateAsync` (umgeht den Audit-Interceptor bewusst — rein technischer Zähler,
  im Kommentar begründen).
- Discord-Push beim Publizieren über die vorhandene Discord-Benachrichtigungs-Konfiguration.
- `PublicModules`: `FahndungArchiv`, `FahndungDruck`.

**Tests** Ablauf-Worker setzt Status und benachrichtigt · Archiv zeigt nur `Gefasst` · Druckansicht ist
anonym erreichbar und enthält keinen Agentenbezug · Aufrufzähler erzeugt kein `GeaendertAm`.

**Fertig, wenn** ein Eintrag mit Ablaufdatum von selbst offline geht, das Archiv gefüllt ist und ein Poster
druckbar ist.

---

## Phase 6 — Kopfgeld

**Ziel:** Behördliches Geld aus der Kasse **und** privates Geld von Agenten auf einen Kopf; öffentlich nur
die Gesamtsumme.

**Daten** (`Phase66_Kopfgeld`)
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

**Daten** (`Phase67_Hinweise`)
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

**Daten** (`Phase69_HinweisBelohnung`)
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

**Daten** (`Phase70_Tickets`)
- `Ticket` → `Tickets`: `Aktenzeichen` (`NOOSE-T`), `Art` (`Fuehrungsebene = 0`, weitere Werte
  **vorbereitet, inaktiv**), `BuergerProfilId`, `Betreff`, `Status`
  (`Offen`/`InBearbeitung`/`WartetAufBuerger`/`Geschlossen`), `BearbeiterId`, `LetzteAktivitaetAm`,
  `ZuletztGelesenBuergerAm`, `ZuletztGelesenAgentAm`, `IAuditable`, `ISoftDelete`.
- `TicketNachricht` → `TicketNachrichten`: `Zielgruppe` (`Buerger`/`Intern`), `Text`, `VonBuerger`,
  `AutorAgentId` (**nur intern**).

**Code**
- `ITicketService`: Öffnen (Bürger, `RequireEnabled`, `RequireNotBlocked`, Rate-Limit), Antworten
  (`Permission.RequireLeadership`), interne Notizen, Schließen/Wiederöffnen, Lesestände.
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

**Daten** (`Phase71_OeffentlicheVorlagen`)
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

**Daten** (`Phase72_Fraktionsprofile`)
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

**Daten** (`Phase73_FahrzeugeEinspruch`)
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

**Daten** (`Phase74_Redaktion`)
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
- Zeitstrahl der Personenakte enthält Publizieren, Depublizieren und Kopfgeld-Änderung.
- `App_Data` bleibt beim Deploy unberührt; `?v=` bei JS-Änderungen gebumpt.
