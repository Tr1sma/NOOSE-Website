# Design: Personen-Doks nachträglich bearbeiten

**Datum:** 2026-06-09
**Status:** Genehmigt (bereit für Implementierungsplan)

## Problem / Ziel

Personen-Doks (Protokolle von Verhören/Maßnahmen) lassen sich aktuell nur
**anlegen**, **schreibgeschützt ansehen** und **löschen** (Soft-Delete). Eine
nachträgliche Bearbeitung fehlt. Tippfehler, nachgereichte Informationen oder ein
falsch gewählter Maßnahme-Ausgang können daher nicht korrigiert werden, ohne das
Dok zu löschen und neu anzulegen.

**Ziel:** Bestehende Doks lassen sich nachträglich bearbeiten — inklusive des
Maßnahme-Ausgangs, dessen Status-Auswirkung beim Speichern neu ausgewertet wird.

## Umfang

### Bearbeitbare Felder
Dieselben Felder wie beim Anlegen:

- `Zeitpunkt` (Datum + Uhrzeit der Maßnahme)
- `Ausgang` (`MassnahmeAusgang`)
- `Grund`
- `Fraktion`
- `ErhalteneInformationen`
- `Wahrheitsserum`

**Nicht** bearbeitbar:
- Die **Person** eines Doks bleibt fix — ein Dok wandert nicht in eine andere Akte.
- `GedaechtnisGeloescht` bleibt **abgeleitet** (= Ausgang `Spritze`), kein eigenes
  Eingabefeld.

### Nicht im Umfang (YAGNI)
- Keine Versionierung / Änderungshistorie über das vorhandene Audit hinaus.
- Keine gesonderte Behandlung mehrerer überlappender Tode auf derselben Person
  innerhalb des 20-Minuten-Fensters (im RP praktisch irrelevant).
- Keine neue Migration — das Datenbankschema bleibt unverändert.

## UX / Einstiegspunkt

Bearbeitung läuft über den **bestehenden `DokDetailDialog`**. Beide Oberflächen
öffnen heute bereits diesen Dialog, erhalten die Bearbeitung also ohne Duplizierung:

- **Akte → `DokPanel`** (Klick auf Dok-Karte, `zeigeZurAkte: false`)
- **Übersicht → `/doks` (`DoksUebersicht`)** (Klick auf Tabellenzeile, `zeigeZurAkte: true`)

### Ablauf
1. `DokDetailDialog` erhält einen **Bearbeiten**-Button.
2. Klick öffnet das vorhandene Formular `DokDialog`, **vorbefüllt** mit den
   aktuellen Werten (Edit-Modus).
3. Speichern ruft den neuen Service-Aufruf `AktualisierenAsync` auf.
4. Der Detail-Dialog aktualisiert die angezeigten Werte und merkt sich intern
   `_geaendert = true`.
5. Beim Schließen gibt der Detail-Dialog `DialogResult.Ok(_geaendert)` zurück.
6. Die aufrufende Oberfläche lädt ihre Liste neu, wenn „geändert" zurückkommt:
   - `DokPanel`: Liste neu laden **und** `PersonChanged` aufrufen (Lebensstatus
     kann sich geändert haben).
   - `DoksUebersicht`: Liste neu laden.

### Wiederverwendung `DokDialog`
`DokDialog` (Formular **ohne** Personen-Auswahl) dient künftig sowohl dem Anlegen
als auch dem Bearbeiten — beim Bearbeiten ist die Person fix, daher ist genau dieses
Formular (nicht `DokAnlegenDialog` mit Personensuche) korrekt, auch wenn aus der
Übersicht heraus bearbeitet wird.

- Optionaler Parameter `Vorhanden` (`PersonDokEingabe?`): wenn gesetzt → Edit-Modus.
- Im Edit-Modus: Felder, Datum und Uhrzeit aus `Vorhanden` vorbefüllen; Titel und
  Button-Text anpassen („Dok bearbeiten" / „Änderungen speichern" statt
  „Neues Dok" / „Dok speichern").
- Rückgabe bleibt `PersonDokEingabe` (mit nach UTC umgerechnetem `Zeitpunkt`).

### Abgelehnte Alternativen
- **(B) Stift-Icon je Listenzeile/Karte:** führt zu doppelter Logik über beide
  Oberflächen; Detail-Dialog bliebe rein lesend.
- **(C) Eigener `DokBearbeitenDialog`:** meiste Code-Duplizierung, kein Reuse.

## Status-Neuauswertung beim Speichern

