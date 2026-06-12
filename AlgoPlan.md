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
3. **Keine Admin-UI / keine konfigurierbaren Gewichte.** Alle Stellschrauben sind benannte Konstanten an
   **einer** Stelle (`BedrohungsScoreKonstanten`), nach erstem Echtdaten-Lauf einmal kalibriert.

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

### S2 — Organisation & Reichweite — **Cap 25**

Größe + ausdifferenzierte Struktur + kriminelle Infrastruktur = handlungsfähige, schwer zerschlagbare
Organisation. Nur Vorhandensein/Sättigung, **nie** Freitext-Mengen (die `Menge`-Felder sind unparsebarer
Freitext wie „ca. 20").

```
groesse     = max( GeschaetzteMitgliederzahl ?? 0, aktiveMitgliederCount )
groessePkt  = 12 · ( 1 − exp(−groesse / 15) )                                   // max 12
strukturPkt = (Raenge ≥ 3 ? 3 : Raenge) + (aktiveLeitung > 0 ? 2 : 0)
            + (Anwesen.Trim() ≠ "" ? 1 : 0)                                     // max 6
waffenPkt   = 4 · ( 1 − exp(−distinctWaffen / 3) )                              // max 4; distinct, getrimmt, nicht-leer
infraPkt    = 3 · ( 1 − exp(−(2·Drogenrouten + Lagerbestand) / 4) )            // max 3

S2 = min( 25, groessePkt + strukturPkt + waffenPkt + infraPkt )
```
`distinct`+`Trim` schließt den „+1 durch 50 leere/Whitespace-Einträge"-Exploit. **Alle** „nicht-leer"-Tests
(auch `Anwesen`) trimmen Whitespace.

### S3 — Konflikt & Bündnis — **Cap 20**

Aktive Revierkonflikte = akute Gewaltlage; Bündnisse = vergrößerte Schlagkraft. Nur **manuelle**
Verknüpfungen (`Automatisch == false`), damit System-„Fraktionskollege"-Sternkanten nicht zählen.

```
konflikte  = COUNT Verknuepfung( Art == Konflikt(1), !Automatisch, !IstGeloescht, inzident zur Fraktion )
buendnisse = COUNT Verknuepfung( Art == Buendnis(2),  !Automatisch, !IstGeloescht, inzident zur Fraktion )
                                                       // inzident = (VonTyp=Fraktion ∧ VonId=Id) ∨ (NachTyp=Fraktion ∧ NachId=Id)
roh = 2.0·konflikte + 1.0·buendnisse
S3  = 20 · ( 1 − exp(−roh / 4) )
```

> **`Inhalt = S1 + S2 + S3 ∈ [0,100]`** (Caps 55+25+20). Keine Reskalierung nötig.

> **Netzwerk-Zentralität bewusst auf V2 verschoben.** War der kleinste Term, hätte eine neue Degree-API
> gebraucht (`GraphService.GradZaehlen` ist `private static`, `GetGraphAsync` braucht `ClaimsPrincipal` +
> 250-Knoten-Deckelung → für einen Batch-Sweep ungeeignet) **und** dieselben Konflikt-Kanten wie S3 erneut
> gezählt (Doppelzählung). Weglassen macht V1 einfacher *und* sauberer. V2: eigener leichter Degree-Count
> nur aus Standard-Kanten (ohne Konflikt/Bündnis) + Mitgliederstern, mit 10-Min-Cache.

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
  `roh = 4.92 + 0.6·3.18 = 6.83` → **S1 = 55·(1−e^−1.138) = 37.4**.
- Organisation: ~22 Mitglieder (9.2) + Struktur 6 + 5 Waffenarten (3.2) + 1 Route (1.2) → **S2 = 19.6**.
- 2 Konflikte + 1 Bündnis (`roh = 5`) → **S3 = 20·(1−e^−1.25) = 14.3**.
- `Inhalt = 37.4 + 19.6 + 14.3 = 71.3`.
- Einstufung **Verdachtsfall** → `Score = 50 + 50·0.713 = 86` → **GefaehrdungsStufe.Kritisch**, Konfidenz ~85 %.
- UI: „Bedrohungs-Score **86** (Kritisch) · Konfidenz 85 %" — Treiber: Aktivitäts-Heat +37 · Organisation +20
  · Konflikte +14, angehoben ins Verdachtsfall-Band (≥50).

Sanity-Check der Verteilung (Band-Projektion verhindert Kollaps): Verdachtsfall mit Inhalt 10 → 55 (Hoch),
mit Inhalt 40 → 70 (Hoch), mit Inhalt 71 → 86 (Kritisch). Unbekannt mit Inhalt 71 → 71 (Hoch), mit 80 →
Kritisch. → gute Binnen-Differenzierung in jeder Klasse.

---

## 10. Zentrale Konstanten (hartkodiert, eine Stelle — kein Admin-UI)

`BedrohungsScoreKonstanten`:
- Halbwertszeit **90 Tage** · S1-Sättigungs-Nenner **6** · Pro-Person-Dok-Cap **8** · `0.6`-Gewicht dokHeat.
- Caps **55 / 25 / 20** · Sockel **0 / 12 / 50 / 75** · Triage-Schwelle **50** · Konfidenz-Frische **180 Tage**.
- `artGewicht`-Tabelle (schwer/mittel/leicht) · `ausgangGewicht`-Tabelle.

**Kalibrier-Hinweis:** nach dem ersten Echtdaten-Lauf einmal so justieren, dass die 2–3 unstrittig
gefährlichsten Fraktionen ≥75 erreichen und die Verteilung streut (nicht alle im Mittelfeld); danach einfrieren.

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

- **Person-Score:** gleiche 3-Schicht-Architektur, Eingangsfelder Person.Doks / Observationen /
  PersonBeziehung (Feind/Verbündeter) / Lebensstatus (Flüchtig); 4 neue Person-Spalten + Migration.
  **Kein** Person→Fraktion→Person-Kontext-Bonus (vermeidet Doppelzählung). Datenreichtum
  (Aliase/Fahrzeuge/Telefone) nur in die Person-Konfidenz, nie in den Score.
- **Netzwerk-Zentralität (Teil-Score):** eigener leichter Degree-Count (nur Standard-Kanten + Mitgliederstern)
  + 10-Min-Cache.
- **Statistik-Reports/Export + automatischer Lagebericht** (Rest von Block D) — eigener Plan.
