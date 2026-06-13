# Bedrohungs-Score — Algorithmus „EHK-Score" (Phase 8, Block D)

> **Zweck dieses Dokuments:** die vollständige, implementierbare Spezifikation des automatischen
> Bedrohungs-Scores für Fraktionen. Schwerpunkt liegt bewusst auf dem **Algorithmus** (Qualität,
> Verteidigbarkeit, Robustheit). Implementierungs-Anschluss steht am Ende.

---

## 1. Kontext & Ziel

Phase 8 (Visualisierungen) ist bis auf **Block D** fertig. Block D = *Automatischer Bedrohungs-Score
(Personen/Fraktionen; Sortierung/Priorisierung)* + Statistik-Reports + Lagebericht. Dieses Dokument
spezifiziert das Herzstück: den **Score-Algorithmus für Fraktionen**.

Bereits im Code vorhanden (wird nur noch *gefüllt*, nicht neu gebaut):
- `Fraktion.BedrohungsScore` (`int?`, 0–100) — Spalte existiert, Berechnung fehlt (`Fraktion.cs:42`).
- `GefaehrdungsStufeLogic.Aus(int?)` — Mapping Score → Stufe mit **festen** Schwellen
  **≤0/null → Keine · <25 → Niedrig · <50 → Mittel · <75 → Hoch · ≥75 → Kritisch**. Das ist die
  **einzige Quelle der Wahrheit**; der Algorithmus richtet seine Kalibrierung danach aus.
- `DashboardService.GetFraktionenNachGefaehrdungAsync` sortiert DB-seitig nach `BedrohungsScore ?? 0`
  → der Score **muss persistiert** sein (eine on-read-Berechnung wäre nicht in `ORDER BY` übersetzbar).

**Festgelegte Entscheidungen (Auftraggeber):**
1. **V1 = nur Fraktion.** Person bekommt denselben Aufbau als V2 (braucht 4 neue Spalten + Migration).
2. **Taten allein können „Kritisch" (≥75) erreichen.** Eine hochaktive, aber noch nicht eingestufte
   Fraktion wird sichtbar kritisch. Die Einstufung wirkt nur als garantiertes Mindest-Band.
3. **Score auch für Personen** (gleiche 3-Schicht-Architektur, eigene Teilscores P1–P5 — siehe Abschnitt 12).
4. **Admin-einstellbare Parameter** (`/admin/bedrohungs-score`): alle numerischen Stellschrauben liegen in
   einem zur Laufzeit geladenen Konfig-Objekt (`BedrohungsScoreKonfiguration`, persistiert als JSON in
   `BedrohungsScoreKonfig`, 10-Min-Cache, Führung-only). **Fix bleiben** (bewusst nicht editierbar): die
   Einstufungs-Sockel und die Stufen-Schwellen 25/50/75 (`BedrohungsScoreKonstanten.Sockel` /
   `GefaehrdungsStufeLogic`) sowie die Schwere-Keyword-Tabellen. Die Default-Konfig = die bisherigen
   Konstanten (⇒ ohne Änderung identische Ergebnisse, per Golden-Master verifiziert).

---

## 2. Leitidee

> **„Taten heizen, Zeit kühlt, Struktur verstärkt, Einstufung garantiert ein Band, Konfidenz erklärt."**

Der Score ist eine **absolut normalisierte** (nicht relativ/perzentil) gewichtete Summe weniger, einzeln
**gesättigter** Teil-Scores. *Absolut*, weil im Strafverfolgungs-Kontext **Verteidigbarkeit &
Reproduzierbarkeit** schwerer wiegen als nichtlineare Genauigkeit:
- Der Score einer Fraktion ändert sich **nie**, nur weil eine *andere* Fraktion verändert/angelegt wurde.
- „73" bedeutet **immer** dasselbe (audit- und gerichtsfest).
- Kein Cold-Start-Problem bei kleinem, lückenhaftem Bestand.

Drei sauber getrennte Schichten, alle **in-memory** nach flachem Laden (`WHERE FK IN`) — kein
`CROSS APPLY`/`LATERAL` (Pomelo/MySQL-Limit).

---

## 3. Schicht 1 — Inhalts-Teilscores (Caps summieren exakt auf 100)