Der Maßnahme-Ausgang wirkt beim Anlegen auf den Lebensstatus der Person
(`Erschossen` → 20-Minuten-Tot-Fenster; `Spritze` → Gedächtnisverlust). Beim
Bearbeiten wird dieser Effekt **neu ausgewertet** (gewählte Variante: „Status neu
anwenden").

### Besitz-Prüfung
Ein Dok „besitzt" das aktuelle Tot-Fenster der Person genau dann, wenn

```
person.Lebensstatus == Tot && person.TotBis == (alterZeitpunkt + RespawnMinuten)
```

(`alterZeitpunkt` = `Zeitpunkt` des Doks **vor** der Bearbeitung). Diese Prüfung
verhindert, dass ein manuell oder von einem **anderen** Dok gesetzter Status
überschrieben wird. Da Dok-bedingte Tode ihr `TotBis` immer aus dem Maßnahme-
Zeitpunkt ableiten und manuelle Tode aus „jetzt", kollidieren beide praktisch nicht.

### Regeln

| Vorher (`altAusgang`) | Nachher (`neuAusgang`) | Wirkung auf die Person |
|---|---|---|
| ≠ `Erschossen` | `Erschossen` | `Lebensstatus = Tot`, `TotBis = neuerZeitpunkt + RespawnMinuten` (wie beim Anlegen) |
| `Erschossen` | `Erschossen` (Zeitpunkt geändert) | `TotBis = neuerZeitpunkt + RespawnMinuten` — **nur** wenn dieses Dok das Fenster besitzt |
| `Erschossen` | ≠ `Erschossen` | Fenster zurücksetzen: `Lebensstatus = Lebend`, `TotBis = null` — **nur** wenn dieses Dok das Fenster besitzt |
| beliebig | — | `dok.GedaechtnisGeloescht = (neuAusgang == Spritze)` |

### Hinweise
- Der Tod ist temporär und wird **on-read** aus `TotBis` berechnet
  (`LebensstatusLogic.Effektiv`). Das Bearbeiten alter Doks (Zeitpunkt > 20 Min her)
  wirkt sich daher meist nicht sichtbar aus — konsistent mit dem Anlegen eines Doks
  mit altem Zeitpunkt.
- Die „neu Erschossen"-Regel setzt den Tod wie beim Anlegen (ohne Besitz-Prüfung);
  Zurücksetzen/Anpassen eines bestehenden Fensters erfolgt nur bei Besitz, um fremde
  Status nicht zu überschreiben.

## Service & API

Neue Methode in `IPersonDokService` und `PersonDokService`:

```csharp
Task<PersonDok> AktualisierenAsync(
    string dokId,
    PersonDokEingabe eingabe,
    ClaimsPrincipal handelnder,
    CancellationToken cancellationToken = default);
```

**Ablauf in `PersonDokService.AktualisierenAsync`:**
1. Dok **inklusive Person** laden (`Include(d => d.Person)`); fehlt das Dok →
   `InvalidOperationException` (analog `ErstellenAsync`).
2. `altAusgang` und `altZeitpunkt` merken (vor dem Überschreiben).
3. Felder aus `eingabe` übernehmen (`Grund`/`Fraktion`/`ErhalteneInformationen`
   per `Leer(...)` normalisieren wie beim Anlegen).
4. `GedaechtnisGeloescht = (eingabe.Ausgang == MassnahmeAusgang.Spritze)`.
5. Status-Regeln (siehe oben) auf die geladene Person anwenden.
6. `SaveChangesAsync` → Audit-Interceptor setzt `GeaendertAm`/`GeaendertVonId` am
   Dok automatisch; die Person erhält nur dann einen „Geändert"-Audit-Eintrag, wenn
   sich ihr Status tatsächlich geändert hat (EF Change-Tracking).

Die Status-Logik wird als kleiner privater Helfer ausgelagert
(z. B. `StatusNeuAuswerten(Person, MassnahmeAusgang altAusgang, DateTime altZeitpunkt, PersonDokEingabe neu)`),
um `AktualisierenAsync` schlank und testbar zu halten.

## Berechtigungen

Wie bei Anlegen und Löschen: **keine zusätzliche Rolle**. Wer den Dok-Bereich sieht
(`DokPanel` in der Akte bzw. die `/doks`-Übersicht), darf auch bearbeiten. Der
`Bearbeiten`-Button wird genauso wenig rollengated wie heute „Neues Dok" und
„Löschen". `handelnder` (`ClaimsPrincipal`) dient nur dem Audit.

## Betroffene Dateien

| Datei | Änderung |
|---|---|
| `Services/IPersonDokService.cs` | Neue Methode `AktualisierenAsync` |
| `Services/PersonDokService.cs` | `AktualisierenAsync` + privater Status-Helfer |
| `Components/Pages/Personen/Shared/DokDialog.razor` | Optionaler `Vorhanden`-Parameter, Edit-Modus (Vorbefüllung, Titel/Button) |
| `Components/Pages/Personen/Shared/DokDetailDialog.razor` | `Bearbeiten`-Button, internes `_geaendert`, Rückgabe `DialogResult.Ok(_geaendert)` |
| `Components/Pages/Personen/Shared/DokPanel.razor` | Detail-Dialog-Ergebnis auswerten → Reload + `PersonChanged` |
| `Components/Pages/Personen/DoksUebersicht.razor` | Detail-Dialog-Ergebnis auswerten → Reload |

**Keine Migration** — Schema unverändert.

## Erfolgskriterien

- Ein bestehendes Dok lässt sich aus der Akte **und** aus der `/doks`-Übersicht
  öffnen, bearbeiten und speichern; die Liste zeigt danach die neuen Werte.
- Audit-Felder `GeaendertAm`/`GeaendertVonId` des Doks werden beim Speichern gesetzt.
- Ausgangswechsel verhält sich gemäß der Regel-Tabelle; ein fremder/manueller
  Lebensstatus wird nicht überschrieben.
- Person bleibt unveränderbar; `GedaechtnisGeloescht` folgt dem Ausgang.
