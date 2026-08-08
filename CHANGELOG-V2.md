# NOOSE V2 — Was ist neu?

Alle Neuerungen gegenüber der bisherigen Version.

---

## Neue Bereiche

### NOOSEI — die Behörden-KI
- Neuer Bereich **NOOSEI** mit Chat zur Aktenlage
- Greift auf die echte Aktendatenbank zu und antwortet mit Quellenangabe (Name + Aktenzeichen)
- Beantwortet u. a. „Wie hängen A und B zusammen?", „Was ist bei X passiert?", „Zeig mir meine Akten"
- Zeigt nur, was der fragende Agent auch selbst sehen darf
- **KI-Kurzbrief** direkt in jeder Akte: Kurzfassung, Risikoeinschätzung, wichtigste Punkte
- **Schreibhilfe im Texteditor**: Rechtschreibkorrektur und Formulierungshilfe per Knopfdruck
- Änderungsvorschau mit farbiger Markierung (neu / entfernt) vor dem Übernehmen
- Gesprächsverlauf wird gespeichert
- Persönliches Kontingent je Agent, jederzeit im eigenen Profil einsehbar

### Asservatenkammer
- Ein- und Auslagerung von Gegenständen mit laufendem Bestand
- Artikelkatalog mit Bild, Kategorie und Beschreibung
- Besitzer je Artikel: NOOSE, ein Agent oder eine Person
- Buchungsverlauf je Artikel, Bestand wird automatisch berechnet
- Sammel-Löschen mehrerer Einträge
- Eigenes Aktenzeichen und Druckansicht

### Entführungen
- Erfassung von Entführungen eigener Agenten
- Täter, Hergang, Dauer und Ausgang (in Gefangenschaft, geflohen, befreit, freigelassen, getötet, Lösegeld gezahlt)
- Dokumentation, welche Informationen abgeflossen sind
- **Kompromittierte Akten** werden markiert und können später wieder freigegeben werden
- Benachrichtigung der Führung bei neuer Entführung
- Druckansicht

### Informanten (V-Personen)
- Verwaltung vertraulicher Quellen mit Zuverlässigkeit und Kontaktdaten
- Protokoll aller Treffen
- Verknüpfbar mit Personen- und Fraktionsakte
- Streng begrenzte Sichtbarkeit: nur Führung und der zuständige Führungsagent
- Eigener Abschnitt in der Fraktionsakte

### Kasse
- Zwei getrennte Konten: **Schwarzgeld** und **Grüngeld**
- Ein-, Aus- und Korrekturbuchungen mit laufendem Kontostand
- Buchungsvorlagen für wiederkehrende Vorgänge
- Schnellbuchungsleiste
- Vollständiger Kassenverlauf, Druckansicht je Buchung

### Finanzierungen
- Agenten stellen Finanzierungsanträge aus einem Artikelkatalog
- Monatsbudget je Agent, jederzeit einsehbar
- Ablauf: beantragt → genehmigt/abgelehnt → ausgezahlt
- Führung kann Mengen kürzen, Genehmigungen zurücknehmen und Auszahlungen stornieren
- Auszahlung wird automatisch in der Kasse verbucht
- Übersicht aller Budgets, Sonderbudgets pro Agent möglich
- Anträge auch in der Personalakte sichtbar
- Zähler offener Anträge direkt in der Navigation

### Nachweis & Gegenaufklärung
- Neue Sammelseite für die gesamte behördeninterne Nachvollziehbarkeit
- **Chronik**: behördenweiter Ereignis-Feed nach Tagen gruppiert, inkl. Änderungsdetails
- Kennzahlen-Kacheln und Aktivitätsband mit Klick-auf-Tag-Filter
- **Änderungsprotokoll** und **Zugriffsprotokoll** (wer hat wann welche Akte angesehen)
- **Gegenaufklärung**: Lagebild, Zugriffs-Heatmap, Agentenprofile und Auffälligkeiten
- Eigene Regeln für Auffälligkeiten anlegbar, mit Live-Vorschau vor dem Speichern
- Alle Filter bleiben in der Adresszeile erhalten (teilbar)

### Ermittlungshinweise
- Automatische Hinweise auf mögliche Verbindungen zwischen Akten
- Meldung neuer Konflikte
- Warnung bei veralteten hohen Einstufungen
- Hinweise können dauerhaft ausgeblendet werden

### Bestenliste
- Team-Ranking nach dokumentierter Ermittlungsarbeit
- Auswertung nach Zeitraum (Woche, Monat, gesamt)
- Abzeichen für Meilensteine (erste Akte, 25 Akten, 100 Akten, Dokumentar, Netzwerker, Analyst u. v. m.)
- Automatische Auszeichnung des besten Agenten
- Discord-Benachrichtigung bei Auszeichnungen

