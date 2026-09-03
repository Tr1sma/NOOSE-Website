# Redaktionelle Außendarstellung — Organisationen, Presse, Warnungen, Recht, Berichte, Gefahrenlage, Zahlen

> **Lies das, bevor du ein öffentliches Profil, eine Pressemitteilung, eine amtliche Warnung,
> einen Gesetzesauszug, einen Lagebericht, die Gefahrenlage-Ampel oder die Startseiten-Zahlen anfasst.**
> Kernregeln: **nach außen geht die Gefahrenstufe, nie der rohe Score** (`PublicPageScanTests.InternalMarkers`
> führt `"ThreatScore"` wörtlich); **Titel und Rumpf sind mitgeschnappt** — neben einem Publizieren-Knopf
> darf „Entwurf speichern" nichts ändern, was schon draußen steht; das **Publikationsdatum wird einmal
> geprägt** (`PublishedAt ??=`) und beim Zurückziehen geräumt; `null` heißt „wird nicht veröffentlicht",
> `0` ist eine Aussage.
> Grundlagen: [oeffentlich-grundlagen.md](oeffentlich-grundlagen.md) · Suche: [oeffentlich-suche.md](oeffentlich-suche.md)

## Phase 12 — Organisationsprofile & Gefahrenlisten

- **Phase 12 (Organisationen & Gefahrenlisten) — was daran anders ist:**
  - **Nach außen geht die Gefahrenstufe, nie der rohe Score** — und das ist mechanisch gesichert, nicht nur
    verabredet: `PublicPageScanTests.InternalMarkers` enthält wörtlich `"ThreatScore"`, eine öffentliche
    Seite, die ihn nennt, macht den Build rot. Die Zahl ließe die Formel aus `AlgoPlan.md` rückwärts rechnen,
    und die Score-Konfiguration steht in `NeverPublic` als „Anleitung zur Umgehung". Festgehalten wird die
    Stufe beim Publizieren, nachgezogen nur auf Knopfdruck.
  - **Zwei Statusachsen auf einer Zeile.** `Status` (`PublicProfileStatus`) entscheidet allein die
    öffentliche Sichtbarkeit, `Einordnung` (`PublicFactionStanding`) ist das Etikett beobachtet/verboten. Ein
    Feld für beides könnte eine Publikation nicht zurückziehen, ohne die Einordnung zu verlieren. Die
    Einordnung wird **nie** aus `Faction.Classification` abgeleitet — die interne Einstufung nach außen zu
    spiegeln veröffentlichte, woran die Behörde arbeitet. Erlaubt ist nur, was in
    `PublicFactionStandingDisplay.All` steht (`RequireKnownStanding`), sonst stünde draußen eine rohe Zahl.
  - **Keine Rang-Weiche, deshalb zwei Guards statt drei.** Alles ab `SeniorSpecialAgent(3)`:
    `Permission.RequirePublicFactionProfileWrite` (interner Agent mit Schreibrecht — `RequireWriteAccess`
    allein ließe ein Bürgerkonto durch) **vor** `RequireHighestClassification`, und
    `RequirePublicFactionProfileRead` (Rang ≥ 3 oder Aufsicht). Die Antrags-Weiche der Fahndung existiert
    dort nur, weil Rang 1–2 Entwürfe anlegen darf; hier gibt es diesen Zweig nicht, also keinen fünften
    `RequestType`, keine Spalte auf `Antraege`, keinen Posteingang, kein Badge — und ein dritter
    „RecordRead"-Guard hätte niemanden einzulassen.
  - **Ein Hub, keine Detailseite.** Name, Einordnung, Stufe und Kurzbeschreibung stehen auf der Karte; eine
    Detailseite wiederholte dieselben vier Felder. Kein Aktenzeichen-Präfix, kein `CaseNumberCounter`. Die
    Ausschreibung verlinkt bewusst **nicht** auf die Organisation: `OeffentlicheFahndung.FraktionId` ist ein
    interner FK, und den Fraktionsnamen beim Rendern nachzulesen wäre ein Live-Blick statt eines Snapshots.
  - **Eigener Cache-Schlüssel, ein Speicherpfad.** `SaveAndInvalidateAsync` ist die einzige Stelle mit
    `SaveChangesAsync` **und** `cache.Remove`; `PublicFactionProfileCacheDisciplineTests` hält das per
    Dateiscan fest, samt „kein zweiter Produktionsdateiname kennt den Schlüssel" und „wer die Profiltabelle
    schreibt, ist dieser eine Dienst". Eigener Schlüssel neben dem Fahndungs-Snapshot, weil es eine andere
    Tabelle ist — das Phase-5-Argument gegen einen zweiten Schlüssel galt Board **und** Archiv aus
    *derselben* Tabelle.
  - **Der Unterdrückungsgürtel ist wieder eine zweite Abfrage** (`OpenFactionsAsync`), nie eine
    Unterabfrage: `IgnoreQueryFilters()` gilt kompilierungsweit. `p.Faction` als Navigation ist ebenso
    unbrauchbar — sie erbt den Filter und ist für eine gelöschte Akte `null`. Zusätzlich zieht
    `RetractForRecordAsync` die Zeile offline, gerufen aus `FactionService.RefreshAsync` (sobald die Akte VS
    wird) und `.DeleteAsync`; einen `FactionMergeService` gibt es nicht.
  - **`/gefahr/personen` hat keinen eigenen Lesepfad**, es projiziert
    `IPublicWantedService.GetBoardAsync().Cards` — die Stufe steht dort schon, hinter demselben Gürtel. Eine
    zweite Abfrage müsste ihn wiederholen, genau die Phase-4-Falle. Die Ranking-Regel steht einmal, in
    `Services/Public/HazardRanking.cs` (Stufe absteigend, Publikationsdatum als Gleichstand-Entscheider,
    `HazardLevel.No` fällt heraus, Deckel 25) — zwei Oberflächen lesen sie, ausgeschrieben driftet sie. Der
    **Deckel wird auf der Seite genannt**, weil ein stiller Schnitt sich wie Vollständigkeit liest. Die
    Personenliste filtert vorher auf `PublicWantedKind.Fahndung`: eine Vermisstenmeldung und ein
    Zeugenaufruf tragen ebenfalls eine Gefahrenstufe, und beide unter „gefährlichste Personen" zu listen
    wäre eine Anschuldigung, die die Ausschreibung nie erhoben hat. Heute wird nur `Fahndung` ausgegeben —
    die Zeile ist für Phase 13 da, die `Fahrzeug` und `Waffe` bringt.
  - **Gates je Datenmenge, verschachtelt:** `/organisationen` an `Organisationen`, `/gefahr/fraktionen` an
    `Gefahrenlisten` **und** `Organisationen`, `/gefahr/personen` an `Gefahrenlisten` **und** `Fahndung`
    (Präzedenz `GetPublishedPhotoAsync`: „das Modul der Menge, in der es die Zeile gefunden hat").
    `PublicModuleGate` nimmt genau ein Modul; zwei verschachtelt zeigen je den eigenen Offline-Text, was
    genau richtig ist — die Meldung sagt, welcher Schalter es war. Zurückziehen und Löschen fragen das Modul
    **nie**.
  - **Kein Discord-Push.** Ein Organisationsprofil ist kein Handlungsaufruf, und ein Kanal-Post wäre eine
    bleibende Anschuldigung gegen eine ganze Fraktion, die ein Rückzug nicht zurückruft (Präzedenz: eine
    Senkung des Kopfgelds bleibt still). Damit kein neuer `NotificationType`.
  - **Die Inhaltsprüfung sitzt im Publish-Rumpf** (Klartext vorhanden, keine `@{…}`-Erwähnung, kein barer
    `{{`-Opener wie in `WarnhinweisService`) — anders als in Phase 4 gibt es nur *einen* Eingang, also
    genügt eine Stelle. Ein **Entwurf** darf eine Erwähnung tragen, er ist intern; eine laufende Publikation
    wird beim Speichern erneut geprüft. Der Schreibpfad prüft die **Aktensichtbarkeit immer**, nicht nur bei
    laufender Publikation — sonst schriebe, wer die Id eines Entwurfs kennt, gegen eine Verschlusssache
    (Präzedenz `SetHintsAsync`).
  - **Kein Unique-Index auf `FraktionId`** — mit Soft-Delete sperrte er die Fraktion für immer
    (Phase-3-Lektion vom Seiten-Slug). „Ein lebendes Profil je Fraktion" ist eine Dienst-Regel, und
    **Wiederherstellen ist der zweite Weg, sie zu verletzen**: es prüft die Adresse erneut und bringt das
    Profil als **Entwurf** zurück, damit ein Rückgängig nichts nebenbei wieder veröffentlicht.
  - **`FeedbackPageTabs` gilt auch für einen `/fahndung`-Abschnitt**, nicht nur für `/einstellungen` — der
    Wächter verlangt jeden `MergedPageSections`-Slug im Feedback-Picker. Und `TrashServiceTests` vergleicht
    die Papierkorb-Slugs **der Reihenfolge nach** gegen `TrashService.Kinds`: die neue Zeile muss dort
    stehen, wo ihre `Source` steht.
  - **Nicht registriert, mit Grund:** `RecordsReference`/`LinkService` (das Profil ist kein
    Verknüpfungsziel, sondern eine Eigenschaft seiner Fraktion) · `PublicRoutes`/`robots.txt` (`/gefahr`
    steht seit Phase 2 in `ExtraPrefixes`, `/organisationen` ist eine Modul-Nav-Route und damit unabhängig
    von `Available` schon in `Prefixes`) · `CaseNumberCounter` · kein Foto, kein Ablaufdatum, kein
    Aufrufzähler (ein Hub ohne Detailseite hat nichts zu zählen).

## Phase 14a — Pressemitteilungen

- **Phase 14a (Presse) — was daran anders ist:**
  - **Ein Aktenzeichen statt eines Slugs, und ein Entwurf hat deshalb baulich keine Adresse.** Der
    Seiten-Slug aus Phase 3 darf **nicht** unique indexiert werden (eine soft-gelöschte Zeile behält ihre
    Adresse); eine Zählernummer wird nie wiederverwendet, also ist `Aktenzeichen` unique — und
    `RestoreAsync` braucht anders als bei einer redaktionellen Seite keine Adressprüfung. Geprägt wird es
    bei der **ersten** Publikation (Präzedenz `OeffentlicheFahndung`), Präfix **`PM`**. Folge: ein Entwurf
    ist nicht erreichbar, weil es keine Adresse gibt, nicht weil ein Status ihn versteckt.
  - **Titel und Teaser sind mitgeschnappt, anders als bei einer redaktionellen Seite.** `OeffentlicheSeite`
    hält nur den Rumpf in zwei Spalten und liest den Titel live; dort ist das folgenlos. Eine
    Pressemitteilung ist eine **datierte Aussage** — wer später einen Tippfehler korrigiert und dabei die
    Schlagzeile anfasst, hätte mit „Entwurf speichern" die längst veröffentlichte Überschrift geändert, ohne
    einen Publizieren-Klick. Deshalb `InhaltTitel`/`InhaltTeaser` neben `InhaltHtml`, alle drei **nur** von
    `PublishAsync` geschrieben, und `PressEdit.DraftDiffers` vergleicht alle drei — sonst nennt das Panel
    eine veraltete Schlagzeile aktuell.
  - **Der Auto-Entwurf bei „gefasst" kommt aus einem festen Skelett im Code** (`PressDraftText`),
    ausdrücklich **nicht** aus einer `OeffentlicheVorlage`. Vier Fehlpassungen des 4. Token-Systems, jede
    für sich hinreichend: `PublicTemplateRenderer` encodet bewusst **nichts** (eine Pressemitteilung ist
    HTML, keine Klartext-Nachricht), er schwärzt `NAME` zu `███████` (eine Festnahmemeldung will den Namen
    *zeigen*), `BUERGER` fällt auf „Bürger/in" zurück (es gibt keinen Bürger) und `MaxLength` hängt an
    `TicketRules.MaxMessageLength`. Ein zusätzlicher Token im geteilten Renderer ginge in **jeder**
    Bürgervorlage unexpandiert nach draußen. `PressDraftText` encodet je Einsetzung mit
    `WebUtility.HtmlEncode` (dasselbe wie `BewerbungTemplateRenderer`) und wickelt jede Zeile in ein `<p>`;
    Umlaute werden dabei zu Zahlentitäten, das ist gewollt und hausüblich.
  - **`DiscordGepushtAm` ist der Idempotenz-Token, weil es hier keinen Statuswechsel gibt, der die Rolle
    übernehmen könnte.** Beim Ablauf-Worker und bei der Belohnung ist der Statuswechsel selbst der Token;
    Zurückziehen → Korrigieren → Wiederveröffentlichen ist dagegen ein legitimer Rundgang und darf den
    Kanal nicht ein zweites Mal beschicken. Der Push sitzt **nach** dem Commit und nimmt **nur** einen
    `PublicPressCard` — der Record kann Autor, Aktenbezug und interne Id strukturell nicht tragen.
  - **`PublicPressPublished` ist die erste routbare öffentliche Kategorie ohne Vorbehalt.** Alles aus
    Hinweisen, Tickets und Einsprüchen ist bewusst nicht routbar, weil die Meldung einen Bürger nennt; eine
    amtliche Verlautbarung nennt keinen und ihr Link ist eine dauerhafte öffentliche Adresse.
  - **Der Auto-Entwurf fragt die gespeicherte Modul-Wahl, nicht `RequireEnabledAsync`.** Letzteres faltet
    den Not-Aus ein, und der ist eine vorübergehende Störung der öffentlichen Seiten — kein Grund, einen
    internen Entwurf zu verlieren (Präzedenz: der Beleg-Lesepfad aus Phase 9). Bei dauerhaft
    ausgeschaltetem Modul entsteht **kein** Entwurf: er existiert, um veröffentlicht zu werden, und einer
    je Festnahme, den niemand veröffentlichen kann, ist Rauschen. Der Aufruf steht nach dem Commit in
    `CapturedAsync` und in `try/catch` — eine ausgefallene Bequemlichkeit nimmt keine Festnahme zurück.
    Kein DI-Zyklus: `PressReleaseService` kennt `IPublicWantedService` nicht, es bekommt den Card übergeben.
  - **`CreateCaptureDraftAsync` hält `RequirePublicWantedWrite`, nicht `RequirePressWrite`** — die Autorität,
    die die Ausschreibung schließt, nicht die, die eine Mitteilung veröffentlicht. `RequirePublicWantedWrite`
    hat **keine Rangschwelle**; mit der Führungsprüfung wäre der Automatismus für jede Festnahme unterhalb
    Rang 4 ausgefallen, und weil der Hook in `try/catch` steht, **lautlos**. Der Entwurf bleibt intern, und
    veröffentlichen darf ihn weiterhin nur die Führung. Nicht „aufräumen".
  - **Keine Vorschau-Route**, Folge der Aktenzeichen-Entscheidung: sie wäre die einzige öffentliche Route,
    die für eine unveröffentlichte Zeile antwortet. Der Editor rendert denselben Stand, der veröffentlicht
    würde; `?vorschau=1` bleibt der redaktionellen Seite, die ihren Slug von Anfang an hat.
  - **Zurückziehen behält Inhalt *und* Nummer**, Löschen erst danach, Wiederherstellen kommt als Entwurf.
    Zurückziehen und Löschen fragen das Modul **nie** — Publizieren braucht ein lebendes Modul,
    *De*publizieren nie, sonst machte der Not-Aus das Zurückziehen unmöglich.
  - **`Permission.RequirePressWrite`** = `IsInternalAgent()` → `MayWrite()` → `IsLeadership()`, in dieser
    Reihenfolge: ein Bürgerkonto trägt gar keinen Rang-Claim, und die Rangprüfung allein ließe Aufsicht und
    Demo-Principal bis zum Prägen einer Nummer laufen. Gelesen wird mit `RequireClassifiedRead` (Vorbild
    `RequirePublicPageWrite`). Rang 3 publiziert Ausschreibungen, aber die Stimme der Behörde bleibt bei
    der Führung — deshalb gibt es hier **keine** Antrags-Weiche wie in Phase 4.
  - **Das Veröffentlichungsdatum wird einmal geprägt, wie das Aktenzeichen, und beim Zurückziehen geräumt.**
    `PublishAsync` stempelte anfangs bei jedem Aufruf neu — eine im März veröffentlichte Mitteilung trug nach
    einer Tippfehler-Korrektur den heutigen Tag, und weil `/presse` nach genau diesem Feld sortiert, stand sie
    danach oben. Also `PublishedAt ??=` und `PublishedById ??=`, und `RetractAsync` setzt beide auf `null`:
    eine Mitteilung, die vom Netz war und wieder rausgeht, ist ehrlich neu datiert. Panel-Spalte deshalb
    „Veröffentlicht", nicht „Zuletzt veröffentlicht". Gleiches gilt für die Warnung aus 14b; bei Fahndung und
    redaktioneller Seite bleibt das Stempeln, weil dort kein Datum nach außen geht.
  - **Nebenbefund, mitbehoben: `/lageberichte` war als indexierbar deklariert.** Intern liegen dort
    `LegacyRouteRedirect` und die führungs-only Druckseite `/lageberichte/{Id}` mit den eingestuften
    Aggregaten; gleichzeitig nannte `PublicModules.SituationReports` die Route als `NavRoute`, und
    `PublicRoutes.Prefixes` sammelt Nav-Routen **ohne** `Available`-Filter ein. Damit stand
    `Allow: /lageberichte` in `robots.txt`, der `noindex`-Header fehlte, `DemoModeMiddleware` schloss die
    Route aus und `PartnerRoutes.IsAllowed` gab `true`. Die öffentliche Route heißt jetzt **`/berichte`**,
    das Label bleibt „Lageberichte". **Ein Modul-`NavRoute` ist damit auch eine Indexierungs-Aussage** —
    eine Route, die intern schon belegt ist, darf dort nicht stehen.
  - **`PressCacheDisciplineTests` ist der dritte Cache-Wächter** (nach Fahndung und Organisationsprofilen):
    ein `SaveChangesAsync(`, ein `cache.Remove(`, kein zweiter Produktionsdateiname kennt den Schlüssel, und
    wer `db.Pressemitteilungen` schreibt, ist dieser eine Dienst. Eigener Schlüssel, weil eigene Tabelle.
  - **Nicht registriert, mit Grund:** die vier Zeitstrahl-/Chronik-Stellen sowie `RecordsReference` und
    `LinkService` — eine Pressemitteilung hängt an keiner Akte, es gibt keinen Elternteil, auf den sie
    fan-in könnte (Präzedenz `Ticket`). `PublicRoutes`/`robots.txt` brauchten nichts: `/presse` kommt aus
    der Modul-`NavRoute`.

## Phase 14b — Amtliche Warnungen & freigegebene Gesetzesauszüge

- **Phase 14b (Warnungen & Recht) — was daran anders ist:**
  - **`OeffentlicheWarnung` ist nicht `Warnhinweis`.** Die eine ist eine Durchsage mit Rumpf und Ablauf,
    die andere die Chip-Werteliste an einer Ausschreibung aus Phase 5. Zwei Tabellen, zwei Bedeutungen,
    zwei Nachbar-Abschnitte in `/einstellungen` (Slug `warnungen` neben `warnhinweise`) — beide
    Panel-Texte sagen deshalb, was sie *nicht* sind.
  - **Ein Hub, keine Detailseite, kein Aktenzeichen** (Präzedenz Phase 12): eine Warnung besteht aus
    Titel und Text, eine Detailseite wiederholte genau diese zwei Felder. Die Karte trägt den ganzen
    Rumpf, also gibt es keinen Präfix, keinen `CaseNumberCounter` und keine Nichtgefunden-Route mit
    ihrem `noindex`. Deckel 20 statt 50 wie bei der Presse — jede Karte bringt ihren Rumpf mit — und
    **der Deckel wird auf der Seite genannt**.
  - **Titel und Rumpf sind mitgeschnappt, `GueltigBis` bewusst nicht.** Die 14a-Regel gilt: neben einem
    Publizieren-Knopf darf „Entwurf speichern" nichts ändern, was schon draußen steht. Das Ablaufdatum
    ist die begründete Ausnahme und hat **keine** zweite Spalte — Verlängern ist keine neue Aussage, und
    ein Republizieren-Zwang ließe eine Warnung sterben, weil niemand den zweiten Knopf drückt (Präzedenz:
    das live gelesene Warnhinweis-Label aus Phase 5). Folge, gewollt: ein Vergangenheitsdatum im Entwurf
    nimmt eine laufende Warnung sofort offline.
  - **Der Filter ist die Kontrolle, und hier gibt es gar keinen Worker.** Der Ablauf-Worker aus Phase 5
    existiert, damit der *interne* Status ehrlich bleibt und offene Anträge geschlossen werden — eine
    Warnung hat weder das eine noch das andere. Der Preis ist benannt: sie steht bis zu ein Cache-Fenster
    (10 s) zu lange, was ein in Tagen gemessener Ablauf nicht merkt, und ihr Status bleibt
    „veröffentlicht"; das Panel schreibt „abgelaufen" daneben. **Löschen verlangt trotzdem erst das
    Zurückziehen** — der Status ist die Aussage, nicht der Filter. Publizieren mit einem Datum in der
    Vergangenheit wird abgewiesen: der Lesepfad filtert auf dasselbe Feld, die Aktion meldete sonst
    Erfolg und änderte nichts Sichtbares.
  - **`Services/Public/PublicExpiry.cs` hält „ein gewählter Tag zählt noch mit"** — herausgezogen aus
    `PublicWantedService`, wo die Regel privat stand. Zwei Kopien driften, und eine Warnung, die mittags
    stirbt, liest sich wie ein Fehler statt wie eine Entscheidung.
  - **Kein Discord-Push, die Umkehrung von 14a.** Eine Pressemitteilung ist routbar, weil ihr Link eine
    dauerhafte Adresse ist; eine Warnung läuft ab, und der Post behauptete danach weiter Gefahr,
    unkorrigierbar — dasselbe Argument, das `PublicWantedExpired` von der Allowlist fernhält. Damit kein
    neuer `NotificationType`.
  - **`/recht` bekommt einen eigenen `IPublicLawService`, obwohl er dünn ist.** Leitsatz 3: ein
    öffentlicher Lesepfad liest nie über einen internen Listendienst. `LawService.GetListAsync`
    beantwortet eine andere Frage (samt Partner-Achse) und darf sich weiten, ohne dass jemand an die
    Öffentlichkeit denkt. Freigegeben wird über **`SetPublicAsync` als einzigen Schreibpfad** von
    `Law.IsPublic`; der Gesetzes-Editor unter `/gesetze` pflegt weiter nur den Text.
  - **Die Gesetzestabelle ist die erste öffentliche Datenquelle mit einem zweiten Schreiber.** Bei
    Fahndung, Organisationsprofilen und Presse heißt die Regel „ein Dienst schreibt die Tabelle";
    `Gesetze` gehört dagegen `ILawService`. Hier heißt sie deshalb **jeder Schreiber verwirft den
    Snapshot**: `LawService` ruft nach `CreateAsync`, `RefreshAsync` und `DeleteAsync`
    `InvalidatePublicViewAsync()` — sonst stünde ein korrigierter oder gelöschter Paragraf ein volles
    Cache-Fenster lang draußen. Auch nach `CreateAsync`, wo es beweisbar unnötig ist: „dieser Paragraf
    kann nicht öffentlich sein" ist eine Aussage über heute. Kein DI-Zyklus — `PublicLawService` kennt
    `ILawService` nicht. `InvalidatePublicViewAsync` läuft durch denselben Choke-Point mit `db == null`
    (Präzedenz Phase 6), damit die Datei bei **einem** `cache.Remove` bleibt.
  - **`PublicLawCacheDisciplineTests` ist deshalb anders gebaut als seine drei Geschwister.** Ein Scan
    „wer die Tabelle nennt und speichert, ist der eine Dienst" wäre dreifach falsch-rot:
    `SearchIndexBackfillWorker`, `LinkService` und `PartnerShareService` **lesen** Paragrafen und
    schreiben anderes. Sie stehen als **Leser mit Begründung** in einer Liste, die Schreiber in einer
    zweiten (Muster `PublicVisibility`) — eine neue Datei mit `db.Laws` + `SaveChangesAsync` macht den
    Build rot, bis jemand entschieden hat, was sie ist.
  - **Kein Deckel auf `/recht`**, anders als auf jedem anderen öffentlichen Hub: ein Gesetzbuch soll
    vollständig sein, „die 50 jüngsten Paragrafen" wäre bei Recht eine falsche Aussage. Der
    **Gesetzestext geht als Klartext raus, nie als `MarkupString`** (`white-space: pre-wrap`,
    Zeilenumbrüche sind seine Formatierung — Präzedenz `OeffentlicheVorlage.Text`); der Warnungs-Rumpf
    ist HTML und läuft über `HtmlCleanup.Clean` beim Speichern **und** beim Publizieren.
  - **Zwei eigene Guards mit heute identischer Bedingung.** `RequireWarningWrite` und
    `RequireLawReleaseWrite` sind beide `IsInternalAgent()` → `MayWrite()` → `IsLeadership()`, in dieser
    Reihenfolge. Getrennt, weil die Meldung den Bereich nennt und zwei Bereiche, die heute dieselben
    Leute einlassen, es morgen nicht müssen. Gelesen wird mit `RequireClassifiedRead`. Freigeben und
    Publizieren brauchen ein lebendes Modul, Zurückziehen und Zurücknehmen **nie**.
  - **`LawService.DeleteAsync` räumt `IstOeffentlich`.** Für einen Paragrafen gibt es heute keinen Weg aus
    dem Papierkorb zurück — aber eine soft-gelöschte Zeile, die ihr Freigabe-Kennzeichen behält, käme an dem
    Tag veröffentlicht zurück, an dem jemand einen baut (Präzedenz: „Wiederherstellen kommt als Entwurf
    zurück"). Das Publikationsdatum von Warnung und Mitteilung folgt derselben Linie, siehe 14a.
  - **`Law` wandert in `PublicVisibility` von `NeverPublic` nach `Publishable`** — das ist die
    eigentliche Aussage der Phase. **Nicht** registriert, mit Grund: die vier Zeitstrahl-/Chronik-Stellen
    sowie `RecordsReference`/`LinkService` — eine Warnung hängt an keiner Akte (Präzedenz `Ticket`,
    `Pressemitteilung`); `PublicRoutes`/`robots.txt` — `/warnungen` und `/recht` kommen seit Phase 2 aus
    der Modul-`NavRoute`, und damit auch `DemoModeMiddleware` und `PartnerRoutes.IsAllowed`.

## Phase 14c — Öffentliche Lageberichte

- **Phase 14c (Lageberichte) — was daran anders ist:**
  - **Der Zeitraum ist die Adresse, nicht ein Aktenzeichen.** `/berichte/2026-08` ist zitierbar, wird nie
    wiederverwendet und ist nicht fälschbar: `Jahr`/`Monat` kommen beim Anlegen **aus dem Anker** und sind
    danach unveränderlich — die Panel-Eingabe trägt sie nicht. Damit kein Präfix, kein `CaseNumberCounter`,
    keine Transaktion. **Kein Unique-Index** darauf (Phase-3-Lektion vom Seiten-Slug: mit Soft-Delete
    sperrte er den Monat für immer); „ein lebender Bericht je Monat" ist eine Dienst-Regel über die
    lebenden Zeilen, und **`RestoreAsync` prüft sie erneut** — nach dem Löschen darf jemand einen neuen
    Text für denselben Monat schreiben, der Papierkorb ist die zweite Tür.
  - **Die Adresse wird geparst, bevor sie nachgeschlagen wird.** `GetByPeriodAsync` geht über
    `ReportPeriod.TryParse` und baut den Schlüssel neu; ohne das läse `2026-8` als zweite Schreibweise
    derselben Seite. `Services/Public/ReportPeriod.cs` ist die einzige Wahrheit von Format, Label und
    Parser (Muster `PublicExpiry`). Der Route-Parameter ist ein **`string`** — Blazor antwortet auf einen
    unparsbaren Wert mit HTTP 500, und an eine öffentliche URL hängt jeder alles.
  - **Der Anker ist nullable, obwohl er beim Anlegen Pflicht ist** — die 13b-Lektion: eine
    **Pflicht**-Navigation wird **INNER** gejoint, also fiele eine Zeile, deren interner Monatsbericht
    gelöscht wurde, lautlos aus `GetAllAsync`, während eine Zählung ohne Navigation sie weiterzählt.
    Nullable ⇒ LEFT JOIN, das Panel schreibt „Monatsbericht gelöscht" daneben, `MySqlTranslationTests`
    hält das `LEFT JOIN` fest.
  - **Kein Löschriegel und kein Rückzugs-Hook auf `SituationReportService.DeleteAsync`.** Der öffentliche
    Text trägt kein einziges Feld des Snapshots, also macht Archiv-Aufräumen ihn nicht falsch (Präzedenz
    14a: eine Pressemitteilung überlebt das Löschen der Ausschreibung, weil sie eine eigene datierte
    Aussage ist). Ein Hook nach dem Muster `RetractForRecordAsync` wäre hier die **schlechtere** Wahl —
    er machte Aufräumen zur stillen Depublikation ohne Grund auf der Zeile. Aus demselben Grund gibt es
    **keinen Unterdrückungsgürtel**: der Lesepfad dereferenziert den Anker nie, also ist die
    `IgnoreQueryFilters`-Falle aus Phase 4/12 baulich nicht erreichbar.
  - **Text, keine Zahlen — und das ist ein Verhaltenstest, kein Namensscan.** Ein interner Monatsbericht
    wird mit `Classified = 4711`, einem Personennamen, einem `NOOSE-P-`-Aktenzeichen und einem
    `/personen/{id}`-Href geseedet; danach wird der **ganze** öffentliche Snapshot serialisiert und darf
    keines davon enthalten, auch nicht die Anker-Id und nicht den Codenamen. Zweite Schicht ist die
    Struktur (`PublicReportCard`/`PublicReportView` können diese Werte nicht tragen), dritte
    `PublicPageScanTests.InternalMarkers` mit `SnapshotJson`, `StatisticsReport`, `DashboardMetrics`,
    `StatisticsTopEntry`, `SituationReportDisplay`, `ISituationReportService` — dieselbe Mechanik, die
    `ThreatScore` seit Phase 12 hält.
  - **Der Anker-Picker liest die Tabelle selbst — `ISituationReportService` wäre ein Objektgraph-Leck.**
    Die öffentlichen Seiten *injizieren* `IPublicReportService`, also baut jeder anonyme Aufruf jeden
    Konstruktor-Parameter dieses Dienstes mit auf; mit dem Statistik-Dienst hängen `IStatisticsService`,
    `IFinancingStatisticsService` und `INotificationService` an `/berichte`. Der Marker-Scan hält
    `ISituationReportService` von den *Seiten* fern und sieht es eine Schicht tiefer nicht. `GetAnchorsAsync`
    liest deshalb vier Spalten direkt — wie `NewForAnchorAsync` ohnehin —, und `GetArchiveAsync` löst je
    Bericht einen Codenamen auf, den ein Picker nicht braucht. **Neue Regel mit Wächter:**
    `PublicSurfaceGuardTests.NoServiceInjectedByAPublicPage_PullsInAnInternalStack` leitet die Dienste aus den
    `@inject`-Zeilen von `Components/Pages/Public/` ab, löst `IFooService` → `FooService.cs` auf und
    **streicht vorher die Kommentare** (Präzedenz `ForeignTokenSystems`). Er meldet einen nicht auflösbaren
    Dienst, statt ihn zu überspringen — sonst leert eine Umbenennung den Scan lautlos.
  - **Der Monatsname ist auf de-DE gepinnt, nicht `CurrentCulture`** — wie `FinancingPeriod`,
    `StatisticsService`, `AttendanceStatisticsService`. Ein Host mit anderer Locale schriebe sonst
    „March 2026“ in eine deutsche Seite, ohne dass etwas rot wird: „August 2026“ liest sich in beiden
    Sprachen gleich, ein Label-Test darauf beweist also nichts. Getestet wird mit März/Oktober/Dezember.
  - **Kein Discord-Push**, anders als bei der Presse: ein Monatsbericht ist keine Eilmeldung, die Nav zeigt
    ihn ohnehin. Damit kein neuer `NotificationType`. Deckel **24** (zwei Jahre) und **auf der Seite
    genannt**; `ByPeriod` über `GroupBy`, nicht `ToDictionary` — die Eindeutigkeit hängt an einer
    Dienst-Regel, nicht an einem Index, und ein werfender Snapshot versteckte **jeden** Bericht.
  - **`Permission.RequirePublicReportWrite`** = `IsInternalAgent()` → `MayWrite()` → `IsLeadership()`, in
    dieser Reihenfolge und als eigener Guard mit eigener Meldung (Präzedenz `RequireWarningWrite`).
    Gelesen wird mit `RequireClassifiedRead`. Titel und Rumpf sind mitgeschnappt, das Publikationsdatum
    einmalig geprägt und beim Zurückziehen geräumt (14a, beide Teile). Publizieren braucht ein lebendes
    Modul, Zurückziehen und Löschen **nie**.
  - **Nicht** registriert, mit Grund: die vier Zeitstrahl-/Chronik-Stellen sowie
    `RecordsReference`/`LinkService` — ein Lagebericht hängt an keiner Akte (Präzedenz `Ticket`,
    `Pressemitteilung`, `OeffentlicheWarnung`); `PublicRoutes`/`robots.txt` — `/berichte` kommt seit 14a
    aus der Modul-`NavRoute`, und damit auch `DemoModeMiddleware` und `PartnerRoutes.IsAllowed`.

## Phase 15a — Gefahrenlage-Ampel

- **Phase 15a (Gefahrenlage) — was daran anders ist:**
  - **Die Ampel ist eine redaktionelle Aussage, und der Trend ist genau die Stufe davor.** Der Plantext
    wollte ihn „aggregiert aus `ThreatScoreHistory`, ohne Aktenbezug"; das ist zweifach unbaubar. Die Reihe
    deckt **jede** Person und Fraktion ab, Verschlusssachen eingeschlossen — ein Aggregat darüber ist
    genau das, was die NOOSEI-Doktrin schon intern verbietet, hier anonym. Und sie exportierte den rohen
    Score in abgeleiteter Form, während `PublicPageScanTests.InternalMarkers` wörtlich `"ThreatScore"`
    führt. Präzedenz ist Phase 12: die öffentliche `Einordnung` einer Fraktion wird nie aus
    `Faction.Classification` abgeleitet. `NeverPublic["ThreatScoreHistory"]` versprach den geplanten
    Trend und nennt jetzt den Grund.
  - **Vier `SystemSettings`-Zeilen statt einer Tabelle** (`GefahrenlageStufe`/`…Einschaetzung`/`…Seit`/
    `…Zuvor`, Präzedenz Not-Aus): es gibt genau eine Gefahrenlage. Gespeichert wird ein **Schlüssel**, nicht
    die Zahl — eine „2" sagt weder in der Einstellungstabelle noch in der Audit-Zeile etwas — und dieser
    Schlüssel ist **nicht** die deutsche Beschriftung, sondern ein eigener ASCII-String
    (`PublicSituationLevelDisplay.Key` neben `.Name`, Präzedenz `WarnhinweisColourChoice` mit „Error"/„Rot"
    und `PublicIconChoice`). Sonst wäre eine Umformulierung von „Erhöht" eine stille Datenmigration: jede
    gespeicherte Zeile hörte auf zu parsen, `/lage` ginge dunkel, und kein Test würde rot — ein Round-Trip
    über `Parse(Name(x))` überlebt jede Umbenennung. `PublicSituationLevelTests` pinnt die vier Schlüssel
    als Literale und prüft die Trennung als *Beziehung*, nie am Wortlaut des Labels.
    Protokolliert wird zweimal, wie beim Not-Aus: die rohen `SystemSetting`-Zeilen über den Interceptor,
    dazu eine `ManualAudit.Row` mit eigenem Typ `"PublicSituation"`, die die Aktion benennt — die trägt
    bewusst die **Beschriftung**, weil sie ein Mensch auf `/nachweis` liest.
  - **`Seit` bewegt sich nur bei einem Stufenwechsel** (14a-Lektion `PublishedAt ??=` in ihrer schärfsten
    Form: die Seite zeigt genau dieses Datum als „seit"). `Zuvor` wird nur dort gesetzt, und ein zweites
    Speichern ohne Unterschied schreibt gar nichts — auch keine Protokollzeile.
  - **`null` heißt Schweigen, und Schweigen ist nicht `Niedrig`.** `GetPublishedAsync` liefert `null` bei
    Modul aus, nie gesetzt und unerreichbarer Datenbank; eine Standardstufe wäre in allen dreien die
    Behauptung „keine Gefahr". Bewusste Abweichung von den anderen öffentlichen Diensten, die eine leere
    Liste zurückgeben — die ist ehrlich „nichts veröffentlicht".
  - **`SetAsync` hat bewusst kein `RequireEnabledAsync`.** Es gibt keinen Entwurf/Publiziert-Schnitt, also
    zwänge ein Gate die allererste Stufe live zu gehen, bevor jemand sie schreiben kann — und weil das
    Ausschalten des Moduls hier das Zurückziehen **ist**, machte es das unumkehrbar. (Kein „einziger
    Publish-Pfad ohne Gate": `PublicPageService.PublishAsync` hat auch keins. Dort ist es allerdings eine
    Auslassung und keine Entscheidung — folgenlos, weil der Lesepfad bei ausgeschaltetem Modul ohnehin
    `PublicPageSnapshot.Empty` liefert. Nicht in dieser Phase angefasst, weil es Phase-3-Verhalten ändert.)
  - **Eigenes Enum, Allowlist beim Lesen.** `PublicSituationLevel` ist bewusst nicht `HazardLevel` (das ist
    `HazardLevelLogic.From(score)`, die Stufe *einer Akte*); ein geteiltes Enum wäre die Einladung, das eine
    aus dem anderen zu rechnen. `PublicSituationLevelDisplay.Parse` ist eine Allowlist, **nie**
    `Enum.Parse`: der Wert kommt aus einer von Hand editierbaren Zeile, und ein Streuwert wäre auf einer
    `[AllowAnonymous]`-Seite ein HTTP 500. Vier unterschiedliche **sichtbare** Farben.
  - **Die Einschätzung ist Klartext**, kein HTML: kein `HtmlCleanup`, kein `MarkupString`, Zeilenumbrüche
    sind die Formatierung. Ein Wächter verbietet `MarkupString` gezielt in `SituationPage.razor` — nur
    dort, weil Warnungs-Hub und Presse-Artikel es legitim benutzen. Deckel 600 Zeichen
    (`SituationRules.MaxNote`), und **zu lang wird gekürzt statt abgelehnt**: die Einschätzung ist der Satz
    unter der Stufe, kein Artikel, und ein abgewiesenes Speichern verlöre nebenbei den Stufenwechsel derselben
    Eingabe.
  - **`SystemSetting` wandert von `NeverPublic` nach `Publishable`**, mit einem Text, der genau die vier
    Schlüssel nennt und festhält, dass jede andere Zeile (Discord-Webhooks, Wartungstext, Theme, Not-Aus)
    das Haus nie verlässt. `PublicVisibilityCoverageTests` wird davon nicht rot — die Entität war schon
    entschieden; deshalb eine bewusste Handänderung.
  - **`EverySettingsRouteOfTheAuditDisplay_NamesAnExistingSection` liest jetzt die Quelle**, nicht die
    `DbSet`s: `AuditEntityDisplay` beantwortet auch Konfigurations-Typen ohne Tabelle (`"PublicArea"`,
    `"PublicSituation"`), und genau die sah die Reflection-Variante nicht.
  - **Nebenbefund, mitbehoben: `robots.txt` matcht nach Zeichenkette, `PublicRoutes.Matches` nach Segment.**
    `Allow: /lage` deckte damit das interne `/lageberichte` mit ab — dieselbe Klasse wie der 14a-Befund, nur
    von der anderen Seite. Im selben Zug fielen `Allow: /hinweis` ⇒ `/hinweise` (der interne Eingang, dessen
    Trennung die Segmentgrenze eben nur im Header leistet) und `Allow: /info` ⇒ `/informanten` auf. Drei
    `Disallow:`-Zeilen (längste Regel gewinnt, RFC 9309) plus ein abgeleiteter Wächter in
    `PublicRoutesTests`: er liest alle `@page`-Routen, nimmt die internen und verlangt für jede, die eine
    Allow-Zeile mitdeckt, eine **echt längere** Disallow-Zeile. **Eine neue öffentliche Route, die Präfix
    einer internen ist, macht damit den Build rot.**
  - **Registriert sind trotzdem zwei:** `MergedPageSections.Settings` (Slug `lage`) und `FeedbackPageTabs` —
    die Ampel bekommt einen eigenen `/einstellungen`-Abschnitt, und `FeedbackPageTabsTests` verlangt jeden
    `MergedPageSections`-Slug im Feedback-Picker. Ohne diesen Satz liest sich die Liste darunter, als hätte
    die Phase gar nichts registriert.
  - **Nicht** registriert, mit Grund: keine neue Entität ⇒ kein `SearchCatalog`, kein
    `WatchlistRecordRollup`, kein Papierkorb, keine der vier Zeitstrahl-/Chronik-Stellen, kein
    `RecordsReference`/`LinkService`, kein `NotificationType`, kein Discord-Push (eine Stufe ist keine
    Eilmeldung, die Nav zeigt sie ohnehin) · `PublicRoutes` — `/lage` ist seit Phase 2 die Modul-`NavRoute`,
    und `Prefixes` sammelt Nav-Routen ohne `Available`-Filter ein, `Allow: /lage` stand also schon in
    `robots.txt`.

## Phase 15b — Öffentliche Zahlen & Startseite

- **Phase 15b (Zahlen & Startseite) — was daran anders ist:**
  - **`null` heißt „wird nicht veröffentlicht", `0` ist eine Aussage.** Alle sechs Felder auf
    `PublicStatistics` sind nullable, und jedes hängt zusätzlich am Modul der gezählten Zeilen
    (`Fahndung`, `FahndungArchiv`, `Hinweise`, `Belohnung`). Ein ausgeschaltetes Modul liefert `null` —
    „0 laufende Fahndungen" wäre eine Behauptung, die niemand aufgestellt hat; dieselbe Entscheidung wie
    bei der Gefahrenlage, hier sechsmal. Die Schalter werden **außerhalb** des Zahlen-Caches gelesen.
  - **Die Fahndungszahlen kommen aus dem Board-Snapshot, nie aus einer zweiten Abfrage** — die müsste den
    Unterdrückungsgürtel wiederholen (Phase-4-Falle, Präzedenz `/gefahr/personen` aus Phase 12).
  - **Vier der sechs Zahlen tragen einen festen RP-Sockel — das ist kein Zählfehler.** Die drei Hinweis-Zähler
    und die Belohnungssumme werden in `LoadAsync` um `PublicStatisticsService.*Baseline` erhöht
    (1.147 / 372 / 94 Hinweise, 1.236.500 $), weil die Seite das *neue Aktenzimmer* einer Behörde ist, die es
    im RP schon länger gibt: „0 $ ausgezahlt" auf der Startseite datiert die Behörde auf den Tag des Deploys.
    Drei Punkte hängen daran und dürfen nicht auseinanderfallen:
    - **Der Sockel steht in `LoadAsync`, nicht in `GetPublishedAsync`.** Nur so erbt er die beiden Regeln, die
      für die gezählten Zeilen schon gelten: unerreichbare Datenbank ⇒ `null` (der nackte Sockel wäre eine
      Zahl, wo „wir können es nicht sagen" die Wahrheit ist), Modul aus ⇒ `null`. `AnUnreachableDatabase_…`
      und `TheRewardModuleOff_…` prüfen genau das.
    - **Alle vier bewegen sich zusammen, und die Trichter-Ordnung bleibt erhalten** (empfangen ≥ bestätigt ≥
      zur Ergreifung geführt) — eine Auszahlungssumme ohne die Hinweise dahinter liest sich als erfunden.
      Deshalb drei getrennte Sockel statt eines geteilten.
    - **Nur diese eine Oberfläche.** `IPublicKpiService` (Admin-Panel), die Belohnungs-Ansichten und jede
      Kassenbuchung stehen weiter auf den echten Zeilen; NOOSEI liest über `IStatisticsService`. Nichts
      Internes darf gegen die Startseiten-Zahlen abgeglichen werden.
    Die Tests benennen den Sockel über `Received()`/`Confirmed()`/`Captures()`/`Paid()` statt ihn abzuschreiben
    — Nachjustieren bleibt eine Zeile. **`CapturedNotices` hat bewusst keinen Sockel** (beschreibt weiterhin
    das Board, siehe nächster Punkt), was den Zähler „… führten zu einer Festnahme" über der Kachel
    „Abgeschlossen" stehen lässt; wer das angleichen will, muss den Board-Bezug aufgeben.
  - **Der Gefasst-Zähler steht auf dem Snapshot, nicht in der Archivliste.** `Archive` ist seit Phase 5 auf
    die 100 jüngsten gedeckelt; `Archive.Count` als Kennzahl wäre dort stehengeblieben und hätte sich als
    Vollständigkeit gelesen. `LoadAsync` zählt die Gefassten deshalb in einer eigenen, **ungedeckelten**
    Abfrage über drei Spalten hinter demselben Gürtel und legt sie **je Art** auf
    `PublicWantedBoard.CapturedTotals` ab — je Art, weil `WithoutItems()` sonst den Sach-Anteil nicht
    abziehen könnte. `/gefasst` benennt seinen Deckel seither selbst.
  - **`IPublicWantedService.GetCapturedTotalAsync` ist nicht `GetArchiveAsync().Count`** und trägt dieselben
    zwei Modul-Schalter wie die Liste, die es beziffert.
  - **Ein Dienst ohne Schreibpfad bekommt den umgekehrten Cache-Wächter.** `PublicStatisticsService` hat
    weder `SaveChangesAsync` noch `cache.Remove`: die Zahlen laufen ab und werden neu gezählt, es gibt also
    keine Invalidierungsstelle, die falsch sein könnte. `PublicSurfaceGuardTests.TheStatisticsService_NeverWrites`
    hält genau das fest — anders als die Geschwister-Wächter über Fahndung, Organisationsprofile, Presse,
    Warnungen, Berichte und Gefahrenlage, die „genau ein Speicherpfad" prüfen, und anders als der Recht-Wächter,
    der „jeder Schreiber verwirft den Snapshot" prüft.
  - **`StatTile` ohne `Href` auf öffentlichen Seiten.** Die Kachel navigiert aus einem `@onclick`-Handler,
    der auf einer `[ExcludeFromInteractiveRouting]`-Seite nie läuft; mit `Href` hätte sie Zeigerkursor und
    `role="link"` und täte nichts — dieselbe Klasse stumm toter Bedienung wie `PrintFrame` auf dem
    Fahndungsposter. `NoPublicPage_RendersAClickableStatTile` scannt darauf.
  - **Zwei Ausfallarten, zwei Antworten.** Hinweis- und Belohnungszahlen antworten bei unerreichbarer
    Datenbank mit `null` (15a: wenn wir es nicht sagen können, sagen wir nichts); die Fahndungszahlen
    beschreiben, **was das Board zeigt**, nicht was die Datenbank hält — ein leeres Board mit „0" daneben
    ist die wahrheitsgemäße Beschreibung der öffentlichen Oberfläche. Ein Fehlschlag wird nie gecacht.
  - **`Hinweis` und `HinweisBelohnung` wandern nach `Publishable`** (Handänderung wie `SystemSetting` in
    15a, `Law` in 14b): drei Anzahlen über den **gesamten** Bestand und eine Summe. **Eine Anzahl je
    Ausschreibung geht ausdrücklich nicht** — die wäre ein öffentliches Verzeichnis darüber, wer wie viel
    Aufmerksamkeit auf sich zieht, und über kleine Zahlen wieder zuordenbar.
  - **Die Zählprädikate werden benannt, nicht geschrieben:** `TipRules.ConfirmedRows` und die neue
    `TipRules.CaptureRows`. Deshalb vier `CountAsync`/`SumAsync` statt einer gruppierten Projektion — ein
    `GroupBy` mit bedingten Zählern kann die geteilten Ausdrücke nicht einsetzen und hätte die Definition
    ein zweites Mal in den Zahlen-Dienst kopiert.
  - **Kein Nav-Tab und keine Route.** Die Zahlen sind ein Band auf der Startseite; `/zahlen` wäre eine
    zweite Wahrheit über denselben Inhalt (Muster `Kopfgeld`). Damit kein `PublicRoutes`-Eintrag, keine
    `robots.txt`-Zeile und kein Präfix, das mit einer internen Route kollidieren könnte.
  - **`Landing.razor` liest je Quelle einzeln** (fünf Dienste, fünf Fänger): ein einzelner `try` um alles
    hätte wegen der ersten kaputten Quelle jeden Abschnitt geleert. Der Schalterstand fällt bei Ausfall auf
    „alles aus" zurück. Jeder dort injizierte Dienst wird für **jeden anonymen Besucher** samt
    Konstruktor-Graph gebaut — dafür ist
    `PublicSurfaceGuardTests.NoServiceInjectedByAPublicPage_PullsInAnInternalStack` da.
  - **Nicht** registriert, mit Grund: keine neue Entität ⇒ kein `SearchCatalog`, kein
    `WatchlistRecordRollup`, kein Papierkorb, keine der vier Zeitstrahl-/Chronik-Stellen, kein
    `RecordsReference`/`LinkService`, kein `AuditEntityDisplay`, keine `MergedPageSections`/`FeedbackPageTabs`
    (es gibt nichts zu konfigurieren außer dem Modulschalter), kein `NotificationType`, kein Discord-Push,
    kein Aktenzeichen-Präfix, keine Migration.
