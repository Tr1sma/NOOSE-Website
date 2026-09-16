# Abgleich Dienstverordnung ↔ Website

Stand: 16.09.2026 · Grundlage: Dienstverordnung in der Fassung vom 02.09.2026
(Dokument `79cd84df-5327-47be-8c26-63359f6e7ab6`, gedruckt am 16.09.2026)

Dieses Dokument hält fest, was beim Abgleich herauskam. Teil A ist erledigt, Teil B und C
brauchen eine Entscheidung von euch.

---

## A. Behoben

### A1. Handbuch widersprach der DVO

Vier Stellen behaupteten, ein Junior Agent könne HRB sein und Module abhaken. Die DVO sagt in
§2.4.1: **Beitritt zu TRU und HRB erst ab Special Agent.**

| Stelle | vorher |
|---|---|
| Handbuch *Wer darf was* | „Ein Kennzeichen hängt nicht am Dienstgrad; ein Junior Agent kann HRB sein." |
| Handbuch *Ausbildungsmodule* | „ein Junior Agent mit diesem Kennzeichen darf es also auch" |
| Handbuch *Konten freigeben und verwalten* | „ein Junior Agent kann HRB sein" |
| Schaubild *Dienstgrad und Kennzeichen* | „Ein Junior Agent kann HRB sein / und Module abhaken." |
| Glossar *TRU*, *HRB* | je „unabhängig vom Dienstgrad" |

Die Aussage war **technisch** richtig (das Kennzeichen ist ein eigenes Feld) und
**dienstlich** falsch. Die neuen Texte trennen beides, statt eines davon wegzulassen.

### A2. Die Software erlaubte es tatsächlich

`TruSetAsync` und `HrbSetAsync` prüften keinen Rang — ein Junior Agent konnte HRB bekommen.
Jetzt gilt die Schwelle in `Services/DepartmentRules.cs` und wird an vier Schreibpfaden
durchgesetzt (Kennzeichen setzen, Konto freigeben, Bewerber hochstufen). Die Schalter in
`/admin/agenten`, `/admin/freigaben` und im Bewerber-Panel sperren auf demselben Prädikat.

Zwei Dinge dazu, die nicht offensichtlich sind:

- **Entfernen bleibt auf jedem Rang erlaubt.** Sonst säße ein Altbestand mit dem Kennzeichen
  fest, weil das Setzen-Verbot auch das Abschalten blockiert hätte.
- **Eine Herabstufung nimmt die Kennzeichen mit.** Ohne das behielte ein degradierter Agent
  sein HRB-Tor — die Guards hätten den Zustand nie erzeugt, aber klaglos behalten.

### A3. Nebenbefund: fehlende Schreibprüfung

`TruSetAsync`, `HrbSetAsync` und `RankChangeAsync` trugen — anders als `TeamLeadSetAsync`
daneben — kein `Permission.RequireWriteAccess`. Die Nur-Lese-Aufsicht erreichte die Schalter
und wurde erst beim Speichern von der Schreibsperre gestoppt. Ergänzt. Funktional verliert
niemand etwas; die Meldung kommt nur früher und verständlicher.

### A4. Ungenauigkeit im Handbuch (unabhängig von der DVO)

Der Artikel *Verschlusssachen* sagte, VS-TRU sehe „nur Konten mit dem Kennzeichen TRU".
Tatsächlich sieht die gesamte Führung und die Nur-Lese-Aufsicht es ebenfalls
(`DocumentViewerScope.CanSee`). Korrigiert.

### A5. Neu im Handbuch

Zwei Kapitel, 16 Artikel, 26 Glossarbegriffe, ein Schaubild:

- **Dienstvorschrift** — Dienstverordnung, Sicherheitsfreigaben, Dienstgrade, Abteilungen,
  Aus- und Fortbildungen, Beförderungskriterien, Verhalten im Dienst, Weisungsbefugnis,
  Sanktionsstufen, Regeldienst, Zusammenarbeit mit anderen Behörden.
- **Ausrüstung & Einrichtungen** — Dienstkleidung und Bewaffnung, Funkverkehr,
  Dienstfahrzeuge, Dienstgebäude, erstattungsfähige Dienstmittel.

Dazu wurden bestehende Artikel ergänzt: Asservatenkammer (Eintragungspflicht nach §3.5),
Operationen (Einsatzberichtspflicht nach §4.6.2), Taskforces (Weisungsbefugnis der
Einsatzleitung nach §3.6), Personalakte (Vermerke als Ort der Sanktionen), Beförderung,
Finanzierungen, Profil (Dienstnummer und Codename), Dokumenten-Bibliothek.

---

## B. Die Dienstverordnung ist in sich nicht schlüssig

