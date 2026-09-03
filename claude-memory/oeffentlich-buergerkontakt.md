# Bürgerkontakt — Hinweise, Triage, Übernahme, Ticket-Chat, Vorlagen

> **Lies das, bevor du `Hinweise`, `Tickets`, `HinweisNachricht`, `TicketNachricht`,
> `OeffentlicheVorlage` oder einen Pfad anfasst, der einen Bürger adressiert.**
> Kernregeln: **Anonymität ist eine Projektion und eine Audit-Regel, keine UI-Bedingung**
> (`Services/Public/TipAnonymity.cs`); Rate-Limits stehen **im Dienst**, nicht in der Middleware
> (Einreichung läuft über SignalR); Bürger-Zeilen tragen baulich **keinen** Agenten;
> `Public*`-Benachrichtigungen sind **nicht routbar** (Discord-Kanal würde den Hinweisgeber outen).
> Geld: [oeffentlich-geld.md](oeffentlich-geld.md) · Grundlagen: [oeffentlich-grundlagen.md](oeffentlich-grundlagen.md)

## Phase 7 — Bürgerhinweise

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
  - **Der Hinweis ist seit Phase 16 durchsuchbar, seine Nachrichten nicht.** `HinweisNachricht` bleibt in
    `SearchCatalog.NotSearchable`: gefunden wird der Hinweis, nicht die einzelne Zeile. Der Provider trägt die
    Anonymitätszusage strukturell, indem er kein Bürgerfeld liest.
  - Registriert in `PublicVisibility` (beide `NeverPublic`), `SearchCatalog`, `TrashService`/`TrashProjection`
    (die Papierkorb-Zeile nennt weder Bürger noch Text), `AuditEntityDisplay`, `WatchlistRecordRollup`
    (beide „not watchable"), den vier Zeitstrahl-Stellen und `MergedPageSections.Trash`.

## Phase 8a — Hinweis-Triage (Priorität, Dubletten, Vertrauen)

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

## Phase 8b — Übernahme in eine Akte

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

## Phase 10 — Ticket-Chat an die Führungsebene

- **Phase 10 (Ticket-Chat) — was daran anders ist:**
  - **Ein Ticket hängt an keiner Akte.** Es ist Schriftwechsel, kein Aktenmaterial: **kein** Eintrag in
    `TimelineService.AuditSourceAsync`, `TimelineDisplay.MapAudit`, `ChronikParentResolver`, `RecordsReference`
    oder `LinkService` — es gibt keinen Elternteil, auf den es fan-in könnte. Registriert ist es in
    `PublicVisibility`, `SearchCatalog` (seit Phase 16 mit Provider; `TicketNachricht` bleibt draußen),
    `AuditEntityDisplay`
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
  - **Gepingt wird trotzdem — über eine eigene Kategorie.** `PublicTicketCreated` ist routbar und pingt die
    Rolle aus `/einstellungen?tab=discord` (Default: die generische NOOSE-Rolle), und zwar **nur beim Anlegen**:
    der Push hängt in `NotifyDeskAsync` — dem einzigen Melder, der ausschließlich beim Öffnen läuft — und
    nicht im `NotificationService`. Drei Folgen, die man nicht „aufräumen" darf: **(1)**
    `PublicTicketOpened` bleibt unroutbar — würde man *sie* routen, pingte jede Bürgerantwort mit, sobald der
    Eingang gerade gelesen war (`NotifyManyOnceAsync` pusht an die *frisch* angelegten Empfänger, still bleibt
    nur die gefaltete Zeile — der Ping hinge also am Lesestand der Führung). **(2)** `PublicTicketCreated`
    legt **keine Glocken-Zeile** an; sie ist reiner Routing-Schlüssel, damit Channel und Rollen-ID im
    Admin-Panel eine eigene Zeile bekommen. **(3)** Der Push übergibt **keinen** `headline` — im Kanal steht
    der generische Hinweis plus der anmeldepflichtige Link, nie der Betreff und nie der Bürger.
  - **Ein Ticket klingelt einmal, nicht je Nachricht.** Alle vier Ticket-Melder gehen über
    `INotificationService.NotifyOnceAsync`/`NotifyManyOnceAsync`: existiert für dasselbe Trio
    (Empfänger, `NotificationType`, `Href`) noch eine **ungelesene** Zeile, wird sie überschrieben und ihr
    `ErstelltAm` hochgesetzt, statt eine zweite anzulegen. Ein laufender Schriftwechsel füllt die Glocke damit
    nicht Zeile für Zeile; gelesen ⇒ das nächste Ereignis legt wieder eine neue Zeile an. Zwei Folgen, die man
    nicht „aufräumen" darf: das Sortierkriterium der Glocke ist `ErstelltAm`, ohne den Bump bliebe die
    zusammengefasste Zeile unter neueren begraben — und der Discord-Push geht nur an die *frisch* angelegten
    Empfänger, weil eine Zusammenfassung kein neues Ereignis ist (bei Tickets ohnehin nicht routbar, aber die
    Methode ist allgemein). Ohne `Href` gibt es kein Faltkriterium ⇒ es wird immer angelegt.
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

## Phase 11 — Vorlagen für Bürger-Nachrichten

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
    (Konfigurationstabelle, Präzedenz `DocumentTemplate` — `ISoftDelete` und trotzdem
    nicht im globalen Papierkorb; zurückgezogen wird über `IstAktiv`). Registriert ist die Tabelle in
    `PublicVisibility`, `SearchCatalog` (`NotSearchable`), `AuditEntityDisplay` (Label **und** Route),
    `MergedPageSections.Settings`, `WatchlistRecordRollup` — **und in `FeedbackPageTabs`, der sechsten
    Registry**, die jeder neue `/einstellungen`-Abschnitt braucht (`FeedbackPageTabsTests` verlangt jeden
    `MergedPageSections`-Slug im Feedback-Picker).

## Phase 18 — Ergreifungsmeldung (Bürger stellt eine gesuchte Person selbst)

- **Phase 18 (Ergreifungsmeldung) — was daran anders ist:**
  - **Eine Meldeart, keine eigene Tabelle.** Eine Ergreifungsmeldung ist ein `Hinweis` mit
    `Art = TipKind.Ergreifung`, weil sie alles teilt, was zählt: Aktenzeichen als Handle, Fahndungsbezug,
    Anhang, Triage-Priorität, den Bürger-Chat und den Auszahlungs-Endpunkt. Nachgemessen: eine zweite Tabelle
    müsste **zwölf** Registries neu bedienen (`PublicVisibility`, `SearchCatalog` + Provider, `TrashService`,
    `TrashProjection`, `AuditEntityDisplay` ×2, `WatchlistRecordRollup`, `TimelineService` ×2,
    `TimelineDisplay`, `ChronikParentResolver` ×2, `RecordsReference`, `MergedPageSections`) und den ganzen
    Chat verdoppeln — eine Spalte bedient **keine** davon neu, weil alle auf dem *Typnamen* verschlüsselt sind
    und `PublicVisibilityCoverageTests`/`SearchCoverageTests` über `DbSet`s reflektieren, nicht über Spalten.
    Präzedenz: `TicketArt` mit genau einem Wert.
  - **Zwei Enums, nicht eines mit drei Werten.** `TipKind` (Beobachtung/Ergreifung) entscheidet, *welche Regeln*
    gelten, `TipHandover` (Festgehalten/Uebergeben) *wie dringend* es ist — orthogonale Achsen, Lehre aus
    `NavSection`/`NavArea`. Zusammengelegt müsste jede Prüfung „ist das eine Ergreifungsmeldung?" zwei Werte
    aufzählen.
  - **Der Prioritäts-Boden steht in der Formel, nicht im Handstempel.** `TipPriority.Floor` hebt eine Meldung
    an, `Compute` nimmt `Math.Max`. **Festgehalten bekommt die Decke (`Max`), nicht 90** — nachgerechnet:
    eine maximale Beobachtung ist 5 × 5 × 4 = 100 und hätte eine Meldung überholt, bei der jemand eine Person
    festhält. Übergeben behält einen Boden **unter** `Max` (70): hoch, weil Geld daran hängt, aber niemand ist
    mehr in Gefahr. Ausdrücklich **nicht** `PriorityOverride` — das Feld heißt in der UI „Priorität manuell",
    trägt einen Begründungstext, und `StampWhereAsync` überspringt Zeilen mit Override *für immer*, die
    automatische Neuberechnung wäre also dauerhaft aus. Der Stempler trägt die zwei Achsen deshalb in seiner
    Projektion mit; er rechnet in-memory und schreibt je Gruppe einen Konstantwert, ein `Math.Max` muss also
    nie nach SQL übersetzt werden. `TipPriority.Max` bleibt 100, weil es auch der Deckel von `SetPriorityAsync`
    ist.
  - **Anonymität wird vorne abgewiesen, nicht hinten festgestellt.** `CaptureRules.AllowsAnonymity` ist `false`,
    `SubmitAsync` überschreibt ein gesetztes `WantsAnonymity`, und das Formular zeigt den Schalter gar nicht.
    Grund: „Anonym ist unauszahlbar" gilt im Belohnungs-Pfad schon heute — eine Wahl anzubieten, die später
    überstimmt wird, liest sich als gebrochenes Versprechen.
  - **Nur eine `Fahndung` ist meldbar** (`CaptureRules.MayReport`), als **Positivliste**, nicht als
    „alles außer Fahrzeug und Waffe": niemand ergreift ein Auto, aber auch keine Vermisstenmeldung und keinen
    Zeugenaufruf — wer eine vermisste Person findet, gibt einen *Hinweis*. Heute wird nur `Fahndung`
    ausgeschrieben, die Regel engt also nichts ein; sie verhindert, dass spätere Arten still mitqualifizieren.
  - **Wer selbst ausgeschrieben ist, kann die eigene Ergreifung nicht melden** — verglichen wird über
    `ObjectionRules.NamesCitizen`, also **nur** gegen den publizierten Anzeigenamen (ein Alias ist oft
    informantengestützt, und der interne Klarname wäre über Treffer/kein-Treffer abfragbar).
  - **Zwei getrennte Tageskontingente.** `CaptureRules.PerDay` (2, flach) steht neben dem trust-gestaffelten
    `TipTrust.QuotaFor` (5–20), und beide zählen **nur ihre eigene Art**: eine Beobachtungs-Serie darf die echte
    Ergreifung nicht blockieren, und eine Ergreifung darf das Hinweis-Kontingent nicht aufessen. Der Ping ist die
    knappe Ressource, deshalb ist die Zahl klein und **nicht** an die Vertrauensstufe gekoppelt. Gezählt wird
    mit `IgnoreQueryFilters` (Löschen kauft keinen Versuch). Den offensichtlichen Missbrauch stoppt ohnehin die
    Regel **ein offener Bericht je Ausschreibung und Konto**.
  - **Dubletten kreuzen keine Arten.** `GroupDuplicatesAsync` nimmt `Art` als zusätzliche Gleichheit ins
    Kandidatenfenster: eine Beobachtung und eine Ergreifungsmeldung zur selben Ausschreibung sind zwei
    verschiedene Aussagen, nicht zwei Erzählungen derselben.
  - **`TipsBroadcaster` trägt jetzt Zeilen-Id *und* Aktenzeichen *und* Zielgruppe** — wörtlich der
    `TicketBroadcaster`. Vorher trug er nur die Id, und genau deshalb war `MeineHinweise.razor` **nicht** live
    angebunden (die Bürgerseite kennt ihren Vorgang nur über das Aktenzeichen). Das Aktenzeichen verhindert, dass
    jeder Bürger-Circuit bei jeder Hinweisänderung im Haus neu lädt; die **Zielgruppe** verhindert, dass der
    *Zeitpunkt* jeder internen Notiz in den Circuit des Bürgers signalisiert wird — Inhalt leckte nie, „wann die
    Behörde über mich spricht" schon. `null` heißt „betrifft beide Fäden" (Einreichung, Status, Löschung).
  - **Zwei `@page`-Direktiven auf `TipForm.razor`**, `/hinweis` und `/hinweis/gestellt`. Der Modus kommt aus dem
    **Pfad**, nicht aus einem Query-Parameter: ein `Enum.Parse` auf angreifer-kontrollierter Eingabe ist auf einer
    `[AllowAnonymous]`-Seite ein HTTP 500 (Präzedenz `?vorschau=1`, `WarnhinweisColours`). `PublicRoutes.Matches`
    ist `path == prefix || path.StartsWith(prefix + "/")`, das **Kind**segment erbt den öffentlichen Präfix also
    automatisch — keine neue Zeile in `PublicRoutes`, `DemoModeMiddleware` oder `robots.txt`. Das Modul-Gate trägt
    ein **`@key`**: beide Routen binden denselben Komponententyp, Blazor recycelt die Instanz, und
    `PublicModuleGate` entscheidet in `OnInitializedAsync` — ohne `@key` bliebe es auf dem Modul der Route stehen,
    von der man kam.
  - **Das Modul hat keine Nav-Route**, und hier ist der Grund mehr als der übliche: `PublicRoutes.Prefixes` leitet
    sich aus `PublicModules.All` ab und `PublicModulesCatalogTests` verlangt eindeutige Nav-Routen (`/hinweis`
    gehört dem Tips-Modul) — **und** ein Tab, der eine frische Meldung ohne Ausschreibung in der Hand einlädt,
    liest sich als Aufforderung, jemanden zu stellen. Gemeldet wird vom Steckbrief aus. Präzedenz: `Reward`.
  - **`PublicCaptureReported` ist routbar und pingt eine Rolle** (Default: NOOSE). Anders als
    `PublicTipReceived`/`PublicTipAnswered`, und das ist kein Widerspruch: der Post trägt nur den generischen Satz
    aus `Notice(type)` plus den anmeldepflichtigen Link, nennt also weder Bürger noch Gesuchten — und der Melder
    ist auf diesem Weg baulich nicht anonym. `ShouldIncludeHeadline` wird **bewusst nicht** erweitert: eine
    Überschrift schriebe das Fahndungs-Aktenzeichen in den Kanal. Webhook- und Rollen-Key entstehen aus dem
    Enum-Namen, die Admin-Zeile also von selbst; `FallbackRoute` zeigt auf `/hinweise`, sonst landete ein
    generischer Push auf `/dashboard`.
  - **Ein Filter, kein vierter Reiter.** Die Abschnitte des Eingangs sind die Status-Achse (`ScopeFilter`,
    `TipInboxCounts` mit drei status-benannten Feldern); die Meldeart ist eine zweite. Sie als vierten
    `TipInboxScope`-Wert hineinzupressen ist genau der `NavSection`/`NavArea`-Fehler — also ein Schalter „Nur
    Ergreifungen" neben den Reitern, client-seitig über die ohnehin geladenen ≤200 Zeilen (Muster `_collapse`).
  - **Die Detailseite setzt die Fahndung nicht auf gefasst.** „`Gefasst` ist Vorbedingung, keine Nebenwirkung"
    (siehe [oeffentlich-geld.md](oeffentlich-geld.md)): `PublicWantedService` behält seinen einen Schreibpfad,
    die Seite schreibt nur hin, was als Nächstes fällig ist. Die Auszahlung läuft danach unverändert über
    `IRewardService` — eine Ergreifungsmeldung ist auszahlbar, *weil* sie per Regel nicht anonym ist.
  - Kind-bewusste Beschriftung gibt es nur an den zwei Stellen, die die Zeile in der Hand haben
    (`TrashProjection.Tip`, `RecordsReference`). `AuditEntityDisplay`, `TimelineDisplay` und `SearchCatalog` sind
    auf den Typnamen verschlüsselt und bleiben bewusst generisch bei „Bürgerhinweis".
