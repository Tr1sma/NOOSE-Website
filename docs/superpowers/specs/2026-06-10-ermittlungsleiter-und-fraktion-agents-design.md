# Design: Agent-Zuteilung bei Fraktionen + Ermittlungsleiter

> Stand: 2026-06-10 · Phase 4/5-Erweiterung · Sprache: Deutsch

## 1. Ziel & Kontext

Heute können NOOSE-Agents bereits **Personengruppen** und **Parteien** zugeteilt werden
(`PersonengruppeAgent` / `ParteiAgent`, jeweils mit Panel, Dialog, Service-Methoden und „Zugeteilte
Agents"-Tab). Bei **Fraktionen** fehlt diese Möglichkeit vollständig.

Zusätzlich soll je Akte kenntlich gemacht werden, **wer die Ermittlung leitet**: der Agent, der die
Akte angelegt hat, bzw. von der Führung zugeteilte Agents. Diese werden als **Ermittlungsleiter (EL)**
markiert und prominent **links in der visuellen Steckkarte** angezeigt.

Der Geltungsbereich umfasst die drei Organisations-Aktentypen **Personengruppe, Fraktion, Partei**.
Einzel-**Person**-Akten bleiben unverändert (dort gibt es bewusst keine Agent-Zuteilung).

## 2. Festgelegte Entscheidungen

| Thema | Entscheidung |
|---|---|
| Anzahl Ermittlungsleiter je Akte | **Mehrere gleichzeitig möglich** (Flag, nicht Einzel-FK). |
| Ermittlungsleiter setzen/wechseln | **Nur Führung** (Supervisory Special Agent+ / Admin). |
| Agents zuteilen / entfernen (zuständig machen) | **Führung ODER ein Ermittlungsleiter dieser Akte** (heute offen für jeden aktiven Agent → wird enger gefasst). |
| Ersteller einer Akte | Wird automatisch zugeteilt **und** als Ermittlungsleiter markiert. |
| Geltungsbereich | Personengruppe, Fraktion, Partei. Einzel-Person unverändert. |
| Verschlusssachen-Schutz | Bleibt unverändert bestehen, zusätzlich zu den neuen Guards. |

## 3. Datenmodell

### 3.1 Neue Entität `FraktionAgent`
1:1-Spiegel von `PersonengruppeAgent` / `ParteiAgent` (siehe `Data/Entities/Fraktionen/`):

```csharp
public class FraktionAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Markiert diesen zugeteilten Agent als Ermittlungsleiter der Akte.</summary>
    public bool IstErmittlungsleiter { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
```

### 3.2 Neues Flag auf den bestehenden Join-Entities
- `PersonengruppeAgent.IstErmittlungsleiter` (bool, default `false`)
- `ParteiAgent.IstErmittlungsleiter` (bool, default `false`)

Mehrere `true`-Zeilen je Akte sind erlaubt (= mehrere Ermittlungsleiter). Das Flag-Muster ist analog
zu `FraktionMitglied.IstLeitung` und vermeidet ein Einzel-FK-Feld an der Akte, das mit „mehrere EL"
nicht funktionieren würde.

### 3.3 `Fraktion`-Entität
Neue Navigations-Liste analog zu Partei/Gruppe:
```csharp
public List<FraktionAgent> Agenten { get; set; } = new();
```

### 3.4 `AppDbContext`
- `DbSet<FraktionAgent> FraktionAgenten => Set<FraktionAgent>();`
- In der `Fraktion`-Konfiguration: `b.HasMany(f => f.Agenten).WithOne(a => a.Fraktion!).HasForeignKey(a => a.FraktionId).OnDelete(DeleteBehavior.Cascade);`
- Eigener `FraktionAgent`-Block (Spiegel von `ParteiAgent`): FK auf `Agent` mit `Restrict`,
  Unique-Index `(FraktionId, AgentId)`, Index `AgentId`.

### 3.5 Migration
Neue Migration `Phase4_Ermittlungsleiter`:
- Tabelle `FraktionAgenten`.
- Spalte `IstErmittlungsleiter` (bool, default `false`) auf `PersonengruppeAgenten`, `ParteiAgenten`,
  `FraktionAgenten`.

Wird direkt auf die Dev-DB (XAMPP) angewendet (`dotnet ef database update`).

## 4. Services & Rechte

Betrifft `FraktionService`, `PersonengruppeService`, `ParteiService` (+ deren Interfaces). Die Guards
werden in der Service-Schicht serverseitig erzwungen (UI versteckt nur).

### 4.1 Ersteller → automatisch Ermittlungsleiter
In `ErstellenAsync` (alle drei) nach dem Speichern der Akte eine Agent-Zuteilung anlegen:
```csharp
var agentId = handelnder.GetAgentId();
if (agentId is not null)
{
    db.<Typ>Agenten.Add(new <Typ>Agent
    {
        <Akte>Id = akte.Id,
        AgentId = agentId,
        IstErmittlungsleiter = true,
    });
    await db.SaveChangesAsync(ct);
}
```
Bei Fraktion ist das die erste Agent-Zuteilung überhaupt; bei Gruppe/Partei ergänzt es die bestehende
`ErstellenAsync`. Innerhalb der bereits vorhandenen Transaktion.

### 4.2 Guard-Helfer „Führung oder EL dieser Akte"
Neuer privater Helfer je Service (oder gemeinsamer Helfer), der prüft, ob der Handelnde Führung ist
**oder** als Ermittlungsleiter dieser Akte eingetragen ist:
```csharp
private static async Task VerlangeFuehrungOderELAsync(AppDbContext db, string akteId,
    ClaimsPrincipal handelnder, CancellationToken ct)
{
    if (handelnder.IstFuehrung()) return;
    var agentId = handelnder.GetAgentId();
    var istEL = agentId is not null && await db.<Typ>Agenten
        .AnyAsync(a => a.<Akte>Id == akteId && a.AgentId == agentId && a.IstErmittlungsleiter, ct);
    if (!istEL)
        throw new UnauthorizedAccessException(
            "Agents zuteilen/entfernen dürfen nur die Führung oder ein Ermittlungsleiter dieser Akte.");
}
```

### 4.3 `AgentZuteilenAsync`
Signatur erweitert um `bool alsErmittlungsleiter`:
- Auth: `VerlangeFuehrungOderELAsync` (Führung oder EL).
- Verschlusssachen-Prüfung bleibt.
- Wenn `alsErmittlungsleiter == true` → zusätzlich `Berechtigung.VerlangeFuehrung(handelnder)`.
- Bestehende Existenz-/Duplikatsprüfungen bleiben; neue Zeile mit gesetztem Flag.

### 4.4 `AgentEntfernenAsync`
- Auth: `VerlangeFuehrungOderELAsync` (Führung oder EL). Verschlusssachen-Prüfung bleibt.

### 4.5 `ErmittlungsleiterSetzenAsync(zuteilungId, bool ist, handelnder)` — neu
- Auth: `Berechtigung.VerlangeFuehrung(handelnder)` (**nur Führung**). Verschlusssachen-Prüfung bleibt.
- Setzt `IstErmittlungsleiter` auf der bestehenden Zuteilung; auditiert automatisch via Interceptor.

### 4.6 Historie
`FraktionService.GetHistorieAsync` nimmt `FraktionAgent` in die Audit-Typen auf und sammelt die
Zuteilungs-IDs (Spiegel der Gruppen-/Partei-Implementierung). Bei Gruppe/Partei ist `*Agent` bereits
enthalten — Flag-Änderungen erscheinen dort automatisch.

## 5. UI

### 5.1 Fraktion: neuer Tab „Zugeteilte Agents"
- Neue Komponente `FraktionAgentePanel` (Spiegel von `ParteiAgentePanel`; eigener Name wegen
  Tag-Auflösung, da Shared-Ordner global `@using` sind).
- In `FraktionDetail.razor` als neuer `MudTabPanel` einfügen und das `_tabs`-Array synchron halten
  (Reihenfolge: `stammdaten, mitglieder, agents, bestaende, einstufung, doks, beziehungen, quellen,
  kommentare, historie`).

### 5.2 Agent-Panels (alle drei)
- „Agent zuteilen"-Button nur sichtbar, wenn `User.IstFuehrung()` **oder** der eingeloggte Agent in
  `_agenten` als EL geführt ist (`_kannVerwalten`).
- Jede Zeile zeigt bei EL einen **„Ermittlungsleiter"-Chip**.
- Für Führung je Zeile ein Umschalter „Als Ermittlungsleiter markieren" / „Markierung entfernen"
  (→ `ErmittlungsleiterSetzenAsync`).
- Entfernen-Button nur sichtbar bei `_kannVerwalten`.
- Nach jeder Änderung `Changed`-Callback an die Detailseite, damit die Steckkarte aktuell bleibt.

### 5.3 `AgentZuteilenDialog` (gemeinsam)
- Neuer Parameter `DarfErmittlungsleiterSetzen` (= `User.IstFuehrung()`).
- Optionale Checkbox „Als Ermittlungsleiter zuteilen" — nur sichtbar, wenn `DarfErmittlungsleiterSetzen`.
- `ZeigenAsync` liefert künftig `(string AgentId, bool AlsErmittlungsleiter)` statt nur `AgentId`
  (alle drei Aufrufer anpassen).

### 5.4 Steckkarten (`Gruppenkarte`, `Fraktionskarte`, `Parteikarte`)
- Neuer Parameter `IReadOnlyList<string> Ermittlungsleiter` (Codenames — entkoppelt die Karte von den
  Entitätstypen).
- Neuer Abschnitt **„Ermittlungsleiter"** oben unter dem Namen, mit Abzeichen-Icon (z. B.
  `Icons.Material.Filled.LocalPolice` oder `Star`). Mehrere Namen als Liste/Chips; bei keinem EL
  dezenter Platzhalter („—").

### 5.5 Detailseiten (3)
- Laden die EL-Codenames (neue Service-Methode `GetErmittlungsleiterAsync(akteId)` → nur geflaggte
  Zuteilungen inkl. `Agent`) und geben sie an die Karte.
- Reagieren auf den `Changed`-Callback des Panels und laden die EL-Liste neu.

## 6. Randfälle

- **Bestehende Akten ohne EL** (vor diesem Feature angelegt): bis ein EL gesetzt ist, kann nur die
  Führung Agents verwalten und EL vergeben. Akzeptiert; in der Praxis selten, da neue Akten den
  Ersteller automatisch als EL führen.
- **Letzten EL entfernen**: möglich (durch Führung/EL); danach Führung-only bis zur Neuvergabe.
- **Verschlusssache + nicht-Führungs-EL**: Der bestehende VS-Guard wirft weiterhin für Nicht-Führung →
  ein Nicht-Führungs-EL kann an einer VS-Akte keine Agents verwalten. Sicher und gewollt.
- **Duplikat-Zuteilung**: durch Unique-Index + bestehende Prüfung verhindert.

## 7. Betroffene/Neue Dateien (Überblick)

**Neu**
- `Data/Entities/Fraktionen/FraktionAgent.cs`
- `Components/Pages/Fraktionen/Shared/FraktionAgentePanel.razor`
- Migration `Phase4_Ermittlungsleiter`

**Geändert**
- `Data/Entities/Fraktionen/Fraktion.cs` (+`Agenten`)
- `Data/Entities/Gruppen/PersonengruppeAgent.cs`, `Data/Entities/Parteien/ParteiAgent.cs` (+Flag)
- `Data/AppDbContext.cs` (DbSet + Konfiguration)
- `Services/IFraktionService.cs` + `FraktionService.cs` (Agent-Methoden + EL + Ersteller-EL + Historie)
- `Services/IPersonengruppeService.cs` + `PersonengruppeService.cs` (EL + Ersteller-EL + Guards)
- `Services/IParteiService.cs` + `ParteiService.cs` (EL + Ersteller-EL + Guards)
- `Components/Pages/Fraktionen/FraktionDetail.razor` (Tab)
- `Components/Pages/Gruppen/Shared/AgentePanel.razor`, `Components/Pages/Parteien/Shared/ParteiAgentePanel.razor`
- `Components/Pages/Gruppen/Shared/AgentZuteilenDialog.razor` (Checkbox + Rückgabe)
- `Components/Pages/Gruppen/Shared/Gruppenkarte.razor`, `Components/Pages/Fraktionen/Shared/Fraktionskarte.razor`, `Components/Pages/Parteien/Shared/Parteikarte.razor`
- `Components/Pages/Gruppen/GruppeDetail.razor`, `Components/Pages/Fraktionen/FraktionDetail.razor`, `Components/Pages/Parteien/ParteiDetail.razor`

## 8. Abnahmekriterien

1. Bei einer **Fraktion** lassen sich Agents zuteilen (neuer Tab) — wie bei Gruppe/Partei.
2. Beim **Anlegen** einer Gruppe/Fraktion/Partei erscheint der Ersteller automatisch als zugeteilter
   Agent **und** ist als Ermittlungsleiter markiert.
3. Die **linke Steckkarte** zeigt den/die Ermittlungsleiter (Codename) bei allen drei Aktentypen.
4. Die **Führung** kann Agents als Ermittlungsleiter markieren/entmarkieren; ein normaler Agent kann
   das nicht (serverseitig verweigert).
5. **Agents zuteilen/entfernen** gelingt nur als Führung oder als Ermittlungsleiter der Akte; ein
   sonstiger Agent wird serverseitig abgewiesen.
6. Mehrere Ermittlungsleiter gleichzeitig sind möglich.
7. Build 0/0, Migration angewendet, bestehende Gruppen-/Partei-Agent-Funktionen unverändert nutzbar.
