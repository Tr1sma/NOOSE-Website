# Öffentliches Geld — Kopfgeld & Belohnung

> **Lies das, bevor du `FahndungKopfgeldAnteile`, `IBountyService`, `IRewardService`,
> `HinweisBelohnungen` oder irgendeinen Pfad anfasst, der Geld bucht.**
> Kernregeln: nach außen geht **eine Zahl, nie eine Aufschlüsselung**; Einzahlen und Auszahlen sind
> **Compare-and-swap**; Geldtabellen sind `IAuditable`, aber **nicht** `ISoftDelete` (append-only,
> kein Papierkorb); jeder Schreiber der Anteil-Tabelle ruft `InvalidatePublicViewAsync`.
> Fahndung: [oeffentlich-fahndung.md](oeffentlich-fahndung.md) · Hinweise: [oeffentlich-buergerkontakt.md](oeffentlich-buergerkontakt.md)

## Phase 6 — Kopfgeld

- **Phase 6 (Kopfgeld) — was daran anders ist:**
  - **Nach außen geht eine Zahl, nie eine Aufschlüsselung.** `PublicBounty(Total, IsCap)` kann Herkunft, Stifter,
    Konto, Kassenbuchung und Anzahl der Anteile strukturell nicht tragen; eine Aufschlüsselung wäre ein öffentliches
    Verzeichnis, welcher Agent eigenes Geld auf wen gesetzt hat. Zwei Schichten: der Typ **und** ein Dateiscan über
    `Components/Pages/Public/` (`PublicSurfaceGuardTests`), weil eine Seite den Dienst auch selbst fragen könnte.
  - **Die Summe wird im Snapshot berechnet, hinter dem Gürtel** — direkt neben
    `HintsAsync`. **Keine Spalte** auf der Ausschreibungszeile (eine denormalisierte Summe driftet still, und eine
    falsche Zahl über Geld ist schlimmer als eine 10 s alte) und **kein zweiter Lesepfad** (der müsste den
    Unterdrückungsgürtel wiederholen — genau die Phase-4-Falle). Eine Summe `<= 0` erzeugt keinen Eintrag statt
    „0 $" außen.
  - **`SaveAndInvalidateAsync` nimmt jetzt `AppDbContext?`.** Der öffentliche Einstieg
    `IPublicWantedService.InvalidatePublicViewAsync()` ruft dieselbe Methode mit `null` — dadurch bleibt
    `PublicWantedService.cs` bei **einem** `SaveChangesAsync(` und **einem** `cache.Remove(`, und
    `PublicWantedCacheDisciplineTests` musste nicht angefasst werden. Der zweite Wächter dazu:
    jede Produktionsdatei, die `FahndungKopfgeldAnteile` **und** `SaveChangesAsync` nennt, muss auch
    `InvalidatePublicViewAsync` nennen.
  - **`IBountyService` besitzt die Anteile, `IPublicWantedService` die Obergrenze.** `KopfgeldIstObergrenze` ist ein
    Snapshot-Feld auf der Ausschreibungszeile, und deren Tabelle hat genau einen Schreibpfad.
  - **In der öffentlichen Summe zählen nur `Zugesagt` und `Gesichert`.** Ein `Beantragt` ist kein Geld — und
    verriete nebenbei eine laufende interne Entscheidung. `Ausgezahlt` ist ausgegeben (gesetzt wird es erst mit der
    Belohnungsphase), `Zurueckgezogen` ist weg. Diese Regel steht **einmal**, in `Services/Public/BountyShares.cs`:
    als EF-Prädikat `Advertised` und als In-Memory-Zwilling `IsAdvertised` (Muster `AgentSelection`). Sie entscheidet
    den öffentlichen Snapshot, die interne Aufschlüsselung **und** das Vorher/Nachher einer Erhöhung — ausgeschrieben
    driftet sie.
  - **Der Kopfgeld-Posteingang verlangt, dass die Ausschreibung noch existiert** (`PendingRequests`), genau wie der
    Veröffentlichungs-Posteingang. Ein gelöschter Entwurf macht seinen Antrag sonst unentscheidbar — Genehmigen
    antwortet „nicht gefunden" — während das Nav-Badge ihn weiterzählt.
  - **Rang 1–2 beantragt behördliches Geld** (`RequestType.Kopfgeld` mit eigener Spalte `KopfgeldAnteilId`, **nicht**
    `VeroeffentlichungFahndungId` — `PublicWantedService.PendingRequests` joint darauf und sammelte den Antrag sonst
    im falschen Posteingang ein). Der Anteil entsteht als `Beantragt`; Genehmigen schaltet ihn auf `Zugesagt`,
    Ablehnen auf `Zurueckgezogen`. Guard-Reihenfolge wie beim Veröffentlichungsantrag: `RequireBountyWrite` **vor**
    `RequireHighestClassification`.
  - **`Permission.RequireBountyWrite` ist ein eigener Guard**, weil `RequireWriteAccess` nur Aufsicht und Partner
    blockt — ein angemeldeter Bürger käme durch.
  - **Die Deckung zählt zwei Summanden**, und der zweite ist der nicht offensichtliche: offene **behördliche**
    Zusagen **plus** bereits eingezahltes **privates** Geld. Eine Einzahlung hebt den Kontostand und ist trotzdem
    schon vergeben; ohne sie meldet die Deckung Entwarnung, die es nicht gibt. Warnung, **keine** Sperre.
  - **Einzahlen ist ein Compare-and-swap**, wörtlich nach `FinancingService.PayAsync`: zwei Tabs würden sonst zwei
    Einzahlungen auf denselben Anteil buchen (verschiedene Ids, der Unique-Index greift nicht). `ExecuteUpdate`
    umgeht den Interceptor ⇒ `ManualAudit.Row` von Hand. Die öffentliche Summe ändert sich dabei **nicht**.
  - **Anteile sind `IAuditable`, aber **nicht** `ISoftDelete`** — Geldhistorie ist append-only, zurückgezogen wird
    per Status. Deshalb keine `TrashService`-Registrierung.
  - **Discord meldet nur Erhöhungen** (`PublicWantedBountyRaised`, routbar), und nur auf einer laufenden,
    nicht abgelaufenen Ausschreibung bei eingeschaltetem Modul. Eine Senkung bleibt still: sie untergräbt die eigene
    Ausschreibung, und der alte Post steht ohnehin unkorrigierbar weiter. Die Compose-Methode nimmt ausschließlich
    `PublicBountyAnnouncement`.
  - **Der Anteil rollt bewusst nicht auf die Beobachtungsliste.** `WatchlistRecordRollup` ist eine statische Map ohne
    Datenbank, und Anteil → Ausschreibung → Akte sind zwei Hops; das Publizieren der Ausschreibung bleibt das
    beobachtbare Ereignis. Zeitstrahl und Chronik gehen dagegen sehr wohl über beide Hops
    (`TimelineService.AuditSourceAsync`, `TimelineDisplay.MapAudit`, `ChronikParentResolver`).