Jeder Teil sättigt per `1 − exp(−roh/k)` bzw. `min(·, cap)`, damit **Massen-Einträge nie durchschlagen**.

### S1 — Aktivitäts- & Maßnahmen-Heat — Kern, **Cap 55**

Zeit-abklingende Evidenz aus *datierten* Vorfällen. Dominanter, manipulationssicherster Term (kostet echte
RP-Ereignisse, nicht Tipparbeit). Frische schwere Gewalttaten treiben am stärksten; ohne neue Taten kühlt der
Score automatisch ab.

```
decay(t)      = 0.5 ^ ( max(0, (UtcNow − t).TotalDays) / 90 )      // Halbwertszeit 90 Tage
                                                                    // Zukunfts-Zeitpunkte → Alter 0 (decay 1)

artGewicht(Art)  // case-insensitive Keyword-Match, NIE 0 (eine erfasste Tat ist immer Signal):
   schwer  = 3.0  { Mord, Tötung, Hinrichtung, Geiselnahme, Entführung, Anschlag, Terror }
   mittel  = 2.0  { Raub, Überfall, Schießerei, Bank, Erpressung, Schutzgeld, Waffenhandel, Drogenhandel }
   leicht  = 1.0  { alles andere / unbekannt / leer }

aktHeat  = Σ  artGewicht(a.Art) · decay(a.Zeitpunkt)               // über FraktionAktivitaet der Fraktion
                                                                    // (direkt an der Fraktion ⇒ austritts-stabil)

ausgangGewicht(Ausgang):  Erschossen 2.0 · Spritze 1.5 · LäuftNoch 1.2 · OffiziellEntlassen 1.0
dokHeat  = Σ über Mitglieder  min( 8, Σ_dok ausgangGewicht · decay(dok.Zeitpunkt) )   // Pro-Person-Cap 8

roh = aktHeat + 0.6 · dokHeat
S1  = 55 · ( 1 − exp(−roh / 6) )
```

**Härtung (austritts-stabil & auf die Mitgliedschaftsdauer begrenzt):**
`FraktionMitglied` trägt einen globalen `!IstGeloescht`-Filter, und `GeloeschtAm` = **Austrittsdatum**.
Würde man Doks nur über *aktive* Mitglieder bündeln, verschwände die gesamte Tat-Historie eines Täters beim
Austritt (und wäre manipulierbar). Daher:
- Mitgliedschafts-Perioden via `db.FraktionMitglieder.IgnoreQueryFilters()` laden (inkl. ausgetretener;
  `ErstelltAm` = Beitritt, `GeloeschtAm` = Austritt-oder-`null`).
- PersonDoks der betroffenen `PersonId`s flach laden; ein Dok zählt **nur**, wenn `dok.Zeitpunkt` in
  **[Beitritt … Austritt]** liegt (schließt sowohl ausgetretene-Täter-Lücke als auch personenfremde
  Vor-Beitritts-Inzidenzen aus).
