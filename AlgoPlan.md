# AlgoPlan.md — EHK-/Bedrohungs-Score

Spezifikation des automatischen Bedrohungs-Scores („EHK-Score") für **Fraktionen** (S1–S4) und **Personen**
(P1–P5). Der Score ist ein Wert **0–100** je Subjekt plus eine **Konfidenz 0–100 %**. Quelle der Wahrheit ist
der Code (`Services/Threat/`); dieses Dokument beschreibt das Modell und die Invarianten. Die konkreten
Zahlen sind zur Laufzeit über `/admin/bedrohungs-score` einstellbar (`ThreatScoreConfiguration`); die hier
genannten Werte sind die **Defaults** (`ThreatScoreConfiguration.Default()`).

## Überblick

Der Score entsteht in drei Schritten:

1. **Content (0–100)** — die Summe der Teilscores. Fraktion: `content = S1 + S2 + S3 + S4`. Person:
   `content = P1 + P2 + P3 + P4 + P5`. Jeder Teilscore ist gegen seinen **Cap** gedeckelt; die Caps summieren
   je Subjekt auf **100** (siehe Invarianten), damit `content` eine saubere 0–100-Skala bildet.
2. **Band-Projektion** — die Einstufung (Prüffall/Verdachtsfall/Gesichert staatsgefährdend) setzt einen fixen
   **Sockel** `base`; der Content wird in das Band `[base, 100]` projiziert:
   `score = base + (100 − base) · content / 100`, gerundet und auf 0–100 geklemmt.
3. **Konfidenz** — separater Datengüte-Wert (senkt den Score nie), gewichtete Summe von „liegt Datum X vor?"-Bits.

Zeitlicher Verfall: alle Heat-Beiträge (Aktivitäten, Doks, Observationen) werden mit `Decay` gealtert.

## Geteilte Primitive

| Primitive | Formel | Default-Parameter |
|-----------|--------|-------------------|
| **Saturate** (Teilscore-Deckelung) | `points = cap · (1 − e^(−raw / denom))` | je Teilscore eigener `cap`, `denom` |
| **Decay** (Halbwertszeit) | `decay(t) = 0.5^(alterTage / halbwertszeit)`, Zukunft → Alter 0 | `HalfLifeDays = 90` |
| **BandScore** (Band-Projektion) | `score = round(base + (100 − base) · content / 100)`, clamp 0–100 | Sockel `base` s. u. |

- `Saturate` bildet einen unbeschränkten Roh-Beitrag `raw` asymptotisch auf `[0, cap)` ab: kleine `denom` →
  schneller Anstieg gegen den Cap, große `denom` → flacher.
- Der theoretische Maximal-`content` ist die Summe aller Caps → **muss 100 sein** (Invarianten).

### Fixe Einstufungs-Sockel (`ThreatScoreConstants.Base`)

| Einstufung | Sockel `base` |
|------------|---------------|
| Gesichert staatsgefährdend | **75** |
| Verdachtsfall | **50** |
| Prüffall | **12** |
| Unbekannt | **0** |

Diese Sockel und die Stufen-Schwellen (25/50/75 für die Gefährdungs-Bänder No/Low/Medium/High/Critical) sind
bewusst **fix** und nicht über die Admin-UI änderbar.

## Fraktion — Teilscores S1–S4

Staatsfraktionen (`IsStateFaction`) sind ausgenommen → `score = null`.

| # | Name | Roh-Beitrag `raw` | Deckelung | Default-Cap · Denom |
|---|------|-------------------|-----------|---------------------|
| **S1** | Aktivitäts- & Maßnahmen-Heat | `Σ KindWeight(art)·decay(aktivität)` + `DocHeatWeight · Σ min(PerMemberDocCap, Σ OutcomeWeight·decay(dok))` | Saturate | Cap 55 · Denom 6 |
| **S2** | Organisation & Reichweite | `Größe⊕ + Struktur + Waffen⊕ + Infrastruktur⊕` | `min(CapS2, Σ)` | Cap 22 |
| **S3** | Konflikt & Bündnis | `ConflictWeight·#Konflikte + AllianceWeight·#Bündnisse + AbductionWeight·Entführungs-Hostilität` | Saturate | Cap 15 · Denom 4 |
| **S4** | Netzwerk-Zentralität | `Grad der sonstigen Default-Kanten` | Saturate | Cap 8 · Denom 4 |

`⊕` = per `Saturate` gedeckelt. **S2-Bausteine** (Default):
- **Größe** = `Saturate(max(geschätzt, aktiveMitglieder), CapSize=10, SizeDenom=15)`
- **Struktur** = `min(RanksMaxPoints=3, #Ränge) + (aktiveLeitung ? LeadPoints=2 : 0) + (Anwesen ? EstatePoints=1 : 0)`
- **Waffen** = `Saturate(#Waffenarten, CapWeapons=3, WeaponsDenom=3)`
- **Infrastruktur** = `Saturate(DrugRouteWeight=2·#Drogenrouten + #Inventar, CapInfra=3, InfraDenom=4)`

**Art-Gewichte** (`KindWeight`, Schlagwort-basiert): schwer=3 (Mord/Entführung/Anschlag/…), mittel=2
(Raub/Überfall/Schießerei/Waffenhandel/…), leicht=1 (sonst). **Ausgang-Gewichte** (`OutcomeWeight`):
Erschossen=2,0 · Spritze=1,5 · läuft noch=1,2 · entlassen=1,0.

## Person — Teilscores P1–P5

| # | Name | Roh-Beitrag `raw` | Deckelung | Default-Cap · Denom |
|---|------|-------------------|-----------|---------------------|
| **P1** | Maßnahmen-Heat | `Σ OutcomeWeight(ausgang)·decay(dok)` | Saturate | Cap 40 · Denom 4 |
| **P2** | Bewaffnung & Eskalation | `Waffen⊕ + (flüchtig ? FugitivePoints : 0)` | `min(CapP2, Σ)` | Cap 22 |
| **P3** | Observations-Heat | `Σ (laufend ? 1 : ObsCompletedWeight=0,6)·decay(start)` | Saturate | Cap 18 · Denom 3 |
| **P4** | Soziale Gefahr | `EnemyW·#Feinde + AllyW·#Verbündete + GpW·#Geschäftsp. + LeadW·#Leitungsrollen + AbductionW·Entführungs-Hostilität` | Saturate | Cap 12 · Denom 4 |
| **P5** | Netzwerk-Zentralität | `Grad der sonstigen Default-Kanten` | Saturate | Cap 8 · Denom 4 |

**P2-Bausteine** (Default): Waffen = `Saturate(#Waffenarten, PersonCapWeapons=14, Denom=2)`; Flüchtig-Bonus
`FugitivePoints=8` (nur wenn effektiver Lebensstatus = flüchtig). **P4-Gewichte** (Default): Feind=2 ·
Verbündeter=1 · Geschäftspartner=1 · Leitungsrolle=1,5. Personen-Score nutzt bewusst **nur personen-eigene
Daten** (keine Fraktions-Scores) → keine Zirkularität.

**Entführungs-Hostilität** (Fraktion S3 wie Person P4): Summe über alle vom Täter (Fraktion bzw. Person)
begangenen Agenten-Entführungen mit Pro-Vorfall-Gewicht `1 + 0,5·Schweregrad(0–4, nur bei Informationsabfluss)
+ (Wahrheitsserum ? 0,5 : 0) + (getötet ? 1 : 0)`, skaliert mit `AbductionWeight`/`PersonAbductionWeight`
(Default je 2,5). Nicht zeitlich gedämpft (wie Konflikt/Bündnis). Personengruppen tragen keinen Score.

## Konfidenz (Datengüte, 0–100 %)

Gewichtete Summe von Ja/Nein-Bits; senkt den Score nie.

**Fraktion:** 0,30·(Aktivität∨Dok) + 0,20·(aktiveMitglieder>0) + 0,15·(Bestände>0) + 0,10·(geschätzteGröße
gesetzt) + 0,10·(Einstufung≠unbekannt) + 0,15·(letzte Erfassung frisch, `ConfidenceFreshDays=180`).

**Person:** 0,30·(Dok∨Observation) + 0,15·(Waffen>0) + 0,10·(Einstufung≠unbekannt) + 0,10·(soziale Kanten>0)
+ 0,10·(Mitgliedschaften>0) + 0,10·(Datenreichtum>0) + 0,15·(frisch).

## Triage

`triage = content ≥ TriageThreshold (Default 50) ∧ Einstufung == unbekannt` — markiert unbewertete Subjekte mit
auffälligem Content zur Sichtung.

## Cap-Invarianten (load-bearing)

Die Admin-UI und `ThreatScoreConfigService.Validate` erzwingen vier Summenregeln; ohne sie skaliert `content`
nicht sauber auf 0–100:

1. `CapS1 + CapS2 + CapS3 + CapS4 = 100` — Default `55 + 22 + 15 + 8`.
2. `CapSize + (RanksMaxPoints + LeadPoints + EstatePoints) + CapWeapons + CapInfra = CapS2` — Default
   `10 + (3 + 2 + 1) + 3 + 3 = 22` (S2 wird per `min`, nicht `Saturate` gedeckelt → die Bausteine müssen exakt den Cap ergeben).
3. `CapP1 + CapP2 + CapP3 + CapP4 + CapP5 = 100` — Default `40 + 22 + 18 + 12 + 8`.
4. `PersonCapWeapons + FugitivePoints = CapP2` — Default `14 + 8 = 22`.

Zusätzlich: alle Nenner > 0, alle Caps/Gewichte ≥ 0, Schwere-Gewichte monoton (`schwer ≥ mittel ≥ leicht ≥ 1`),
`TriageThreshold` 0–100, `ConfidenceFreshDays` > 0. Die Kalibrier-UI bietet **„Caps ausbalancieren"**
(proportionale Normalisierung auf diese Invarianten) und eine **Verteilungs-Vorschau am Echtbestand**
(Dry-Run mit ungespeicherter Config, ohne Persistierung), um die Gewichte gegen echte Daten einzustellen.

## Berechnung & Persistierung

- `Calculate(input, nowUtc, config)` / `CalculatePerson(...)` sind **reine Funktionen** ohne DB-Zugriff.
- Persistiert wird getrennt via `ExecuteUpdateAsync` (umgeht bewusst den Audit-Interceptor → kein
  „geändert"-Stempel bei jedem Recompute).
- Ausgelöst **event-getrieben** (bei Schreibvorgängen am Subjekt/Kind-Daten) und durch den **nächtlichen Sweep**
  (`ThreatScoreSweepWorker`, 24 h) gegen Decay-Drift. Gespeicherte Scores nutzen die Config vom Zeitpunkt ihrer
  letzten Berechnung → nach Config-Änderung „Alle Scores neu berechnen".

## Referenzen (Code)

- `NOOSE-Website/Services/Threat/ThreatScoreService.cs` — `Calculate`/`CalculatePerson`, `Saturate`/`Decay`/`BandScore`.
- `NOOSE-Website/Services/Threat/ThreatScoreConfiguration.cs` — Caps/Gewichte/Nenner + Defaults, `KindWeight`/`OutcomeWeight`.
- `NOOSE-Website/Services/Threat/ThreatScoreConstants.cs` — fixe Einstufungs-Sockel.
- `NOOSE-Website/Services/Threat/ThreatScoreConfigService.cs` — `Validate` (Invarianten).
- `NOOSE-Website/Models/Threat/ThreatScoreModelle.cs` — Inputs, `ThreatScoreDetail`/`ThreatScoreResult`.
- `NOOSE-Website/Infrastructure/Threat/ThreatScoreSweepWorker.cs` — nächtlicher Sweep.
- `NOOSE-Website/Components/Pages/Admin/ThreatScore.razor` — Kalibrier-UI (`/admin/bedrohungs-score`).
