# Design: Steckbrief-Vorschläge (Katalog) + Chip-Eingabe

**Datum:** 2026-06-09
**Status:** Genehmigt (Umsetzung gestartet)

## Problem / Ziel

Im Steckbrief-Editor (`SteckbriefForm`) werden **Waffen**, **Fahrzeuge** und **Orte**
aktuell als vertikale Liste voller-Breite-Textfelder erfasst. Zwei Wünsche:

1. **Vorschläge per Dropdown:** Beim Erfassen sollen bereits bekannte Werte als
   Autocomplete-Vorschläge erscheinen. Ein neuer Wert (z. B. „Karabiner") wird
   hinterlegt, damit ihn die nächste Person als Vorschlag bekommt.
2. **Chip-Darstellung:** Die erfassten Werte sollen als Chips **nebeneinander**
   (umbrechend) erscheinen statt untereinander in voller Breite.

## Entscheidungen (mit dem Nutzer abgestimmt)

- **Vorschlagsquelle:** Eigene **Katalog-Tabelle** (nicht aus den Personendaten
  abgeleitet) — wächst unabhängig, später admin-kuratierbar.
- **Zweitfelder behalten:** Kennzeichen (Fahrzeug) und Notiz (Ort) bleiben erhalten
  und werden im Chip als „Hauptwert · Zusatz" angezeigt.
- **Aliase & Telefonnummern** bleiben unverändert (persönlich, keine geteilte
  Vokabel, kein Chip-Umbau).

## Datenmodell — Katalog

Neue, schlanke Referenz-Tabelle (kein Audit / kein Soft-Delete):

```csharp
public class SteckbriefVorschlag
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public VorschlagTyp Typ { get; set; }
    public string Wert { get; set; } = string.Empty;
}

public enum VorschlagTyp { Waffe, Fahrzeug, Ort }
```

- **Unique-Index `(Typ, Wert)`** — die MySQL-Collation ist case-insensitiv, daher
  gelten „Karabiner" und „karabiner" als ein Eintrag (keine Duplikate).
- `Wert` mit sinnvoller Maximallänge (z. B. 200).
- Eine EF-Migration.

## Service — `ISteckbriefVorschlagService`

```csharp
Task<IReadOnlyList<string>> GetAsync(VorschlagTyp typ, CancellationToken ct = default);
Task VormerkenAsync(VorschlagTyp typ, IEnumerable<string> werte, CancellationToken ct = default);
```

- `GetAsync` → alphabetisch sortierte, distinkte Werte eines Typs für das Autocomplete.
- `VormerkenAsync` → ermittelt fehlende Werte (case-insensitiv) und fügt sie dem
  Change-Tracker hinzu, **ohne selbst zu speichern**. So werden sie im selben
  `SaveChanges` wie die Person persistiert (atomar). Der Unique-Index schützt gegen
  seltene Races (rollt im Konfliktfall den Speichervorgang zurück).
- Nutzt denselben scoped `AppDbContext` wie `PersonService`.
- In DI als Scoped registriert.

## Befüllung & Verschlusssachen-Schutz

`PersonService.ErstellenAsync` / `AktualisierenAsync` rufen nach dem Mappen der
Kinder, vor `SaveChanges`, `VormerkenAsync` für die drei Typen auf
(Waffe → `Text`, Fahrzeug → `Bezeichnung`, Ort → `Text`) — **nur wenn
`!person.IstVerschlusssache`**. Dadurch gelangen als Verschlusssache geführte Werte
nicht in die geteilte Vorschlagsliste. Da der Katalog so ausschließlich
unklassifizierte Vokabeln enthält, dürfen alle Agenten alle Vorschläge sehen —
kein Lese-Filter nötig.

`PersonService` erhält dafür `ISteckbriefVorschlagService` als zusätzliche Abhängigkeit.

## UI — wiederverwendbare Chip-Komponente

Neue Komponente `SteckbriefMehrfachFeld<TItem>` (ein Baustein für alle drei Felder
statt dreifacher Duplizierung):

- Rendert vorhandene Einträge als umbrechende **Chips** („Hauptwert · Zusatz",
  x zum Entfernen).
- Add-Zeile: **MudAutocomplete** für den Hauptwert (Vorschläge aus `GetAsync`,
  freie Eingabe neuer Werte via `CoerceValue`) + optionales kleines Zusatzfeld
  (Kennzeichen/Notiz, abschaltbar) + „+"-Button zum Übernehmen.
- Bindet direkt an die `PersonEingabe`-Liste (`Items`): Add/Remove mutieren die echte
  Liste → kein Rück-Synchronisieren nötig.
- Lädt die Vorschläge einmalig in `OnInitializedAsync` und filtert pro Tastendruck
  in-memory (keine DB-Last je Anschlag).
- Dedupliziert innerhalb derselben Person (kein doppelter Chip).

Mini-Interface zur generischen Bindung:

```csharp
public interface ISteckbriefMehrfach
{
    string Hauptwert { get; set; }
    string? Zusatz { get; set; }
}
```

`WaffeEingabe` (Zusatz entfällt → no-op/null), `FahrzeugEingabe`
(Bezeichnung/Kennzeichen) und `OrtEingabe` (Text/Notiz) implementieren es **explizit**
auf ihre echten Felder. Die Eingabemodelle ändern sich sonst nicht; `PersonService`
und die Lese-Ansicht bleiben unberührt. Constraint: `where TItem : ISteckbriefMehrfach, new()`.

## `SteckbriefForm` umbauen

Die drei Abschnitte Waffen/Fahrzeuge/Orte nutzen künftig `SteckbriefMehrfachFeld`
(Waffe ohne Zusatzfeld; Fahrzeug/Ort mit). Aliase & Telefonnummern bleiben als Zeilen.
Wirkt automatisch in *Anlegen* und *Bearbeiten* (beide nutzen `SteckbriefForm`). Die
Lese-Ansicht `SteckbriefListe` zeigt bereits Chips → unverändert.

## Betroffene Dateien

**Neu:**
- `Models/Enums/VorschlagTyp.cs`
- `Data/Entities/Personen/SteckbriefVorschlag.cs`
- `Services/ISteckbriefVorschlagService.cs`, `Services/SteckbriefVorschlagService.cs`
- `Components/Pages/Personen/Shared/SteckbriefMehrfachFeld.razor`
- 1 EF-Migration

**Geändert:**
- `Data/AppDbContext.cs` (DbSet + Unique-Index + Länge)
- `Program.cs` (DI-Registrierung)
- `Services/PersonService.cs` (Vormerken-Aufrufe, neue Abhängigkeit)
- `Models/Personen/PersonEingabe.cs` (Interface auf den 3 Eingabe-Klassen)
- `Components/Pages/Personen/Shared/SteckbriefForm.razor`

## Erfolgskriterien

- Beim Erfassen von Waffen/Fahrzeugen/Orten erscheinen bekannte Werte als
  Autocomplete-Vorschläge; ein neuer Wert wird übernommen und steht künftig als
  Vorschlag bereit.
- Werte als Verschlusssache geführter Personen erscheinen **nicht** im Katalog.
- Erfasste Einträge werden als Chips nebeneinander dargestellt; Kennzeichen/Notiz
  bleiben sicht- und entfernbar.
- Anlegen und Bearbeiten funktionieren weiterhin; bestehende Daten bleiben erhalten.
- `dotnet build` ist sauber.
