# Öffentliche Fahndung — Ausschreibung, Board, Archiv, Sachfahndung, Einspruch

> **Lies das, bevor du `OeffentlicheFahndung`, `PublicWantedService`, `/gesucht`, `/gefasst`,
> das Fahndungsposter, den Foto-Endpoint oder `/buerger/einspruch` anfasst.**
> Kernfallen: der **Unterdrückungsgürtel** (zweite Abfrage, nie Unterabfrage — `IgnoreQueryFilters()`
> gilt kompilierungsweit), **ein** Speicherpfad + **ein** Cache-Schlüssel, und die Guard-Reihenfolge
> Schreibrecht **vor** Rangprüfung.
> Grundlagen: [oeffentlich-grundlagen.md](oeffentlich-grundlagen.md) · Geld: [oeffentlich-geld.md](oeffentlich-geld.md)

## Phase 4 — Die öffentliche Ausschreibung

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
    `/gesucht/{Aktenzeichen}/foto` ist die einzige anonyme Dateiroute **des öffentlichen Bereichs** (app-weit
    gibt es eine zweite, `/system/logo` in `SystemEndpointRouteBuilderExtensions`): die Autorisierung
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
    gepflegt. **Achtung, dieser Satz war falsch:** die Liste verhindert das Demo-Principal *nicht*, weil
    `DemoAwareAuthenticationStateProvider` es routen-unabhängig liefert — und `DEPLOYMENT-DEMO.md` verspricht
    das auch ausdrücklich so ("jeder anonyme Besucher wird auf den read-only Demo-Agenten geschaltet"). Die
    Präfixliste ist reine Konsistenz. `/gesucht` stand dort
    von Hand; `/gefasst` ist eine **Geschwister**-Route, kein Kind davon, und wäre genauso durchgerutscht. Ebenso
    gibt `PartnerRoutes.IsAllowed` für jede öffentliche Route `true` zurück: ein Partner kann dieselbe Seite
    abgemeldet öffnen, die Sperrmeldung behauptete also eine Einschränkung, die es nicht gibt.
  - Die öffentlichen Seiten heißen `WantedHub`/`WantedProfile`/`WantedArchiveHub`/`WantedPoster`, **nicht**
    `WantedBoard`: `Services/WantedBoard.cs` ist eine global importierte statische Klasse, und `/fahndung` bleibt
    die interne Seite.


## Phase 5 — Ausbau (Warnhinweise, Archiv, Poster, Ablauf, Aufrufzähler, Discord)

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

## Phase 13a — Sachfahndung (Fahrzeuge & Waffen)

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

## Phase 13b — Bürger-Einspruch

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