## Phase 9 — Belohnung (Auszahlung, Kasse, Beleg)

- **Phase 9 (Belohnung) — was daran anders ist:**
  - **Ein Auszahlungspfad, eine Transaktion.** `RewardService.PayoutAsync` bucht je Anteil über
    `IKassenService.BookAsync(db, …)`, legt die `HinweisBelohnungen` an, setzt die Anteile auf `Ausgezahlt` und
    schließt die Hinweise über `ITipService.MarkRewardedAsync(db, …)` — alles in **einem** Kontext und **einer**
    Transaktion, weil kein Geld ohne Statuswechsel und kein Statuswechsel ohne Geld existieren darf. Die Transaktion
    ist ohnehin Pflicht: `ICaseNumberService.NextAsync` verweigert ohne umschließende Transaktion. Nach dem Commit,
    nie davor: `InvalidatePublicViewAsync`, `StampForNoticeAsync`, `AfterRewardAsync`.
  - **Eigener `IRewardService`, nicht `IBountyService.PayoutAsync`.** `BountyService` ist einaudienz-intern; die
    Belohnung ist der erste Geldpfad mit einer **Bürger**-Leseseite (Beleg). Er besitzt `HinweisBelohnungen` und ist
    der einzige Schreiber von `BountyShareStatus.Ausgezahlt` — das macht ihn zum Schreiber der Anteil-Tabelle, also
    greift `PublicSurfaceGuardTests.EveryWriterOfTheBountyTable_DropsThePublicSnapshot` und verlangt
    `InvalidatePublicViewAsync`. Richtig so: `Ausgezahlt` fällt aus `BountyShares.Advertised`, die öffentliche Summe
    sinkt auf 0.
  - **`Gefasst` ist Vorbedingung, keine Nebenwirkung.** Die Auszahlung weist eine nicht gefasste Ausschreibung ab,
    statt sie selbst umzuschalten — die Fahndungstabelle behält ihren einen Schreibpfad (`PublicWantedService`), und
    das Panel schreibt den Hinweis darauf hin, statt einen toten Knopf zu zeigen.
  - **Eine Auszahlung je Ausschreibung.** Danach sind **alle** beworbenen Anteile `Ausgezahlt`; der Statuswechsel ist
    der Idempotenz-Token (Muster Ablauf-Worker), gesetzt per Compare-and-swap wie in `PayInAsync`, mit
    `ManualAudit.Row` je Anteil, weil `ExecuteUpdate` den Interceptor umgeht. **`Ausgezahlt` heißt erledigt, nicht
    restlos geleert** — sonst zählt `GetCoverageAsync` einen abgeschlossenen Fall für immer als offene Verpflichtung.
  - **Die Verteilregel steht einmal**, in `Services/Public/RewardAllocation.cs`: zuerst Geld ohne persönliche
    Übergabe (`Gesichert`, `NooseKasse`), dann unbezahlte private Zusagen (`AgentPrivat` + `Zugesagt` ⇒ keine
    Buchung, `SelbstAusgezahltAm`), je Gruppe ältester Anteil zuerst, `AnteilId` als Gleichstand-Entscheider —
    dieselbe Auszahlung muss immer dieselben Buchungen erzeugen. Dort sitzen auch die Σ-Invariante und die Ablehnung
    einer dritten Dezimalstelle (die Spalte hält zwei, MySQL schneidet die dritte wortlos ab).
  - **`HinweisBelohnung` ist `IAuditable`, aber **nicht** `ISoftDelete`** — Geldhistorie ist append-only, Präzedenz
    `FahndungKopfgeldAnteil`. Keine `TrashService`-Registrierung; eine Fehlbuchung wird in der Kasse gegengebucht.
    Die **`BelegNummer` trägt eine Gruppe** (je Auszahlung und Hinweis) und ist deshalb **nicht** unique indexiert:
    eine Zeile ist ein (Hinweis × Anteil)-Paar, der Bürger bekommt einen Beleg je Hinweis, und der Beleg summiert
    seine Zeilen. Unique ist `KassenBuchungId`. Präfix **`BEL`** (`B` gehört den Bewerbungen).
  - **Der Verwendungszweck der Kassenbuchung nennt nur Aktenzeichen.** `/kasse` liest jeder Agent; ein Bürgername
    dort wäre die Anonymitätszusage über das Kassenbuch umgangen — auch bei aufgelöster Anonymität. Eigener Test.
  - **Anonym ist unauszahlbar** (`TipAnonymity.IsHidden`): Geld braucht einen Empfänger, und der Beleg nennt ihn.
    Ein `Neu`-Hinweis ebenso — `TipRules` erlaubt den Sprung nach `FuehrteZurErgreifung` bewusst nicht. Der Dialog
    listet beide Fälle mit Grund.
  - **`Permission.RequireRewardPayout` ist eine eigene Achse** (interner Agent + Schreibrecht + Führung):
    `RequireKassenBookingWrite` greift nur im Buchungszweig, eine vollständig aus privater Zusage bezahlte Belohnung
    liefe also ohne Führungsprüfung durch. Schreib-Guard vor allem anderen (Präzedenz Phase 6).
  - **Das Modul-Gate sitzt auf den Bürger-Lesepfaden, nicht auf der Auszahlung** — Präzedenz „Publizieren braucht
    ein lebendes Modul, *De*publizieren nie"; eine interne Geldbewegung hängt an keinem öffentlichen Schalter.
    Gefragt wird die **gespeicherte Wahl allein** (`PublicModuleSnapshot.Find(key)?.IsEnabled`), nicht
    `RequireEnabledAsync`: das faltet den Not-Aus ein, und der lässt `/buerger` bewusst offen. Derselbe Grund führt
    `PartnerRoutes.IsAllowed` für `/buerger/**` auf `true` — `BuergerLayout` fragt die Liste nie, `PrintLayout`
    schon, und ohne die Zeile wäre der Beleg die einzige Bürgerseite mit „nicht freigegeben" für einen Partner.
  - **Beleg: Eigentümer und Führung**, jeder andere bekommt `null` ⇒ „nicht gefunden" (nie „kein Zugriff", sonst
    Existenz-Orakel). Der Bearbeiter steht auf **keiner** Projektion — `CitizenRewardReceipt` kann ihn strukturell
    nicht tragen —, und `RewardReceipt.razor` setzt `PrintedBy` nicht. Die Seite liegt unter `Pages/Portal/`, nicht
    unter `Pages/Public/`: der Kontobereich eines angemeldeten Bürgers ist nicht öffentlich, also greifen
    `PublicPageScanTests` und `PublicRoutes` dort nicht — und `PrintFrame` funktioniert, weil Portal-Seiten
    interaktiv sind (anders als das Fahndungsposter).
  - **`PublicRewardPaid` ist nicht routbar** — eine Belohnungsmeldung im öffentlichen Kanal outet den Hinweisgeber.
  - **Zeitstrahl über drei Hops, in zwei Abfragen.** Belohnung → Anteil → Ausschreibung → Akte; gestaffelt statt
    verschachtelt, weil `IgnoreQueryFilters()` kompilierungsweit gilt (Phase-4-Falle). Registriert in
    `TimelineService.AuditSourceAsync`, `TimelineDisplay.MapAudit`, `AuditEntityDisplay` und
    `ChronikParentResolver`; **nicht** im `WatchlistRecordRollup` (statische Map ohne Datenbank — beobachtbar ist
    das Gefasst-Setzen).
