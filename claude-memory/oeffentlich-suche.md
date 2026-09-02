# Öffentliche & interne Suchanbindung des Außenbereichs (Phase 16)

> **Lies das, bevor du einen `ISearchProvider` für eine öffentliche Kategorie, `PublicSearchService`,
> `/suche-oeffentlich` oder die internen Außen-Kennzahlen anfasst.**
> Kernregeln: **kein Suchtreffer nennt einen Bürger, strukturell** (Provider lesen kein Bürgerfeld);
> `PublicSearchService` hält **keinen** `IDbContextFactory` — das Fehlen des Handles *ist* der Entwurf;
> genau ein Typ bekommt `NooseiUse.Read` (`Hinweis`).
> Suche allgemein: [services-details.md](services-details.md) · NOOSEI: [noosei.md](noosei.md)

## Phase 16 — Suche, NOOSEI-Anbindung, interne KPIs

- **Phase 16 (Suche, NOOSEI, interne KPIs) — was daran anders ist:**
  - **Neun Kategorien verlassen `SearchCatalog.NotSearchable`, nicht die sieben des Plantexts.** 14b und 14c
    hatten `OeffentlicheWarnung` und `OeffentlicherLagebericht` mit derselben Versprechens-Formel nachgetragen.
    `SearchCoverageTests` prüft, dass eine Begründung **existiert**, nicht dass sie noch stimmt — sieben zu bauen
    wäre grün geblieben und hätte zwei uneingelöste Versprechen stehengelassen.
  - **`OeffentlicheFahndung` und `OeffentlichesFraktionsprofil` sind `ContentChild` mit `ParentTab "oeffentlich"`**
    und zielen auf die Akte, aus der publiziert wurde. Der Join gegen `db.People.OnlyVisible(scope)` **ist** das
    Sichtbarkeits-Prädikat — der Provider benennt eines, statt eines zu schreiben. Bewusst enger als
    `GetAllAsync`, das die Einstufung über `IgnoreQueryFilters` auflöst, damit eine Ausschreibung auf einer
    gelöschten Akte bearbeitbar bleibt: ein Treffer muss anspringbar sein. Deshalb **kein** Id-Mengen-Paritätstest
    für diese Kategorie.
  - **`AppliesTo` nimmt den schmalen Guard** (`RequirePublicWantedRead`, Rang ≥ 3 oder Aufsicht). Der weite
    `RequirePublicWantedRecordRead` gilt der **einen** Ausschreibung, an der ein Rang-1–2-Agent arbeitet; eine
    Suche ist eine Querliste. `AppliesTo == false` nimmt die Kategorie ganz aus dem Katalog — „nichts passte" und
    „nicht deins" dürfen nicht gleich aussehen.
  - **Kein Suchtreffer nennt den Bürger, strukturell.** Hinweis-, Ticket- und Einspruchs-Provider lesen kein
    Bürgerfeld, auch nicht bei aufgelöster Anonymität: Namen tippen und schauen, ob ein Hinweis zurückkommt, wäre
    ein Anonymitäts-Orakel. Nebeneffekt: keine Pflicht-Navigation auf ein soft-löschbares `BuergerProfil`, die EF
    **INNER** joint — anders als im Schalter-Eingang fällt der Hinweis eines entfernten Kontos hier nicht heraus.
    Wächter: `NoSearchProvider_ProjectsACitizenIdentity` scannt `Services/Search/Providers/` auf `BuergerProfil`
    und `CitizenProfile` — **nur** auf diese beiden, weil `FirstName`/`LastName` am Agenten-Provider falsch-rot
    wären und der Scan dann entschärft würde.
  - **`SearchParentResolver` bekommt genau einen Arm — für `Hinweis`, als Verknüpfungs-*Ende*.** Keine der neun
    ist ein polymorphes Kind. `TipTakeoverService` schreibt `Link.SourceType = nameof(Hinweis)`, und
    `LinkSearchProvider` überspringt jede Verknüpfung, deren eines Ende der Resolver nicht auflöst — **seit
    Phase 8b war damit jede Übernahme-Verknüpfung in `/suche` unauffindbar.** Der Kommentar sagt
    „Verknüpfungs-Ende", sonst ergänzt der nächste Leser sechs tote Arme.
  - **Genau ein Typ bekommt `NooseiUse.Read`: `Hinweis`** — ohne `List` (das Filtervokabular von `finde_akten` ist
    aktenförmig) und ohne `Chronicle` (die Chronik ist eine geschlossene Liste von zehn Aktenarten). Er ist als
    einziger der neun aktenförmig: eigenes Aktenzeichen, eigene Route, `RecordsReference`-Arm,
    `LinkService.knownTypes`-Eintrag. **`Ticket` bekommt `Read` ausdrücklich nicht:** es hängt an keiner Akte, also
    weitete `Read` vier Werkzeuge auf einen Typ, der strukturell immer „nichts" antwortet — und das Modell
    unterscheidet „nichts" nicht von „gibt es nicht". Die übrigen acht stehen in `ReachableWithoutRead`.
  - **`ViewerScope.IsInternalAgent`, weil `!IsPartner` weiter ist als die Regel** (ein Bürger und ein Bewerber sind
    auch keine Partner). Nachgestellt, `false` als Vorgabe, also fail-closed. Der Bool-Shim am Ende von
    `Visibility` bleibt unangetastet: auf dem Erwähnungs-Fanout ist ein Hinweis damit unsichtbar, was richtig ist.
  - **Der `Hinweis`-Arm in `Visibility` steht als „jeder interne Agent", nicht als `MayClassifiedRead`.** Das Gate
    entscheidet nicht nur NOOSEI: `CommentService`, `SourceService`, `FollowupService`, `WatchlistService` und
    `LinkService.GetForRecordAsync` reichen einen beliebigen `entityType` hindurch, und die schmalere Form leerte
    das erste `LinkPanel` auf einem Hinweis, ohne es zu sagen.
  - **Der Kurzbrief eines Hinweises trägt kein Bürgerfeld — eine Cache-Regel.** Er wird einmal je Akte auf
    Mindestprivileg erzeugt und von jedem gelesen, der die Akte sehen darf; eine Identität darin überlebte den
    auditierten Führungsakt, der die Anonymität auflöst. Auch das Anonymitäts-Flag bleibt draußen: es ist eine
    Aussage über die Person, nicht über den Hinweis.
  - **Zwei neue `lies_akteninhalt`-Abschnitte und zwei neue `lies_bereich`-Bereiche.** `oeffentlich` (Person: alle
    Ausschreibungen samt Einsprüchen; Fraktion: das öffentliche Profil), `hinweise` (was zu den Ausschreibungen
    einer Person einging — die Antwort auf das Fertig-Kriterium), `tickets` und `oeffentlichkeit`. Alle lesen über
    die Dienste, die die Gates halten, und fangen `UnauthorizedAccessException` ab, damit ein Abschnitt ohne Recht
    wortgleich wie ein leerer liest. `oeffentlichkeit` ist **ein** Bereich über fünf Oberflächen: gleiches Gate,
    gleiches Budget, gleiche Frage. **`NooseiPrompts.ToolChoice` nennt alle vier** — eine Fähigkeit, von der dem
    Modell niemand erzählt, ist keine.
  - **`ITipService.GetForNoticeAsync` ist das Gegenstück zu `GetForLinkedPersonAsync`.** Jenes beantwortet „was hat
    diese Person gemeldet", ist auf die Identität des Bürgers verschlüsselt und hängt deshalb an
    `TipAnonymity.Disclosable`; dieses beantwortet „was kam zu dieser Ausschreibung", nennt niemanden und wird von
    der Zusage folgerichtig nicht eingeschränkt. `IObjectionService.GetForNoticeAsync` ist wie die Schalterliste
    gewurzelt (über den Soft-Delete-Filter, `!IsDeleted` von Hand zurück).
  - **`SearchIndexBackfillWorker.Version` wird NICHT hochgezählt**, entgegen dem Plantext. Der Bump gehört zu einer
    Änderung von `SearchIndexProjection`, und keine der neun darf `SideIndexed` sein (acht scannen `longtext`, was
    `A_side_indexed_category_is_not_a_longtext_scan` verbietet; beim neunten stemmte der Interceptor einen Betreff
    in der Transaktion, in der ein Bürger sein Ticket öffnet). Ein Bump löscht beide Indextabellen und läuft beim
    Start durch den ganzen Bestand — für null neue Zeilen. **Kein Test wehrt sich dagegen.**
  - **`PublicSearchService` ist ein Komponist, kein Abfrage-Layer.** Es nimmt `IPublicModuleService` plus sieben
    `Services/Public`-Schnittstellen und **keinen** `IDbContextFactory`, kein `IMemoryCache`, keinen
    `AppDbContext` — damit erbt es Unterdrückungsgürtel, Modul-Schalter und Not-Aus, statt sie zu wiederholen
    (Präzedenz `/gefahr/personen` aus Phase 12, Vorbild `lies_kalender`). `ThePublicSearchService_NeverWritesAndHoldsNoContext`
    hält das fest und verbietet zusätzlich `IgnoreQueryFilters`: das Fehlen des Handles **ist** der Entwurf.
    `ISearchService` steht jetzt in `PublicSurfaceGuardTests.InternalStacks` — es aufzulösen baute alle ~60
    Provider für einen anonymen Besucher.
  - **Nicht durchsucht, mit Grund** (Prosa im Dienst, keine zweite Registry): das Gefasst-Archiv (ein Treffer
    verlinkte ins Leere, und namentlich durchsuchbar wäre es ein „wurde diese Person je gefasst"-Nachschlagewerk) ·
    beide Gefahrenlisten (dasselbe Subjekt zweimal, und die Gefahrenstufe sähe aus wie ein durchsuchbares Merkmal) ·
    `/lage` und die Zahlen · Kopfgeldbeträge (ein durchsuchbarer Betrag ist eine Preisliste auf Köpfe) · alles aus
    `PublicVisibility.NeverPublic`. Bei den Infoseiten nur die **verlinkte** Menge: `ImMenue` entscheidet die
    Verlinkung, und Suche ist ein zweites Menü.
  - **`/suche-oeffentlich` ist statisch, mit nacktem `<form method="get">`** und deshalb ohne
    `InteractiveExempt`-Eintrag: ein GET-Formular ist eine Browser-Navigation. Ein `MudTextField` mit `@bind-Value`
    sähe aus wie eine Suche und täte nichts. Ein **public string**-Query-Parameter, **kein** `?bereich=` und damit
    kein Wert, den eine Allowlist zurückparsen müsste — `PublicSearchArea` ist reiner Gruppenschlüssel. `noindex`,
    sobald eine Anfrage anliegt: die Route antwortet auf jede erfundene mit 200.
  - **Route, Modul-Schlüssel und `robots.txt`-Zeile gab es seit Phase 2**; die Arbeit war `Available` von `false`
    auf `true`. Folge: es gibt keinen unfertigen Nav-Modul-Eintrag mehr, weshalb
    `NavEntries_ExcludeAModuleWhosePagesDoNotExistYet` die Regel jetzt zusätzlich an einem selbstgebauten Snapshot
    behauptet und den Katalog weiter benutzt, sobald es wieder ein unfertiges Modul gibt.
  - **Die Kennzahlen rechnen in GTA-Dollar, und das ist erlaubt.** Die KI-Eigner-Regel gilt echten API-Kosten:
    `NooseiCostVisibilityTests.MoneyMarkers` sind `ToCents`, `CostUsd`, `ToCost(`, `¢`, `Realkosten` — der
    OpenRouter-Preispfad. Kopfgeld und Belohnung laufen über `Services/Money.cs`, das `/kasse` jedem Agenten
    rendert; das Panel benutzt `Money.Format` und schreibt nie „Realkosten".
  - **Drei Nenner, die falsch zu wählen leicht war.** Die Ergreifungs-Quote läuft über die **entschiedenen**
    Hinweise (`TipRules.ClosedRows`), sonst zählt jeder noch nicht angesehene Hinweis als Fehlschlag. „Belohnung je
    Ergreifung" teilt durch die Ergreifungen, für die **etwas gezahlt wurde**; ohne Auszahlung ist die Zahl `null`,
    nicht `0`. Und „Ergreifungen mit Belohnung" ist ein Anteil **an den Ergreifungen des Zeitraums** — eine
    Auszahlung im Fenster kann zu einer Festnahme davor gehören, und die eine Kohorte durch die andere zu teilen
    ergab Anteile über 100 %.
  - **Eine nicht gemessene Reaktionszeit ist `null`, keine Null** — „0 min" läse sich als sofortige Antwort
    (Vorbild: die nullable Betriebsspalten von `KiAnfragen`). Das Panel zeigt einen Gedankenstrich.
  - **Die zweite Suchwelle wuchs von 16 auf 24 Provider, das Wanduhr-Budget nicht.** Acht der neun neuen
    Kategorien scannen eine Langtext-Spalte und sind zu Recht `Heavy`. Der öffentliche Bestand ist klein, aber die
    Richtung ist benannt: läuft `SearchOptions` (8 s) ab, fällt der phonetische Nachschlag der bestehenden
    `SideIndexed`-Kategorien zuerst weg, und `SearchResults.Incomplete` sagt es. Die Antwort wäre dann ein größeres
    Budget oder eine dritte Welle, nicht ein `Heavy` weniger.
  - **Ein Suchbegriff aus gewichtslosen Zeichen hätte den ganzen Bestand ausgeliefert.** Ein kultur-sensitiver
    Vergleich hält eine Zeichenkette aus lauter Formatzeichen — drei Nullbreiten-Leerzeichen genügen — für einen
    Treffer an Position 0, also für **jede** veröffentlichte Zeile. `PublicSearchRules.Normalise` streicht Steuer-,
    Format-, Surrogat- und Privatnutzungs-Codepunkte **vor** der Mindestlänge.
  - **Die Ticket-Reaktionszeit misst bis zur ersten Antwort eines Menschen.** Die Phase-11-Eingangsbestätigung
    entsteht in dem `SaveChanges`, das auch das Ticket anlegt, und trägt deshalb dessen Zeitstempel — ohne
    `TicketRules.IsHumanAgencyReply` (striktes `>`) meldet das Panel auf jeder Installation mit aktiver Vorlage
    einen perfekten Schalter.
  - **Aufrufzahlen sind `null`, wenn der Leser die Ausschreibungsliste nicht führen darf**, nicht `0`. Der Zweig
    ist heute unerreichbar — wer `RequireClassifiedRead` besteht, besteht auch die Listen-Zielgruppe —, und genau
    das ist als Unit-Fakt behauptet: den Tag, an dem jemand das Panel-Gate weitet, macht ein roter Test sichtbar,
    kein Leck. Dasselbe für den Aktenfilter über der Rangliste.
  - **Nicht** registriert, mit Grund: `TrashService`, die vier Zeitstrahl-/Chronik-Stellen, `AuditEntityDisplay`,
    `WatchlistRecordRollup`, `RecordsReference`, `LinkService.knownTypes`, `PublicRoutes`/`robots.txt`,
    `PublicVisibility`, `NotificationType`, Discord-Push, Aktenzeichen-Präfix, Migration. Jede hängt an etwas, das
    Durchsuchbarkeit nicht ändert (`ISoftDelete` plus Restore-Methodengruppe · eine `AuditLog`-Zeile mit Fan-out
    auf einen **Elternteil** · eine `IAuditable`-Instanz zum Schreibzeitpunkt · das Ziel einer manuellen
    Verknüpfung), und alle neun Typen stehen längst dort, wo sie hingehören.