Das sind Fragen an euch, nicht an die Software. Ich habe im Handbuch jeweils die vorsichtige
Lesart gewählt und den Konflikt benannt, statt ihn zu entscheiden.

| # | Fundstelle | Problem |
|---|---|---|
| B1 | §3.3 gegen §1.1 | §3.3 erklärt **alle** dienstlich erlangten Informationen pauschal zur Geheimhaltungsstufe 5 „Top Secret". §1.1 beschreibt Confidential zugleich als Grundstufe für interne Informationen. Beides zusammen geht nicht |
| B2 | §1.1 | „Zugriff für sämtliche Special Agenten" — hat ein **Junior Agent** damit Confidential oder nicht? Der Dienstgrad steht in der Rangliste direkt darüber |
| B3 | §6.1 dreimal vergeben | Funkcodes, Dienstnummer und Codenamen sowie Funkpflicht tragen alle die Nummer §6.1. Vermutlich gemeint: §6.1, §6.2, §6.3 |
| B4 | §7.1 zweimal vergeben | Regelungen und Freigaben. Tarnfahrzeuge wären dann §7.3 statt §7.2 |
| B5 | §3.7 Stufe 3 | verweist auf „§5.2 und §7.1 **dieser Grundeinweisung**" — das Dokument ist die Dienstverordnung, nicht die Grundeinweisung. Und §7.1 ist wegen B4 doppeldeutig |
| B6 | §5.2.1, Stufe Special Agent | „Pump Shotgun, Dienstgewehr, • Nur während Tarnung als METRO Mitglied" — die dritte Zeile ist ein Zusatz, keine Waffe. Auf welche der beiden er sich bezieht, steht nicht da |
| B7 | §7.1 Freigaben | „Rang 1…4" und darunter „Verfügbar ab dem Dienstgrad des *Special Agents*". Unklar, ob Rang = Dienstgrad oder eine eigene Fahrzeugstufe ist, und worauf sich der Nachsatz bezieht |
| B8 | §9.1 | „…sind entsprechend über den Ausgang des Fluchttunnel über den Fluchttunnel zu transportieren" — die Passage ist doppelt |
| B9 | Schreibweise | Die DVO schreibt durchgehend „Human **Resource** Branch", das Handbuch schrieb bisher „Human **Resources** Branch". Angeglichen an die DVO; die alte Schreibweise bleibt als Synonym im Glossar, damit die Suche beide findet |
| B10 | Tippfehler | „Entbidung" (§3.7 Stufe 6), „Haupfunkfrequenz" (5×), „Disziplinarmassnahmen", „unter strengen Risikoabwägung", „Musik Übertragungen" |

B6 und B7 stehen im Handbuch mit dem ausdrücklichen Hinweis, im Zweifel bei der Führungsebene
nachzufragen. Sobald ihr euch festlegt, ersetze ich das durch die eindeutige Regel.

---

## C. Wofür die DVO eine Stelle auf der Seite vorsieht, die es noch nicht gibt

| # | DVO | Zustand |
|---|---|---|
| C1 | §2.3 nennt sechs Pflichtausbildungen | Die Ausbildungsmodule werden **leer** ausgeliefert. Nach eurer Entscheidung bleibt das so — das Handbuch nennt die sechs, anlegen muss sie die Führung unter *Einstellungen* |
| C2 | §4.6.2 verlangt einen Einsatzbericht „entsprechend der behördeninternen Vorlage" | Es gibt **keine ausgelieferte Vorlage** — weder als Dokument- noch als Aktivitätsvorlage. Der Bericht selbst ist eine *Operation*; die Vorlage müsst ihr anlegen oder ich baue eine |
| C3 | §8.0 listet die erstattungsfähigen Mittel samt Höchstmengen | Der Finanzierungskatalog ist außerhalb des Demo-Modus **leer**. Die Liste steht jetzt im Handbuch; als Katalogeinträge (mit Preis, Zuschuss, Mindestdienstgrad) müsste sie jemand einpflegen |
| C4 | §2.2: HRB hat ein Mitgliederlimit von fünf | **Nicht erzwungen.** Eine harte Sperre blockiert die legitime Übergabe, wenn der sechste eintritt, bevor der fünfte geht. Steht im Handbuch; sag Bescheid, wenn es hart sein soll |

---

## Hinweis zum Ausrollen

Der Handbuch-Seeder fasst **nichts** an, was jemand redaktionell bearbeitet hat
(`IstAngepasst`). Wurde einer der korrigierten Artikel auf der Produktionsinstanz schon von
Hand geändert, bleibt dort der alte Text stehen — das ist so gewollt, muss beim Prüfen nach
dem Deploy aber mitgedacht werden. Betroffen wären vor allem *Wer darf was*,
*Ausbildungsmodule* und *Konten freigeben und verwalten*.