- `PersonDok.OrgTyp/OrgId` wird **nicht** zur Zuordnung genutzt (laut Modell „lose"/unzuverlässig) — die
  Bündelung läuft robust über die Mitgliedschaft.

### S2 — Organisation & Reichweite — **Cap 22**

Größe + ausdifferenzierte Struktur + kriminelle Infrastruktur = handlungsfähige, schwer zerschlagbare
Organisation. Nur Vorhandensein/Sättigung, **nie** Freitext-Mengen (die `Menge`-Felder sind unparsebarer
Freitext wie „ca. 20").

```
groesse     = max( GeschaetzteMitgliederzahl ?? 0, aktiveMitgliederCount )
groessePkt  = 10 · ( 1 − exp(−groesse / 15) )                                   // max 10
strukturPkt = (Raenge ≥ 3 ? 3 : Raenge) + (aktiveLeitung > 0 ? 2 : 0)
            + (Anwesen.Trim() ≠ "" ? 1 : 0)                                     // max 6
waffenPkt   = 3 · ( 1 − exp(−distinctWaffen / 3) )                              // max 3; distinct, getrimmt, nicht-leer
infraPkt    = 3 · ( 1 − exp(−(2·Drogenrouten + Lagerbestand) / 4) )            // max 3

S2 = min( 22, groessePkt + strukturPkt + waffenPkt + infraPkt )                 // Sub-Caps 10+6+3+3 = 22
```
`distinct`+`Trim` schließt den „+1 durch 50 leere/Whitespace-Einträge"-Exploit. **Alle** „nicht-leer"-Tests
(auch `Anwesen`) trimmen Whitespace. Die Komponenten-Sub-Caps müssen exakt auf `Cap S2` summieren (sonst toter
`Math.Min`-Bereich).

### S3 — Konflikt & Bündnis — **Cap 15**

Aktive Revierkonflikte = akute Gewaltlage; Bündnisse = vergrößerte Schlagkraft. Nur **manuelle**
Verknüpfungen (`Automatisch == false`), damit System-„Fraktionskollege"-Sternkanten nicht zählen.

```
konflikte  = COUNT Verknuepfung( Art == Konflikt(1), !Automatisch, !IstGeloescht, inzident zur Fraktion )
buendnisse = COUNT Verknuepfung( Art == Buendnis(2),  !Automatisch, !IstGeloescht, inzident zur Fraktion )
                                                       // inzident = (VonTyp=Fraktion ∧ VonId=Id) ∨ (NachTyp=Fraktion ∧ NachId=Id)
roh = 2.0·konflikte + 1.0·buendnisse
S3  = 15 · ( 1 − exp(−roh / 4) )
```

### S4 — Netzwerk-Zentralität — **Cap 8** (kleiner Verstärker)

Wie zentral die Fraktion im allgemeinen Beziehungsnetz ist (über *manuelle Standard*-Verknüpfungen zu Personen,
Operationen, Vorgängen … — **ohne** Konflikt/Bündnis, die S3 abdeckt, und **ohne** Mitgliederstern, der in S2
steckt). Eigener schlanker `COUNT` (nicht `GraphService`: dessen `GradZaehlen` ist `private static`,
`GetGraphAsync` braucht `ClaimsPrincipal` + 250-Knoten-Deckelung → für einen Batch-Sweep ungeeignet).

```
grad = COUNT Verknuepfung( Art == Standard(0), !Automatisch, !IstGeloescht, inzident zur Fraktion )
S4   = 8 · ( 1 − exp(−grad / 4) )
```
`Art == Standard` hält S4 **disjunkt zu S3** (Konflikt/Bündnis); `!Automatisch` ist defensiv (persistierte
Auto-Kanten sind Person↔Person, nie fraktions-inzident). Da S4 von Standard-Kanten abhängt, recomputed
`VerknuepfungService` jetzt bei **jeder** manuellen Verknüpfung mit Fraktionsbeteiligung (nicht nur Konflikt/Bündnis).

> **`Inhalt = S1 + S2 + S3 + S4 ∈ [0,100]`** (Caps 55+22+15+8). Keine Reskalierung nötig.

---

## 4. Schicht 2 — Band-Projektion durch die Einstufung

Die Einstufung (`Einstufung`-Enum, `EinstufungVerlauf`-auditiert) ist das einzige validierte, von einem Agent
verantwortete menschliche Gefährlichkeits-Urteil. Sie wirkt **genau einmal** — als garantiertes
**Mindest-Band**, in das der Inhalt hineinprojiziert wird:

```
sockel(Einstufung):  Unbekannt 0 · Prüffall 12 · Verdachtsfall 50 · GesichertStaatsgefährdend 75

BedrohungsScore = round( sockel + (100 − sockel) · Inhalt/100 )      // dann clamp 0..100
```

Das ersetzt bewusst den naheliegenden harten `max(score, sockel)`, der die Verteilung **kollabieren** ließe
(alle Verdachtsfälle klebten bei exakt 50, keine Binnen-Differenzierung). Mit der Band-Projektion gilt:

| Einstufung | Band | Verhalten |
|---|---|---|
| Unbekannt | [0, 100] | **Score = Inhalt** → reine Taten können „Kritisch" werden (Auftraggeber-Entscheidung). |
| Prüffall | [12, 100] | leichter Sockel, Inhalt dominiert. |
| Verdachtsfall | [50, 100] | garantiert ≥ „Hoch"; Inhalt differenziert innerhalb (dünn ~50–60, stark ~85). |
| GesichertStaatsgefährdend | [75, 100] | **immer** „Kritisch"; Inhalt sortiert innerhalb der Kritisch-Klasse. |

- **Garantierte Mindeststufe** exakt an die festen Schwellen 50/75 gekoppelt.
- **Inhalt differenziert innerhalb und über dem Band** → keine Klassen-Verklumpung.
- **Einstufung zählt nicht doppelt** (sie ist *kein* Inhalts-Teilscore, nur das Band).

Stufe unverändert: `GefaehrdungsStufeLogic.Aus(BedrohungsScore)`.

---

## 5. Schicht 3 — Daten-Konfidenz (separat, score-unabhängig)

**Kern-Designprinzip: „fehlend ≠ ungefährlich".** Datenlücken senken den Score **nie** (jeder fehlende Teil
trägt additiv 0 bei, keine Imputation, keine Multiplikation des Scores mit der Konfidenz). Stattdessen eine
**getrennt persistierte** Kennzahl `BedrohungsKonfidenz` (0–100) = gewichteter Erfassungsgrad:

```
konfidenz = round( 100 · ( 0.30·(hatAktivität ∨ MitgliederMitDoks)
                         + 0.20·(aktiveMitglieder > 0)
                         + 0.15·(Waffen ∨ Lager ∨ Routen > 0)
                         + 0.10·(GeschaetzteMitgliederzahl gesetzt)
                         + 0.10·(Einstufung ≠ Unbekannt)
                         + 0.15·(jüngste *Erfassung* < 180 Tage) ) )
```

- **Frische über Erfassungszeit** (`ErstelltAm`/`GeaendertAm`), **nicht** über die RP-`Zeitpunkt`-Felder
  (sonst drückt ein gerade nacherfasstes Alt-Ereignis die Konfidenz fälschlich; Zukunfts-Werte ausgeschlossen).
- Konflikt (S3) geht **nicht** in die Konfidenz ein (eine unauffällige/isolierte Fraktion ist kein
  Erfassungs-Loch).
- **UI-Pflicht:** Score immer mit Konfidenz-Badge. So liest die UI: *hoher Score + niedrige Konfidenz =
  „gefährlich, aber dünn belegt → dringend nacherfassen"*. Konfidenz sortiert **nie** herab.

---

## 6. Triage-Flag (operativer Anschluss, kein Score-Eingriff)

`Inhalt ≥ 50 ∧ Einstufung == Unbekannt` → Flag **„hochaktiv, aber nicht eingestuft – bitte triagieren"**
(im DetailJson + als Filter auf der Fraktionsliste/Dashboard). Macht den gefährlichsten blinden Fleck — die
noch nicht getriagte, aber hochaktive Gang — operativ sichtbar, ohne den Score zu verzerren. (Analog kann
`hoher Score + niedrige Konfidenz` einen „nacherfassen"-Hinweis erzeugen.)

---

## 7. Sonderfälle

- **Staatsfraktion** (`IstStaatsfraktion == true`): **allererster** Schritt der Berechnung →
  `BedrohungsScore = null`, `Konfidenz = null`, `DetailJson = {"ausgenommen":"Staatsfraktion"}`, return.
  `null` (nicht 0) = „ausgenommen/nicht bewertet"; `GefaehrdungsStufeLogic.Aus(null) = Keine`;
  `OrderByDescending(... ?? 0).ThenBy(Name)` hält sie konsistent ans Ende.
- **Soft-Delete** (`IstGeloescht`): gelöschte Fraktion → kein Recompute.
- **`IstVerschlusssache`** beeinflusst den Score **nicht** (nur die Anzeige folgt dem bestehenden VS-Gate).
- **Leere Akte (Unbekannt, keine Daten):** alle Teilscores 0 → Score 0 → Stufe Keine, Konfidenz niedrig
  (ehrlich: kein Signal vorhanden). Staatsfraktion-Shortcut läuft **vor** jeder DB-Query → kein Null-Crash.

---

## 8. Warum der Score gut/verteidigbar ist (Designprinzipien)

- **Austritts-stabiler Heat** → nicht durch „Mitglied vor der Bewertung austreten lassen" manipulierbar;
  eine Geiselnahme vor 10 Tagen verschwindet nicht durch einen Austritt.
- **Einstufung wirkt genau einmal** (Band-Projektion) → keine Doppelzählung, kein Verteilungs-Kollaps.
- **Sättigung pro Teilscore** → robust gegen Massen-/Karteileichen-Einträge; `distinct`+`Trim` gegen Trivial-Exploits.
- **Absolute Normalisierung** → reproduzierbar, audit-fest, kein Cold-Start.
- **Zeit-Decay** → spiegelt *aktuelle* Bedrohung, kühlt ohne neue Taten automatisch ab.
- **Volle Nachvollziehbarkeit:** jeder Punkt ist auf eine konkrete, datierte, auditierte Akte
  (`FraktionAktivitaet`/`PersonDok`/`EinstufungVerlauf`/`Verknuepfung`) zurückführbar → „warum kritisch?"
  Zeile für Zeile beantwortbar.

---

## 9. Durchgerechnetes Beispiel — Fraktion „Vagos" (Stand 2026-06-13)

- Aktivitäten: Raub (vor 5 T), Geiselnahme (vor 40 T), Schießerei (vor 120 T):
  `aktHeat = 2.0·0.962 + 3.0·0.735 + 2.0·0.397 = 4.92`.
- 2 Mitglieder mit je 1 Dok „Erschossen" vor ~30 T (`decay 0.794`): `dokHeat = 2·min(8, 2.0·0.794) = 3.18`.
  `roh = 4.92 + 0.6·3.18 = 6.83` → **S1 = 55·(1−e^−1.138) = 37.4** (Cap 55 unverändert).
- Organisation (Cap 22): ~22 Mitglieder (7.7) + Struktur 6 + 5 Waffenarten (2.4) + 1 Route (1.2) → **S2 = 17.3**.
- 2 Konflikte + 1 Bündnis (`roh = 5`) → **S3 = 15·(1−e^−1.25) = 10.7**.
- 5 sonstige (Standard-)Verknüpfungen → **S4 = 8·(1−e^−1.25) = 5.7**.
- `Inhalt = 37.4 + 17.3 + 10.7 + 5.7 = 71.1`.
- Einstufung **Verdachtsfall** → `Score = 50 + 50·0.711 = 86` → **GefaehrdungsStufe.Kritisch**, Konfidenz ~85 %.
- UI-Treiber: Aktivitäts-Heat +37 · Organisation +17 · Konflikte +11 · Netzwerk +6, angehoben ins Verdachtsfall-Band (≥50).

Sanity-Check (Band-Projektion verhindert Kollaps): Vagos **ohne** Standard-Kanten (grad 0) → Inhalt 65.4 →
Score **83** (weiter Kritisch); die 5 Standard-Kanten heben es auf **86**. Verdachtsfall mit Inhalt 10 → 55
(Hoch), mit 40 → 70 (Hoch). Unbekannt mit Inhalt 71 → 71 (Hoch), mit 80 → Kritisch. → gute Binnen-Differenzierung.

---

## 10. Parameter (admin-einstellbar) & fixe Anker

Alle numerischen Stellschrauben liegen in **`BedrohungsScoreKonfiguration`** (Default = die unten genannten
Werte) und sind über **`/admin/bedrohungs-score`** editierbar (persistiert als JSON in `BedrohungsScoreKonfig`,
10-Min-Cache via `IBedrohungsScoreKonfigService`, Führung-only, mit Summen-Validierung + „alle neu berechnen").

**Default-Werte:**
- Geteilt: Halbwertszeit **90 Tage** · Konfidenz-Frische **180 Tage** · Triage-Schwelle **50** ·
  `artGewicht` schwer/mittel/leicht **3/2/1** · `ausgangGewicht` Erschossen/Spritze/LäuftNoch/Entlassen **2/1.5/1.2/1**.
- Fraktion: Caps **S1 55 / S2 22 / S3 15 / S4 8** (= 100); S1-Nenner 6, Pro-Mitglied-Dok-Cap 8, dokHeat-Gewicht 0.6;
  S2-Sub Größe 10 / (Ränge-max 3 + Leitung 2 + Anwesen 1) / Waffen 3 / Infra 3 = 22; S3-Nenner 4, S4-Nenner 4.
- Person: Caps **P1 40 / P2 22 / P3 18 / P4 12 / P5 8** (= 100); P2-Sub Waffen 14 + Flüchtig 8 = 22;
  Nenner P1 4 / P3 3 / P4 4 / P5 4; Person-Waffen-Nenner 2; Beziehungs-Gewichte Feind 2 / Verbündeter 1 / GP 1 / Leitung 1.5.

**Fix (nie editierbar):** Sockel **0 / 12 / 50 / 75** (`BedrohungsScoreKonstanten.Sockel`), Stufen-Schwellen
**25/50/75** (`GefaehrdungsStufeLogic`), Schwere-Keyword-Tabellen, die Sättigungsformel. **Validierung erzwingt:**
Caps je Subjekt = 100, S2-Sub = Cap S2, P2-Sub = Cap P2, Nenner > 0, Schwere monoton ≥ 1.

**Kalibrier-Hinweis:** nach dem ersten Echtdaten-Lauf einmal so justieren, dass die 2–3 unstrittig
gefährlichsten Ziele ≥75 erreichen und die Verteilung streut; danach einfrieren.

---

## 11. Implementierungs-Anschluss

### Modell + Migration
`Data/Entities/Fraktionen/Fraktion.cs`: 3 neue Spalten — `BedrohungsKonfidenz (int?)`,
`BedrohungsDetailJson (string?)`, `ScoreBerechnetAm (DateTime?)`. (`BedrohungsScore` existiert.)
Migration `Phase19_BedrohungsScore` (EF stempelt mit 2026-06-13… → sortiert nach `Phase18`); Dev-Server vor
der Migration stoppen (bin-Lock), nach `migrations add` neu bauen vor `--no-build` (Projektkonvention).
Schema wird beim Start automatisch via `db.Database.MigrateAsync()` angewandt (`Program.cs:333`).

### Service + Persistenz
`Services/Bedrohung/IBedrohungsScoreService.cs` + `BedrohungsScoreService.cs` (Muster `AktualitaetService`:
`IDbContextFactory<AppDbContext>` + `IMemoryCache`):
- `BedrohungsScoreErgebnis Berechne(...)` — **reine** Funktion über geladene Daten → unit-testbar.
- `Task NeuBerechnenAsync(string fraktionId, …)` — lädt flach, berechnet, **persistiert via
  `ExecuteUpdateAsync`** (am Audit-Interceptor vorbei; Muster QuelleService/PersonMergeService/DokumentService).
  Sonst stempelt der Save-Pfad `GeaendertAm` (verfälscht die Aktualitäts-Ampel, die auf
  `GeaendertAm ?? ErstelltAm` keyed) und flutet AuditLog/Zeitstrahl bei jedem Recompute.
- `Task NeuBerechnenAlleAsync(…)` — für den Sweep.

### Recompute (Hybrid)
- **Event-getrieben:** `NeuBerechnenAsync(fraktionId)` am Ende jeder score-relevanten Schreibmethode in
  `FraktionService` (Erstellen/Aktualisieren/EinstufungSetzen/Mitglied ±/Mitglied ändern/Aktivität ±/ändern —
  nach `tx.CommitAsync` bei den transaktionalen), `PersonDokService` (Dok angelegt/geändert/gelöscht → aktive
  Fraktionen der Person neu rechnen) und `VerknuepfungService` (Konflikt/Bündnis betrifft beide Fraktions-Seiten).
- **Zeit-getrieben:** `Infrastructure/Bedrohung/BedrohungsScoreSweepDienst.cs` — `BackgroundService` +
  `PeriodicTimer`, eigener Scope je Lauf (Muster `WiedervorlageFaelligkeitsDienst`), **täglich** alle
  nicht-Staat/nicht-gelöschten Fraktionen → fängt die tägliche Decay-Drift von S1 ab.
- `Program.cs`: `IBedrohungsScoreService` (scoped) + `AddHostedService<BedrohungsScoreSweepDienst>()` bei den
  Phase-8-Services registrieren.

### UI
- `Components/Pages/Fraktionen/FraktionDetail.razor`: Panel **„Gefährdung"** — Kopf „Score (Stufe) ·
  Konfidenz %", Aufschlüsselung je Teilscore aus `BedrohungsDetailJson` mit Klartext-Treibern, Band-Hinweis,
  ggf. Triage-/Nacherfassen-Hinweis.
- `Components/Pages/Fraktionen/FraktionDruck.razor`: rendert bereits `GefaehrdungsStufeLogic.Aus(...)` —
  Aufschlüsselung anhängen.
- `Components/Pages/Fraktionen/FraktionenListe.razor`: sortierbare Score-/Stufen-Badge-Spalte + Triage-Filter.
- `Components/Pages/Home.razor`: funktioniert über `DashboardService`, sobald Scores befüllt sind; optional Triage-Kachel.

### Verifikation
1. **Unit-Test** der reinen `Berechne`-Funktion mit „Vagos" → `Inhalt ≈ 71`, `Score ≈ 86 (Kritisch)`,
   Konfidenz ~85 %. Weitere: leere Unbekannt-Akte → 0/Keine; Staatsfraktion → `null`; ausgetretenes Mitglied
   mit alter Tat → Heat bleibt (austritts-stabil); 50 leere Bestands-Einträge → S2 unverändert (Manipulationstest).
2. **Build 0/0** (Dev-Server vorher stoppen).
3. **End-to-end:** Migration anwenden → Aktivität anlegen → Score aktualisiert sich sofort (Event) →
   Aufschlüsselung plausibel → Dashboard sortiert/zeigt Stufe → Sweep aktualisiert Decay.
4. **Kalibrierung** am Echtbestand prüfen; Konstanten einmal nachziehen.

---

## 12. Abgrenzung / später (V2+)

- **Netzwerk-Zentralität (Teil-Score S4):** ✅ **umgesetzt** (Variante A: reiner Count manueller Standard-Kanten,
  Cap 8, Nenner 4; disjunkt zu S3, orthogonal zu S2; eigener schlanker `COUNT` ohne `GraphService`; Recompute
  bei jeder manuellen Verknüpfung mit Fraktionsbeteiligung). Bewusst **kein** Mitgliederstern (wäre Größen-Dublette
  zu S2) und **keine** Typvielfalt-Gewichtung (ungewichteter Count hält `Berechne` an einer Zahl deterministisch/testbar).
- **Person-Score:** ✅ **umgesetzt** (`BerechnePerson`, gleiche 3-Schicht-Architektur). Teilscores
  **P1 Maßnahmen-Heat** (Person.Doks, Cap 40) · **P2 Bewaffnung & Eskalation** (Waffen + Flüchtig, Cap 22) ·
  **P3 Observations-Heat** (laufend wiegt mehr, beide zeit-abklingend, Cap 18) · **P4 Soziale Gefahr**
  (PersonBeziehung Feind/Verbündeter/GP + Leitungsrollen, Cap 12) · **P5 Netzwerk-Zentralität** (manuelle
  Standard-Verknüpfungen, Cap 8) = 100. Lebensstatus on-read via `LebensstatusLogic.Effektiv`: **Tot
  statusneutral** (temporär; Respawn-Hinweis on-read in der UI, **nicht** im JSON), **Flüchtig → P2-Bonus**.
  **Kein** Person→Fraktion→Person-Kontext-Bonus (azyklisch). Datenreichtum (Aliase/Fahrzeuge/Telefone/Orte) nur
  in die Person-Konfidenz, nie in den Score. Recompute-Trigger: PersonService / ObservationService /
  BeziehungService / PersonDokService (zusätzlich zur Fraktion) / VerknuepfungService (Person-Zweig) /
  Mitglieder-Leitung in Fraktion/Gruppe/Partei-Service / nächtlicher Sweep.
- **Statistik-Reports/Export + automatischer Lagebericht** (Rest von Block D) — eigener Plan.
- **Optional/offen:** Person-Verteilung im Dashboard (`GetPersonenNachGefaehrdungAsync`) – Kern-UI (Tab + Liste)
  steht, die Dashboard-Kachel ist noch nicht ergänzt. P3 „laufend-Term" und Caps nach Echtdaten kalibrieren.