### Feedback
- Neue Seite, um Rückmeldungen zur Website zu geben
- Kategorien (Fehler, Wunsch, Sonstiges) und Bearbeitungsstatus
- Führung sieht alle Rückmeldungen mit Detailansicht
- Benachrichtigung bei neuem Feedback

---

## Verbesserungen an bestehenden Bereichen

### Suche
- Findet jetzt auch ähnlich klingende Namen (Maier / Meyer / Mayr / Meier)
- Erkennt Wortformen: „Verhaftung" findet auch „Verhaftungen"
- Deutlich bessere Treffer bei Tippfehlern
- Sucht zusätzlich in Asservaten, Kasse, Finanzierungen und Entführungen
- Spürbar schnellere Ergebnisse durch neuen Suchindex

### Beziehungsgraph
- **Schlüsselfiguren** werden automatisch erkannt und golden hervorgehoben
- **Gruppenerkennung**: zusammengehörige Netzwerke werden farblich eingefärbt
- **Zeitregler**: Wachstum des Netzwerks über die Zeit abspielen
- Gespeicherte Ansichten je Agent — Anordnung bleibt erhalten
- Verknüpfungen direkt im Graph per Ziehen zwischen zwei Akten anlegen
- Beim Verschieben eines Knotens ziehen benachbarte Akten mit
- Akten-Kurzinfo direkt im Graph aufrufbar

### Statistik
- Komplett neu aufgebaut mit zehn Abschnitten: Überblick, Bestand, Aktivität, Bedrohungslage, Entführungen, Netzwerk, Kasse, Finanzierungen, Dienststelle, Lageberichte
- Neue Diagrammarten: Jahres-Heatmap, Bestandsbaum, Score-Rennen, Einstufungs-Fluss
- Neue Filterleiste für Zeitraum und Umfang
- Personalauswertung (Dienststelle) und Netzwerkauswertung neu
- Verbesserter CSV-Export

### Bedrohungs-Score
- **Verlauf** wird jetzt gespeichert — Score-Entwicklung über die Zeit sichtbar
- Mini-Verlaufskurve direkt in der Akte
- Trend-Auswertung mit Top-Aufsteigern
- **Warnmeldung an die Führung** bei plötzlichem Score-Anstieg

### Akten allgemein
- Zeitstrahl mit Kategorie-Filtern und übersichtlicherer Darstellung
- Bedrohungs-Verlauf als eigener Abschnitt in Personen- und Fraktionsakten
- Aktualitäts-Ampel für Fraktionen jetzt nach Themenbereichen getrennt
- Druckansichten erweitert

### Bewerbungen
- Automatisches Anlegen eines Vorgangs zu jeder Bewerbung (optional)
- Neuer Abschnitt **Anforderungen**: Voraussetzungen für die Aufnahme hinterlegen
- Neuer Abschnitt **Automatik** zur Steuerung der Abläufe
- Vorlagen für Bewerbungsvorgänge

### Texteditor
- Bilder-Handhabung verbessert (Bilder bleiben bei KI-Korrekturen erhalten)
- Neue KI-Werkzeuge direkt in der Werkzeugleiste
- Tabellen-Werkzeug stabiler

### Benachrichtigungen
- Vier neue Arten: Bedrohungs-Anstieg, Agenten-Entführung, Finanzierung, Feedback
- Discord-Weiterleitung für die neuen Kategorien
- Sammelmeldungen bei beobachteten Akten übersichtlicher

### Verwaltung & Einstellungen
- **NOOSEI-Betrieb**: Zustandsanzeige, Anfrageprotokoll und Detailansicht einzelner Anfragen
- **Kontingent-Verwaltung**: Regeln je Rang, Übersicht aller Agenten, Aufstocken im Einzelfall
- **Finanzierungsregeln**: Budgets und Katalog konfigurieren
- **Bester Agent**: Auszeichnung und Ping konfigurieren
- Protokoll-Ansichten aus den Einstellungen in den neuen Bereich „Nachweis" verschoben

### Papierkorb
- Auch Asservate, Kassenbuchungen, Finanzierungsanträge, Entführungen und Informanten sind wiederherstellbar

---

## Sonstiges
- Zahlreiche Fehlerbehebungen in Anzeige, Filtern und Formularen
- Überarbeitetes Erscheinungsbild an mehreren Stellen
- Neue Hintergrundprozesse für Abzeichen, Auszeichnungen und Suchindex
