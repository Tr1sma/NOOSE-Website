# Services-Layer — Detailregeln

> **Lies das, bevor du `AgentSelection` „aufräumst", an `GamificationService`/der Bestenliste
> arbeitest oder eine Suchkategorie hinzufügst.**
> Die Kurzfassung dieser drei Themen steht in `CLAUDE.md`; hier stehen die Begründungen und
> die Ausnahmelisten, die man sonst versehentlich glattbügelt.

## Bewusste Ausnahmen von `AgentSelection` — nicht „aufräumen"

- **Bewusste Ausnahmen von `AgentSelection`** — nicht „aufräumen":
  `MentionService` (Partner bleiben erwähnbar) · `GetAllAsync`/`GetPendingAsync` (Admin-Roster mit eigenen
  Tabs für TL/Partner/Gekündigte) · `PartnerShareService.GetSelectablePartnersAsync` (die Inverse) · alle
  ID→Codename-Wörterbücher (müssen gekündigte Akteure in historischen Zeilen auflösen) ·
  `NotificationService.NotifyManyAsync`, `FollowupDueWorker`, `WatchlistFanout` (filtern eine **übergebene**
  Empfängerliste, kein Roster — Partner müssen Freigabe-/Chat-Benachrichtigungen weiter erhalten) ·
  `AnnouncementService` Bestätigungs-Zähler (ohne Status-Klausel, sonst fällt die Zeile eines gekündigten
  Agenten aus `TotalCount`) · `CounterIntelEventLoader` (braucht jeden User, er *erkennt* TL-Zugriffe).

## Bestenliste: der Rang-Boden liegt NICHT in `AgentSelection`

- **Bestenliste: der Rang-Boden liegt NICHT in `AgentSelection`.** `GamificationService` teilt das Ergebnis in
  zwei Slices (`LeaderboardView.Ranked` / `.OutOfCompetition`): ab `Rank.SupervisorySpecialAgent` wird ein Agent
  **gelistet, aber nicht gewertet** (`Position = 0`), weil das Ranking für die Führung bedeutungslos ist. Der Boden
  (`GamificationService.LeadershipFloor`, Vorbild `DocumentAccessService.cs`) partitioniert eine *schon
  autorisierte* Menge auf einer zweiten Achse — `OnlySelectable()` bleibt die einzige Autorität darüber, wer
  überhaupt **gelistet** wird. Ihn nach `AgentSelection` zu ziehen färbt `AgentSelectionTests` rot und leert jeden
  Picker. Drei Details, die aussehen wie Schlampigkeit und keine sind: der Boden ist **rang-only** und lässt
  `IsAdmin` weg (ein Admin mit Rang 2 spielt weiter mit) — bewusst enger als `IsLeadership()`; **ein** Prädikat,
  negiert für die andere Slice, weil `Agent.Rank` nullable ist und ein `>=`/`<`-Paar bei `null` auf **beiden**
  Seiten `false` ist; und `topN` deckelt **jede Slice für sich**, sonst verdrängen drei Führungszeilen das Podium.
- **Wortwahl der Bestenlisten-Ankündigung folgt dem Intervall** (`TopAgentPeriodDisplay.For`): 7 Tage →
  „der Woche (KW nn)", 28–31 → „des Monats" (**ohne Monatsnamen** — das Fenster ist rollierend, kein Kalendermonat),
  sonst Tag/Quartal/Jahr/„letzte N Tage". Der `Marker` derselben Zeile ist der Dedupe-Schlüssel der
  Personalakten-Vermerke (`Text.Contains`), deshalb: **das 7-Tage-Band ist byte-eingefroren** (`KW 36/2026`,
  **ungepolstert** — `IsoWeekPeriod.Label` polstert und darf hier nicht wiederverwendet werden), jeder Marker ist
  Teilstring seiner `NotePhrase`, beginnt nie mit einer Ziffer (`4 Tage …` steckt in `14 Tage …`) und trägt außer
  beim Wochen-Band den **Lauftag** — ein Kalenderlabel kollidiert, weil zwei Läufe dasselbe Intervall passieren
  können. `FileNotesAsync` sieht ausschließlich **eigene** Vermerke (Text-Präfix) und prüft neben dem Marker einen
  `EntryDate`-Boden: Vermerke entstehen **vor** dem Post, und ein
  fehlgeschlagener Post lässt `LastRun` absichtlich stehen — ohne den Boden legt ein kaputter Webhook pro Tag einen
  neuen Vermerk an. `GamificationPeriodDisplay` („7 Tage"/„30 Tage") ist ein **Filter**-Label und wird mit diesem
  Auszeichnungs-Vokabular **nicht** zu einem Helfer verschmolzen.

## Globale Suche — die sechs Regeln hinter dem Orchestrator

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
