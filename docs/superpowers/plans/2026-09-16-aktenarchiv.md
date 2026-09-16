# Aktenarchiv Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein dritter Aktenzustand neben aktiv und Papierkorb — sieben Aktentypen lassen sich von Hand archivieren; archivierte Akten fallen aus Listen, Auswahllisten, Standardsuche und allen Auswertungen heraus, bleiben aber über Detailseite, Verknüpfungen, Erwähnungen und Graph vollständig lesbar.

**Architecture:** Marker-Interface `IArchivable` + zentraler statischer Helfer `RecordArchive` (Muster von `AgentSelection`/`RecordVisibility`), **kein** globaler Query-Filter. Der Filter wird explizit an den Stellen angewandt, die einen Bestand aufzählen; die Menge dieser Stellen ist in diesem Plan vollständig aufgeführt. Geschrieben wird über `ExecuteUpdateAsync` plus `ManualAudit`, damit `GeaendertAm` unberührt bleibt.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server), MudBlazor 9.5, EF Core 9 + Pomelo 9 (MySQL/MariaDB), xunit + In-Memory-SQLite, NSubstitute.

**Spec:** [`docs/superpowers/specs/2026-09-16-aktenarchiv-design.md`](../specs/2026-09-16-aktenarchiv-design.md)

## Global Constraints

- **Kommentare im Code: nur Englisch.** Inline-`//` sind 2–3 Wörter und beschreiben das *Warum*. XML-`/// <summary>` ist ein kurzer englischer Satz. Keine Block-Kommentare, keine „Phase X"-Verweise.
- **Domänen-Vokabular, UI-Texte, Migrationsnamen und dieser Plan: Deutsch.** DB-Spalten Deutsch, C#-Member Englisch.
- **Umlaute** in allen UI-Texten korrekt (ü/ä/ö/ß); ASCII nur für Bezeichner und Schlüssel.
- **Nie `.Where(x => !x.IsArchived)` von Hand** — immer über `RecordArchive`. Gleiche Regel wie bei `AgentSelection`.
- **`Permission.RequireWriteAccess(actor)` ist die erste Anweisung** jedes Schreibpfads, weil `ExecuteUpdateAsync` den `ReadOnlyBarrierInterceptor` umgeht.
- **Kein bUnit** — `.razor`-Dateien sind nicht testbar. Testbare Logik gehört in den Service-Layer; UI-Aufgaben werden per Build und manuell abgenommen.
- **Vor `dotnet ef migrations add` den Dev-Server stoppen** (bin-Lock). EF-Befehle laufen aus `scripts/`, davor `dotnet tool restore`.
- **EF/Identity nicht auf 10.x heben** (Pomelo-9-Kollision).
- **Testkommandos sind Teil dieses Plans und ausdrücklich freigegeben** — der Repo-Eigentümer will sonst vor Builds gefragt werden; für die hier aufgeführten `dotnet test`/`dotnet build`-Schritte gilt das nicht.
- Testlauf-Kommando (ganze Suite): `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj`
- Einzelne Klasse: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~KlassenName"`

## Die sieben Aktentypen

Diese Tabelle gilt für den ganzen Plan. Wo eine Aufgabe „für jeden der sieben Typen" sagt, sind genau diese Dateien gemeint.

| Typ | Entität | DbSet | Dienst / Interface | Liste | Detailseite | `Visible()` im Suchanbieter |
|---|---|---|---|---|---|---|
| `Person` | `Data/Entities/People/Person.cs` | `db.People` | `PersonService` / `IPersonService` | `Pages/People/PeopleList.razor` | `Pages/People/PersonDetail.razor` | `RecordSearchProviders.cs:77` |
| `Faction` | `Data/Entities/Factions/Faction.cs` | `db.Factions` | `FactionService` / `IFactionService` | `Pages/Factions/FactionsList.razor` | `Pages/Factions/FactionDetail.razor` | `RecordSearchProviders.cs:188` |
| `PersonGroup` | `Data/Entities/Groups/PersonGroup.cs` | `db.PersonGroups` | `PersonGroupService` / `IPersonGroupService` | `Pages/Groups/GroupsList.razor` | `Pages/Groups/GroupDetail.razor` | `RecordSearchProviders.cs:273` |
| `Party` | `Data/Entities/Parties/Party.cs` | `db.Parties` | `PartyService` / `IPartyService` | `Pages/Parties/PartiesList.razor` | `Pages/Parties/PartyDetail.razor` | `RecordSearchProviders.cs:354` |
| `Operation` | `Data/Entities/Operations/Operation.cs` | `db.Operations` | `OperationService` / `IOperationService` | `Pages/Operations/OperationsList.razor` | `Pages/Operations/OperationDetail.razor` | `RecordSearchProviders.cs:439` |
| `Case` | `Data/Entities/Cases/Case.cs` | `db.Cases` | `CaseService` / `ICaseService` | `Pages/Cases/CasesList.razor` | `Pages/Cases/CaseDetail.razor` | `RecordSearchProviders.cs:523` |
| `Taskforce` | `Data/Entities/Taskforces/Taskforce.cs` | `db.Taskforces` | `TaskforceService` / `ITaskforceService` | `Pages/Taskforces/TaskforceList.razor` | `Pages/Taskforces/TaskforceDetail.razor` | `OperationsSearchProviders.cs:75` |

Alle Razor-Pfade relativ zu `NOOSE-Website/Components/`, alle übrigen relativ zu `NOOSE-Website/`.

## Dateiübersicht

**Neu:**
- `NOOSE-Website/Models/Abstractions/IArchivable.cs` — Marker-Interface, vier Eigenschaften.
- `NOOSE-Website/Models/Enums/ArchiveFilter.cs` — `Active` / `Including` / `Only`.
- `NOOSE-Website/Services/RecordArchive.cs` — Lesefilter (`OnlyActive`/`OnlyArchived`/`Apply`) **und** der generische Schreibvorgang.
- `NOOSE-Website/Data/Migrations/*_Phase81_Archiv.cs` — vier Spalten + Index auf sieben Tabellen.
- Testdateien (je Aufgabe genannt) unter `NOOSE-Website.Tests/Services/Integration/`.

**Geändert:** die sieben Entitäten, die sieben Dienste samt Interfaces, `AuditAction` + `AuditActionDisplay` + `TimelineDisplay`, `SearchCriteria` + `SearchQuery` + sieben `Visible()`-Helfer + `SearchPage.razor`, `DashboardService`, vier Statistikdienste, `ThreatScoreService`, `RecencyService`-Konsumenten, `GraphService` + `graph.js`, die sieben Listen und sieben Detailseiten, `RecordsChapter.cs` + `GlossaryContent.cs` + `ChangelogContent.cs`.

---

### Task 1: Marker-Interface, Enum, Entitäten und Migration

**Files:**
- Create: `NOOSE-Website/Models/Abstractions/IArchivable.cs`
- Create: `NOOSE-Website/Models/Enums/ArchiveFilter.cs`
- Modify: die sieben Entitäten aus der Typ-Tabelle
- Create: `NOOSE-Website/Data/Migrations/<timestamp>_Phase81_Archiv.cs` (von `dotnet ef` erzeugt)
- Modify: `NOOSE-Website/Data/AppDbContext.cs` (sieben `HasIndex`-Zeilen)
- Test: `NOOSE-Website.Tests/Services/Integration/ArchiveModelTests.cs`

**Interfaces:**
- Consumes: nichts.
- Produces: `IArchivable` mit `bool IsArchived`, `DateTime? ArchivedAt`, `string? ArchivedById`, `string? ArchiveReason`. `ArchiveFilter` mit `Active = 0`, `Including = 1`, `Only = 2`. Alle sieben Entitäten implementieren `IArchivable`.

- [ ] **Step 1: Write the failing test**

`NOOSE-Website.Tests/Services/Integration/ArchiveModelTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The archive columns exist on every archivable type and round-trip.</summary>
public sealed class ArchiveModelTests
{
    [Theory]
    [InlineData(typeof(Person))]
    [InlineData(typeof(Faction))]
    [InlineData(typeof(PersonGroup))]
    [InlineData(typeof(Party))]
    [InlineData(typeof(Operation))]
    [InlineData(typeof(Case))]
    [InlineData(typeof(Taskforce))]
    public void Every_archivable_record_type_implements_the_marker(Type clrType)
        => Assert.True(typeof(IArchivable).IsAssignableFrom(clrType), $"{clrType.Name} muss IArchivable implementieren.");

    [Fact]
    public async Task Archive_columns_round_trip()
    {
        using var ctx = new SqliteTestContext();
        var stamp = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1", "Archivar", p =>
            {
                p.IsArchived = true;
                p.ArchivedAt = stamp;
                p.ArchivedById = "agent-1";
                p.ArchiveReason = "Dauerhaft tot";
            }));
            await db.SaveChangesAsync();
        }
        using (var db = ctx.NewContext())
        {
            var person = await db.People.SingleAsync(p => p.Id == "p1");
            Assert.True(person.IsArchived);
            Assert.Equal(stamp, person.ArchivedAt);
            Assert.Equal("agent-1", person.ArchivedById);
            Assert.Equal("Dauerhaft tot", person.ArchiveReason);
        }
    }

    [Fact]
    public async Task A_new_record_is_not_archived()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Factions.Add(Seed.Faction("f1"));
            await db.SaveChangesAsync();
        }
        using (var db = ctx.NewContext())
        {
            Assert.False((await db.Factions.SingleAsync(f => f.Id == "f1")).IsArchived);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveModelTests"`
Expected: Compile error — `IArchivable` existiert nicht, `p.IsArchived` existiert nicht.

- [ ] **Step 3: Marker-Interface anlegen**

`NOOSE-Website/Models/Abstractions/IArchivable.cs`:

```csharp
namespace NOOSE_Website.Models.Abstractions;

/// <summary>Marks a record as archivable; filtered out of stock listings, still readable everywhere else.</summary>
public interface IArchivable
{
    bool IsArchived { get; set; }
    DateTime? ArchivedAt { get; set; }
    string? ArchivedById { get; set; }
    string? ArchiveReason { get; set; }
}
```

- [ ] **Step 4: Enum anlegen**

`NOOSE-Website/Models/Enums/ArchiveFilter.cs`:

```csharp
namespace NOOSE_Website.Models.Enums;

/// <summary>Which part of the stock a listing asks for.</summary>
public enum ArchiveFilter
{
    /// <summary>Active stock only. The default everywhere.</summary>
    Active = 0,
    /// <summary>Active and archived together.</summary>
    Including = 1,
    /// <summary>Archived only.</summary>
    Only = 2,
}
```

- [ ] **Step 5: Die sieben Entitäten erweitern**

In **jeder** der sieben Entitäten aus der Typ-Tabelle: `IArchivable` an die Basisliste anfügen und diesen Block direkt unter die vorhandenen Soft-Delete-Spalten setzen (bei `Person` also unter `DeletedById`, `Person.cs:113`). Keine Navigation, kein FK — genau wie `DeletedById`, das ebenfalls ein nacktes `string?` ist.

```csharp
    [Column("IstArchiviert")]
    public bool IsArchived { get; set; }
    [Column("ArchiviertAm")]
    public DateTime? ArchivedAt { get; set; }
    [Column("ArchiviertVonId")]
    public string? ArchivedById { get; set; }
    /// <summary>Optional note shown on the banner and in the audit row.</summary>
    [Column("Archivgrund")]
    public string? ArchiveReason { get; set; }
```

Klassendeklaration, Beispiel `Person.cs:9`:

```csharp
public class Person : IAuditable, ISoftDelete, IClassifiableRecord, IArchivable
```

Bei `Taskforce` und `Case` die dort vorhandene Interface-Liste entsprechend ergänzen; `IArchivable` kommt immer als letztes.

- [ ] **Step 6: Indizes in `AppDbContext.OnModelCreating`**

Für jeden der sieben Typen eine Zeile in den jeweils schon vorhandenen `modelBuilder.Entity<T>(b => { … })`-Block:

```csharp
            // the stock listings ask for this on every page load
            b.HasIndex(e => e.IsArchived);
```

- [ ] **Step 7: Migration erzeugen**

Dev-Server vorher stoppen (bin-Lock), dann:

```bash
cd scripts && dotnet tool restore && dotnet ef migrations add Phase81_Archiv --project ../NOOSE-Website/NOOSE-Website.csproj --startup-project ../NOOSE-Website/NOOSE-Website.csproj && cd ..
```

Prüfen, dass die erzeugte Migration genau 28 `AddColumn` (7 Tabellen × 4) und 7 `CreateIndex` enthält und **keine** anderen Änderungen mitschleppt. Falls doch, ist ein fremder Modellstand im Snapshot — dann stoppen und nachfragen.

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveModelTests"`
Expected: PASS (3 Fälle + 7 Theory-Zeilen).

- [ ] **Step 9: Commit**

```bash
git add NOOSE-Website/Models/Abstractions/IArchivable.cs NOOSE-Website/Models/Enums/ArchiveFilter.cs NOOSE-Website/Data NOOSE-Website.Tests/Services/Integration/ArchiveModelTests.cs
git commit -m "Add archive columns to the seven record types"
```

---

### Task 2: Zentraler Helfer `RecordArchive`

**Files:**
- Create: `NOOSE-Website/Services/RecordArchive.cs`
- Test: `NOOSE-Website.Tests/Services/Integration/RecordArchiveTests.cs`

**Interfaces:**
- Consumes: `IArchivable`, `ArchiveFilter` (Task 1).
- Produces:
  - `IQueryable<T> OnlyActive<T>(this IQueryable<T>) where T : class, IArchivable`
  - `IQueryable<T> OnlyArchived<T>(this IQueryable<T>)`
  - `IQueryable<T> Apply<T>(this IQueryable<T>, ArchiveFilter)`
  - `Task<bool> SetArchivedAsync<T>(AppDbContext db, string id, bool archived, string? reason, ClaimsPrincipal actor, CancellationToken ct) where T : class, IArchivable` — schreibt per `ExecuteUpdateAsync`, gibt `true` zurück, wenn sich eine Zeile geändert hat.

- [ ] **Step 1: Write the failing test**

`NOOSE-Website.Tests/Services/Integration/RecordArchiveTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The one place that decides what "active stock" means.</summary>
public sealed class RecordArchiveTests
{
    private static async Task<SqliteTestContext> TwoPeopleAsync()
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        db.Users.Add(Seed.Agent("agent-1"));
        db.People.Add(Seed.Person("aktiv", "Aktiv"));
        db.People.Add(Seed.Person("archiv", "Archiviert", p => p.IsArchived = true));
        await db.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task OnlyActive_drops_archived_rows()
    {
        using var ctx = await TwoPeopleAsync();
        await using var db = ctx.NewContext();
        var ids = await db.People.OnlyActive().Select(p => p.Id).ToListAsync();
        Assert.Equal(["aktiv"], ids);
    }

    [Fact]
    public async Task OnlyArchived_keeps_only_archived_rows()
    {
        using var ctx = await TwoPeopleAsync();
        await using var db = ctx.NewContext();
        var ids = await db.People.OnlyArchived().Select(p => p.Id).ToListAsync();
        Assert.Equal(["archiv"], ids);
    }

    [Theory]
    [InlineData(ArchiveFilter.Active, 1)]
    [InlineData(ArchiveFilter.Including, 2)]
    [InlineData(ArchiveFilter.Only, 1)]
    public async Task Apply_selects_the_requested_part_of_the_stock(ArchiveFilter filter, int expected)
    {
        using var ctx = await TwoPeopleAsync();
        await using var db = ctx.NewContext();
        Assert.Equal(expected, await db.People.Apply(filter).CountAsync());
    }

    [Fact]
    public async Task SetArchivedAsync_writes_the_four_columns()
    {
        using var ctx = await TwoPeopleAsync();
        var actor = ClaimsPrincipalBuilder.Agent("agent-1").WithRank(Rank.JuniorAgent).Build();
        await using (var db = ctx.NewContext())
        {
            Assert.True(await RecordArchive.SetArchivedAsync<Person>(db, "aktiv", true, "  Aufgelöst  ", actor, default));
        }
        await using (var db = ctx.NewContext())
        {
            var person = await db.People.SingleAsync(p => p.Id == "aktiv");
            Assert.True(person.IsArchived);
            Assert.NotNull(person.ArchivedAt);
            Assert.Equal("agent-1", person.ArchivedById);
            Assert.Equal("Aufgelöst", person.ArchiveReason);
        }
    }

    [Fact]
    public async Task SetArchivedAsync_clears_the_columns_on_the_way_back()
    {
        using var ctx = await TwoPeopleAsync();
        var actor = ClaimsPrincipalBuilder.Agent("agent-1").WithRank(Rank.JuniorAgent).Build();
        await using (var db = ctx.NewContext())
        {
            Assert.True(await RecordArchive.SetArchivedAsync<Person>(db, "archiv", false, null, actor, default));
        }
        await using (var db = ctx.NewContext())
        {
            var person = await db.People.SingleAsync(p => p.Id == "archiv");
            Assert.False(person.IsArchived);
            Assert.Null(person.ArchivedAt);
            Assert.Null(person.ArchivedById);
            Assert.Null(person.ArchiveReason);
        }
    }

    [Fact]
    public async Task SetArchivedAsync_reports_no_change_when_the_state_already_matches()
    {
        using var ctx = await TwoPeopleAsync();
        var actor = ClaimsPrincipalBuilder.Agent("agent-1").WithRank(Rank.JuniorAgent).Build();
        await using var db = ctx.NewContext();
        Assert.False(await RecordArchive.SetArchivedAsync<Person>(db, "archiv", true, null, actor, default));
    }
}
```

`Rank` kommt aus `NOOSE_Website.Models.Enums` — das Using steht schon in der Datei.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~RecordArchiveTests"`
Expected: Compile error — `RecordArchive` existiert nicht.

- [ ] **Step 3: Helfer schreiben**

`NOOSE-Website/Services/RecordArchive.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>The one rule for what counts as active stock, and the one way to move a record in or out of the archive.</summary>
/// <remarks>
/// Deliberately not a global query filter like <see cref="ISoftDelete"/>. Archiving is soft: the detail page, the
/// link panel, mention resolution and the graph must keep showing the record, so the filter is applied where a
/// listing enumerates the stock and nowhere else. Never hand-roll the predicate — same rule as AgentSelection.
/// </remarks>
public static class RecordArchive
{
    /// <summary>Active stock: everything not filed away.</summary>
    public static IQueryable<T> OnlyActive<T>(this IQueryable<T> query) where T : class, IArchivable
        => query.Where(x => !x.IsArchived);

    /// <summary>The archive itself.</summary>
    public static IQueryable<T> OnlyArchived<T>(this IQueryable<T> query) where T : class, IArchivable
        => query.Where(x => x.IsArchived);

    /// <summary>The part of the stock a listing asked for.</summary>
    public static IQueryable<T> Apply<T>(this IQueryable<T> query, ArchiveFilter filter) where T : class, IArchivable
        => filter switch
        {
            ArchiveFilter.Active => query.OnlyActive(),
            ArchiveFilter.Only => query.OnlyArchived(),
            _ => query,
        };

    /// <summary>In-memory twin for rows already materialised.</summary>
    public static bool IsActive(IArchivable record) => !record.IsArchived;

    /// <summary>Moves one record in or out of the archive; false when it already was in that state.</summary>
    /// <remarks>
    /// ExecuteUpdate on purpose: a tracked save would stamp GeaendertAm through the audit interceptor, and a
    /// record coming back out of the archive would then read as freshly edited — which the recency traffic light
    /// believes. Callers own the write guard and the audit row, because this bypasses both interceptors.
    /// </remarks>
    public static async Task<bool> SetArchivedAsync<T>(
        AppDbContext db, string id, bool archived, string? reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
        where T : class, IArchivable
    {
        // locals so EF parameterizes instead of baking the values into the SQL
        var stamp = archived ? DateTime.UtcNow : (DateTime?)null;
        var meId = archived ? actor.GetAgentId() : null;
        var note = archived && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null;

        // IArchivable carries no key, so the id goes through EF's property accessor
        var changed = await db.Set<T>()
            .Where(x => EF.Property<string>(x, "Id") == id && x.IsArchived != archived)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsArchived, archived)
                .SetProperty(x => x.ArchivedAt, stamp)
                .SetProperty(x => x.ArchivedById, meId)
                .SetProperty(x => x.ArchiveReason, note), cancellationToken);
        return changed > 0;
    }
}
```

Alle sieben Typen haben `public string Id { get; set; }`, also trifft `EF.Property<string>(x, "Id")` überall den Primärschlüssel.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~RecordArchiveTests"`
Expected: PASS (7 Fälle inkl. Theory).

**Wenn `SetArchivedAsync` mit einer Übersetzungs-Ausnahme scheitert** (EF kann die Interface-Eigenschaft im `SetProperty`-Baum nicht auflösen): den generischen Schreibvorgang ersatzlos aus `RecordArchive` entfernen, die beiden `SetArchivedAsync`-Tests löschen und in Task 4 je Dienst diesen typisierten Block schreiben (Beispiel `PersonService`):

```csharp
        var changed = await db.People.Where(p => p.Id == id && p.IsArchived != archived)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsArchived, archived)
                .SetProperty(p => p.ArchivedAt, stamp)
                .SetProperty(p => p.ArchivedById, meId)
                .SetProperty(p => p.ArchiveReason, note), cancellationToken);
```

- [ ] **Step 5: MySQL-Übersetzung absichern**

`NOOSE-Website.Tests/Infrastructure/MySqlTranslationTests.cs` prüft, dass Abfragen auf Pomelo übersetzen. Dort einen Fall nach dem Muster der vorhandenen Einträge ergänzen, der `db.People.OnlyActive().Take(5)` übersetzt, damit die Extension nicht erst in Produktion auffällt.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj`
Expected: PASS — nichts darf durch die neue Extension brechen.

- [ ] **Step 7: Commit**

```bash
git add NOOSE-Website/Services/RecordArchive.cs NOOSE-Website.Tests
git commit -m "Add the central archive predicate and write path"
```

---

### Task 3: Zwei neue Audit-Aktionen

**Files:**
- Modify: `NOOSE-Website/Models/Enums/AuditAction.cs`
- Modify: `NOOSE-Website/Services/AuditActionDisplay.cs`
- Modify: `NOOSE-Website/Services/TimelineDisplay.cs:26-35`
- Test: `NOOSE-Website.Tests/Services/Integration/ArchiveAuditLabelTests.cs`

**Interfaces:**
- Consumes: nichts.
- Produces: `AuditAction.Archived = 4`, `AuditAction.Unarchived = 5`; `AuditActionDisplay.Name` liefert „Archiviert" bzw. „Aus dem Archiv geholt"; `TimelineDisplay.MapAudit(nameof(Person), AuditAction.Archived)` liefert einen Titel, der nicht „geändert" enthält.

- [ ] **Step 1: Write the failing test**

`NOOSE-Website.Tests/Services/Integration/ArchiveAuditLabelTests.cs`:

```csharp
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The archive actions carry their own label, or the log reads as an ordinary edit.</summary>
public sealed class ArchiveAuditLabelTests
{
    [Fact]
    public void Archive_actions_have_german_labels()
    {
        Assert.Equal("Archiviert", AuditActionDisplay.Name(AuditAction.Archived));
        Assert.Equal("Aus dem Archiv geholt", AuditActionDisplay.Name(AuditAction.Unarchived));
    }

    [Fact]
    public void Archive_actions_are_offered_in_the_log_filter()
    {
        Assert.Contains(AuditAction.Archived, AuditActionDisplay.All);
        Assert.Contains(AuditAction.Unarchived, AuditActionDisplay.All);
    }

    [Fact]
    public void Timeline_titles_say_archive_not_changed()
    {
        var archived = TimelineDisplay.MapAudit(nameof(Person), AuditAction.Archived);
        var back = TimelineDisplay.MapAudit(nameof(Person), AuditAction.Unarchived);
        Assert.Contains("archiviert", archived.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Archiv", back.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("geändert", archived.Title, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveAuditLabelTests"`
Expected: Compile error — `AuditAction.Archived` existiert nicht.

- [ ] **Step 3: Enum erweitern**

`NOOSE-Website/Models/Enums/AuditAction.cs`:

```csharp
    Created = 0,
    Modified = 1,
    Deleted = 2,
    Restored = 3,
    /// <summary>Filed away: out of listings, still readable.</summary>
    Archived = 4,
    /// <summary>Brought back into the active stock.</summary>
    Unarchived = 5,
```

- [ ] **Step 4: Anzeige erweitern**

`NOOSE-Website/Services/AuditActionDisplay.cs` — drei Stellen:

```csharp
    public static readonly AuditAction[] All =
    [
        AuditAction.Created, AuditAction.Modified, AuditAction.Deleted, AuditAction.Restored,
        AuditAction.Archived, AuditAction.Unarchived,
    ];
```

```csharp
        AuditAction.Archived => "Archiviert",
        AuditAction.Unarchived => "Aus dem Archiv geholt",
```

(in `Name`, vor dem `_ =>`-Zweig) und in `Colour`:

```csharp
        AuditAction.Archived => Color.Default,
        AuditAction.Unarchived => Color.Default,
```

- [ ] **Step 5: Zeitstrahl erweitern**

`NOOSE-Website/Services/TimelineDisplay.cs`, in der lokalen `Verb`-Funktion (Zeile 29–35), vor dem `_ =>`-Zweig:

```csharp
            AuditAction.Archived => "archiviert",
            AuditAction.Unarchived => "aus dem Archiv geholt",
```

- [ ] **Step 6: Die acht Verlaufs-Komponenten prüfen**

`Pages/People/Shared/HistoryTimeline.razor`, `Factions/Shared/FactionHistoryTimeline.razor`, `Groups/Shared/GroupHistoryTimeline.razor`, `Jobs/Shared/JobHistoryTimeline.razor`, `Cases/Shared/CaseHistoryTimeline.razor`, `Parties/Shared/PartyHistoryTimeline.razor`, `Operations/Shared/OperationHistoryTimeline.razor`, `Taskforces/Shared/TaskforceHistoryTimeline.razor`.

In jeder nach `AuditAction.` suchen. Wo über die Aktion verzweigt wird (Icon/Farbe), die beiden neuen Werte mit aufnehmen — `Archived` wie `Deleted` einordnen, aber in `Color.Default`, `Unarchived` wie `Restored`. Wo nur `AuditActionDisplay` benutzt wird, ist nichts zu tun.

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveAuditLabelTests"`
Expected: PASS (3 Fälle).

- [ ] **Step 8: Commit**

```bash
git add NOOSE-Website/Models/Enums/AuditAction.cs NOOSE-Website/Services NOOSE-Website/Components NOOSE-Website.Tests
git commit -m "Give archiving its own audit action"
```

---

### Task 4: Schreibweg in den sieben Diensten

**Files:**
- Modify: die sieben Interfaces und die sieben Implementierungen aus der Typ-Tabelle
- Test: `NOOSE-Website.Tests/Services/Integration/ArchiveWritePathTests.cs`

**Interfaces:**
- Consumes: `RecordArchive.SetArchivedAsync<T>` (Task 2), `AuditAction.Archived`/`Unarchived` (Task 3).
- Produces: auf **jedem** der sieben Interfaces
  - `Task ArchiveAsync(string id, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default)`
  - `Task UnarchiveAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Write the failing test**

`NOOSE-Website.Tests/Services/Integration/ArchiveWritePathTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Archiving is a write, is audited, and must not look like an edit.</summary>
public sealed class ArchiveWritePathTests
{
    private static PersonService Build(SqliteTestContext ctx)
        => new(ctx.Factory,
            Substitute.For<IFileStorageService>(),
            Substitute.For<IProfileSuggestionService>(),
            Substitute.For<ICaseNumberService>(),
            Substitute.For<IThreatScoreService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<NOOSE_Website.Services.Public.IPublicWantedService>());

    private static ClaimsPrincipal Junior(string id = "junior")
        => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.JuniorAgent).Build();

    private static async Task<SqliteTestContext> OnePersonAsync(DateTime? modifiedAt = null)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        db.Users.Add(Seed.Agent("junior", Rank.JuniorAgent));
        db.People.Add(Seed.Person("p1", "Akte", p => p.ModifiedAt = modifiedAt));
        await db.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task ArchiveAsync_sets_the_flag_and_keeps_the_record_readable()
    {
        using var ctx = await OnePersonAsync();
        await Build(ctx).ArchiveAsync("p1", "Aufgelöst", Junior());

        await using var db = ctx.NewContext();
        var person = await db.People.SingleAsync(p => p.Id == "p1");
        Assert.True(person.IsArchived);
        Assert.Equal("Aufgelöst", person.ArchiveReason);
        Assert.Equal("junior", person.ArchivedById);
    }

    [Fact]
    public async Task ArchiveAsync_leaves_the_edit_stamp_alone()
    {
        var edited = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        using var ctx = await OnePersonAsync(edited);
        await Build(ctx).ArchiveAsync("p1", null, Junior());

        await using var db = ctx.NewContext();
        Assert.Equal(edited, (await db.People.SingleAsync(p => p.Id == "p1")).ModifiedAt);
    }

    [Fact]
    public async Task ArchiveAsync_writes_an_audit_row_against_the_record()
    {
        using var ctx = await OnePersonAsync();
        await Build(ctx).ArchiveAsync("p1", "Aufgelöst", Junior());

        await using var db = ctx.NewContext();
        var row = await db.AuditLogs.SingleAsync(a => a.EntityType == nameof(Person) && a.EntityId == "p1");
        Assert.Equal(AuditAction.Archived, row.Action);
        Assert.Equal("junior", row.AgentId);
        Assert.Contains("Aufgelöst", row.ChangesJson);
    }

    [Fact]
    public async Task UnarchiveAsync_clears_the_flag_and_audits_its_own_action()
    {
        using var ctx = await OnePersonAsync();
        var service = Build(ctx);
        await service.ArchiveAsync("p1", null, Junior());
        await service.UnarchiveAsync("p1", Junior());

        await using var db = ctx.NewContext();
        Assert.False((await db.People.SingleAsync(p => p.Id == "p1")).IsArchived);
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == AuditAction.Unarchived);
    }

    [Fact]
    public async Task Archiving_twice_writes_only_one_audit_row()
    {
        using var ctx = await OnePersonAsync();
        var service = Build(ctx);
        await service.ArchiveAsync("p1", null, Junior());
        await service.ArchiveAsync("p1", null, Junior());

        await using var db = ctx.NewContext();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == AuditAction.Archived));
    }

    [Theory]
    [InlineData("readonly")]
    [InlineData("partner")]
    [InlineData("demo")]
    public async Task Accounts_without_write_access_are_refused(string kind)
    {
        using var ctx = await OnePersonAsync();
        var actor = kind switch
        {
            "readonly" => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).WithTeamLead().Build(),
            "partner" => ClaimsPrincipalBuilder.Agent("partner").WithRank(Rank.JuniorAgent).WithPartnerAgency(PartnerAgency.DoJ).Build(),
            _ => ClaimsPrincipalBuilder.Agent("demo").WithRank(Rank.Director).WithDemo().Build(),
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Build(ctx).ArchiveAsync("p1", null, actor));
    }
}
```

**Vor dem Schreiben prüfen:** `ClaimsPrincipalBuilder` (`NOOSE-Website.Tests/Infrastructure/ClaimsPrincipalBuilder.cs`) öffnen und die dortigen Methodennamen für Nur-Lese-Aufsicht, Partner und Demo einsetzen — `WithTeamLead()`, `WithPartnerAgency(...)` und `WithDemo()` sind die erwarteten Namen, die tatsächlichen stehen in der Datei. Ebenso die Konstruktor-Parameter von `PersonService` gegen `NOOSE-Website/Services/PersonService.cs:19-22` abgleichen.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveWritePathTests"`
Expected: Compile error — `ArchiveAsync` existiert nicht.

- [ ] **Step 3: Die beiden Methoden auf die sieben Interfaces**

In jedes der sieben `I*Service.cs`, direkt unter `RestoreAsync`:

```csharp
    /// <summary>Files the record away: out of listings, pickers and the default search, still fully readable.</summary>
    Task ArchiveAsync(string id, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Brings the record back into the active stock.</summary>
    Task UnarchiveAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implementierung in `PersonService`**

`NOOSE-Website/Services/PersonService.cs`, direkt unter `RestoreAsync` (Zeile 228 ff.):

```csharp
    public async Task ArchiveAsync(string id, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
        => await SetArchivedAsync(id, true, reason, actor, cancellationToken);

    public async Task UnarchiveAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
        => await SetArchivedAsync(id, false, null, actor, cancellationToken);

    private async Task SetArchivedAsync(string id, bool archived, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        // a stock decision, not a rank decision
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await RecordArchive.SetArchivedAsync<Person>(db, id, archived, reason, actor, cancellationToken))
        {
            return;
        }
        // ExecuteUpdate bypasses the audit interceptor, so the row is written by hand
        var note = archived && !string.IsNullOrWhiteSpace(reason)
            ? ManualAudit.Change("Archivgrund", null, reason.Trim())
            : null;
        db.AuditLogs.Add(ManualAudit.Row(nameof(Person), id,
            archived ? AuditAction.Archived : AuditAction.Unarchived, actor, note));
        await db.SaveChangesAsync(cancellationToken);
    }
```

- [ ] **Step 5: Dasselbe in die übrigen sechs Dienste**

Denselben Block in `FactionService`, `PersonGroupService`, `PartyService`, `OperationService`, `CaseService`, `TaskforceService` einsetzen, jeweils direkt unter deren `RestoreAsync`. Zu ersetzen sind genau zwei Dinge: der Typparameter `<Person>` und das `nameof(Person)`. Also:

| Dienst | Typparameter | `nameof(...)` |
|---|---|---|
| `FactionService` | `<Faction>` | `nameof(Faction)` |
| `PersonGroupService` | `<PersonGroup>` | `nameof(PersonGroup)` |
| `PartyService` | `<Party>` | `nameof(Party)` |
| `OperationService` | `<Operation>` | `nameof(Operation)` |
| `CaseService` | `<Case>` | `nameof(Case)` |
| `TaskforceService` | `<Taskforce>` | `nameof(Taskforce)` |

Der Rest — Guard, Rückgabe-Kurzschluss, Audit-Zeile, `SaveChangesAsync` — ist wörtlich identisch. Alle sieben Dienste haben bereits ein `dbFactory`-Feld aus ihrem Primary Constructor.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveWritePathTests"`
Expected: PASS (5 Fälle + 3 Theory-Zeilen).

- [ ] **Step 7: Commit**

```bash
git add NOOSE-Website/Services NOOSE-Website.Tests
git commit -m "Let every record service archive and unarchive"
```

---

### Task 5: Lesepfad der Dienste

**Files:**
- Modify: die sieben Interfaces und Implementierungen (`GetListAsync`, `SearchAsync`)
- Modify: die neun Aufrufstellen von `GetListAsync` (siehe Step 5)
- Test: `NOOSE-Website.Tests/Services/Integration/ArchiveListPathTests.cs`

**Interfaces:**
- Consumes: `RecordArchive.Apply`/`OnlyActive` (Task 2), `ArchiveFilter` (Task 1).
- Produces:
  - sechs Dienste: `GetListAsync(ViewerScope scope, ArchiveFilter filter = ArchiveFilter.Active, CancellationToken cancellationToken = default)`
  - `TaskforceService`: `GetListAsync(bool mayAll, string? meId, ArchiveFilter filter = ArchiveFilter.Active, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null)`
  - alle sieben `SearchAsync`-Überladungen liefern **nur** aktive Akten; ihre Signatur ändert sich nicht.

- [ ] **Step 1: Write the failing test**

`NOOSE-Website.Tests/Services/Integration/ArchiveListPathTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>An archived record leaves the listings and the pickers, and nothing else.</summary>
public sealed class ArchiveListPathTests
{
    private static PersonService Build(SqliteTestContext ctx)
        => new(ctx.Factory,
            Substitute.For<IFileStorageService>(),
            Substitute.For<IProfileSuggestionService>(),
            Substitute.For<ICaseNumberService>(),
            Substitute.For<IThreatScoreService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<NOOSE_Website.Services.Public.IPublicWantedService>());

    private static ViewerScope Leader => new(MayClassifiedRead: true, MayAllTaskforces: true, MeId: "lead",
        PartnerAgency: null, IsLeadership: true);

    private static async Task<SqliteTestContext> StockAsync()
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        db.People.Add(Seed.Person("aktiv", "Aktive Akte"));
        db.People.Add(Seed.Person("archiv", "Archivierte Akte", p => p.IsArchived = true));
        await db.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task GetListAsync_shows_the_active_stock_by_default()
    {
        using var ctx = await StockAsync();
        var rows = await Build(ctx).GetListAsync(Leader);
        Assert.Equal(["aktiv"], rows.Select(p => p.Id));
    }

    [Fact]
    public async Task GetListAsync_can_include_the_archive()
    {
        using var ctx = await StockAsync();
        var rows = await Build(ctx).GetListAsync(Leader, ArchiveFilter.Including);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task GetListAsync_can_show_the_archive_alone()
    {
        using var ctx = await StockAsync();
        var rows = await Build(ctx).GetListAsync(Leader, ArchiveFilter.Only);
        Assert.Equal(["archiv"], rows.Select(p => p.Id));
    }

    [Fact]
    public async Task SearchAsync_never_offers_an_archived_record()
    {
        using var ctx = await StockAsync();
        var hits = await Build(ctx).SearchAsync("Akte", isLeadership: true);
        Assert.Equal(["aktiv"], hits.Select(p => p.Id));
    }

    [Fact]
    public async Task The_detail_page_still_opens_an_archived_record()
    {
        using var ctx = await StockAsync();
        var person = await Build(ctx).GetDetailAsync("archiv", Leader);
        Assert.NotNull(person);
        Assert.True(person!.IsArchived);
    }

    [Fact]
    public async Task A_link_to_an_archived_record_still_resolves()
    {
        using var ctx = await StockAsync();
        await using var db = ctx.NewContext();
        var map = await RecordsReference.ResolveAsync(db, [(nameof(Person), "archiv")]);
        Assert.Equal("Archivierte Akte", map[(nameof(Person), "archiv")].Display);
    }

    [Fact]
    public async Task Archived_and_deleted_are_two_separate_axes()
    {
        using var ctx = await StockAsync();
        var lead = ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();
        var service = Build(ctx);

        await service.DeleteAsync("archiv", lead);
        var trash = await service.GetTrashAsync();
        Assert.Contains(trash, p => p.Id == "archiv");

        await service.RestoreAsync("archiv", lead);
        await using var db = ctx.NewContext();
        var person = await db.People.SingleAsync(p => p.Id == "archiv");
        Assert.False(person.IsDeleted);
        Assert.True(person.IsArchived);
    }
}
```

Für den letzten Fall muss `db.Users` einen Agenten `lead` enthalten — in `StockAsync` `db.Users.Add(Seed.Agent("lead", Rank.Director));` ergänzen. `RecordsReference.ResolveAsync(db, refs, ct, mayAllTaskforces, meId)` hat Standardwerte für die hinteren drei Parameter (`NOOSE-Website/Services/RecordsReference.cs:27`).

`ViewerScope` ist ein `readonly record struct` mit benannten Parametern (`NOOSE-Website/Services/ViewerScope.cs:19-22`) — die Initialisierung oben mit benannten Argumenten ist gültig; die übrigen Felder haben Standardwerte.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveListPathTests"`
Expected: FAIL — `GetListAsync` kennt den zweiten Parameter nicht; `SearchAsync` liefert beide Akten.

- [ ] **Step 3: `GetListAsync` in den sechs ViewerScope-Diensten**

Signatur im Interface **und** in der Implementierung, Beispiel `IPersonService.cs:12` / `PersonService.cs:24`:

```csharp
    Task<List<Person>> GetListAsync(ViewerScope scope, ArchiveFilter filter = ArchiveFilter.Active,
        CancellationToken cancellationToken = default);
```

```csharp
    public async Task<List<Person>> GetListAsync(ViewerScope scope, ArchiveFilter filter = ArchiveFilter.Active,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await VisiblePeople(db, scope)
            .Apply(filter)
            .Include(p => p.Aliases)
            .OrderByDescending(p => p.ModifiedAt ?? p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
```

Dasselbe Muster in `FactionService`, `PersonGroupService`, `PartyService`, `OperationService`, `CaseService`: `ArchiveFilter filter = ArchiveFilter.Active` als zweiten Parameter einfügen und `.Apply(filter)` direkt hinter die vorhandene Sichtbarkeits-Abfrage setzen — **vor** die `Include`/`OrderBy`-Kette.

- [ ] **Step 4: `GetListAsync` im `TaskforceService`**

Der Parameter kommt hier **hinter `meId`**, damit die beiden vorhandenen Aufrufe mit ihren benannten Partner-Argumenten weiterlaufen:

```csharp
    Task<List<Taskforce>> GetListAsync(bool mayAll, string? meId, ArchiveFilter filter = ArchiveFilter.Active,
        CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null);
```

In der Implementierung `.Apply(filter)` an dieselbe Stelle wie oben.

- [ ] **Step 5: `SearchAsync` in allen sieben Diensten**

In **jeder** `SearchAsync`-Überladung (bei `CaseService` sind es zwei, `ICaseService.cs:16` und `:19`) `.OnlyActive()` direkt hinter die Ausgangsabfrage hängen. Beispiel `PersonService.cs:64`:

```csharp
        var query = db.People.OnlyActive().Where(p => isLeadership || !p.IsClassified);
```

Kommentar darüber:

```csharp
        // a picker must not offer a record that was filed away
```

- [ ] **Step 6: Aufrufstellen prüfen**

Diese neun Stellen rufen `GetListAsync` der sieben Typen auf. Keine übergibt den `CancellationToken` positionell, also muss **keine** geändert werden — nur gegenprüfen, dass sie weiter kompilieren:

`Pages/People/PeopleList.razor:158` · `Pages/Factions/FactionsList.razor:173` · `Pages/Groups/GroupsList.razor:123` · `Pages/Parties/PartiesList.razor:109` · `Pages/Operations/OperationsList.razor:151` · `Pages/Cases/CasesList.razor:150` · `Pages/Taskforces/TaskforceList.razor:100` · `Pages/Board/Shared/AnnouncementForm.razor:99` · `Pages/Wanted/Shared/WantedBoardPanel.razor:119`

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveListPathTests"`
Expected: PASS (5 Fälle).

- [ ] **Step 8: Run the full suite**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj`
Expected: PASS. Schlägt ein Bestandstest fehl, weil er eine archivierte Akte erwartet hat, ist das ein echtes Signal — nicht den Test aufweichen, sondern die Stelle prüfen.

- [ ] **Step 9: Commit**

```bash
git add NOOSE-Website NOOSE-Website.Tests
git commit -m "Keep archived records out of listings and pickers"
```

---

### Task 6: Suche — Facette und die sieben Sichtbarkeits-Helfer

**Files:**
- Modify: `NOOSE-Website/Models/Common/SearchModels.cs:6-26` (`SearchCriteria`)
- Modify: `NOOSE-Website/Models/Common/SearchQuery.cs` (Feld + `From` bei Zeile 46)
- Modify: `NOOSE-Website/Services/Search/Providers/RecordSearchProviders.cs` (sechs `Visible`-Helfer)
- Modify: `NOOSE-Website/Services/Search/Providers/OperationsSearchProviders.cs:75` (Taskforce)
- Modify: `NOOSE-Website/Components/Pages/Search/SearchPage.razor` (Facette + `BuildCriteria`)
- Test: `NOOSE-Website.Tests/Services/Integration/ArchiveSearchTests.cs`

**Interfaces:**
- Consumes: `RecordArchive.OnlyActive` (Task 2).
- Produces: `SearchCriteria.IncludeArchived` (bool), `SearchQuery.IncludeArchived` (bool, `init`), von `SearchQuery.From` durchgereicht.

**Der Trick dieser Aufgabe:** jeder der sieben Anbieter hat **einen** privaten `Visible(db, query)`-Helfer, den `SearchAsync`, `ResolveIdsAsync`, `QuickAsync` und `FuzzyAsync` alle benutzen. Eine Zeile dort deckt beide Suchwellen, die Kommandopalette und den @-Picker zugleich ab.

- [ ] **Step 1: Write the failing test**

`NOOSE-Website.Tests/Services/Integration/ArchiveSearchTests.cs`:

```csharp
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The default search hides the archive; the facet brings it back — in both waves.</summary>
public sealed class ArchiveSearchTests
{
    private static async Task<SearchTestHost> HostAsync()
    {
        var host = SearchTestHost.Create(maxConcurrency: 1);
        await using var db = host.Context.NewContext();
        db.People.Add(Seed.Person("aktiv", "Meier Aktiv"));
        db.People.Add(Seed.Person("archiv", "Meier Archiv", p => p.IsArchived = true));
        await db.SaveChangesAsync();
        return host;
    }

    [Fact]
    public async Task The_default_search_leaves_the_archive_out()
    {
        using var host = await HostAsync();
        var hits = await host.SearchAsync(new SearchCriteria { Text = "Meier" });
        Assert.DoesNotContain(hits, h => h.TargetId == "archiv");
        Assert.Contains(hits, h => h.TargetId == "aktiv");
    }

    [Fact]
    public async Task The_facet_brings_the_archive_back()
    {
        using var host = await HostAsync();
        var hits = await host.SearchAsync(new SearchCriteria { Text = "Meier", IncludeArchived = true });
        Assert.Contains(hits, h => h.TargetId == "archiv");
    }

    [Fact]
    public async Task The_phonetic_second_wave_respects_the_facet()
    {
        using var host = await HostAsync();
        // "Mayer" only reaches the record through the phonetic side index
        var hits = await host.SearchAsync(new SearchCriteria { Text = "Mayer", Fuzzy = true });
        Assert.DoesNotContain(hits, h => h.TargetId == "archiv");
    }

    [Fact]
    public async Task The_command_palette_never_offers_an_archived_record()
    {
        using var host = await HostAsync();
        var hits = await host.QuickSearchAsync("Meier");
        Assert.DoesNotContain(hits, h => h.TargetId == "archiv");
    }
}
```

**Vor dem Schreiben:** `NOOSE-Website.Tests/Infrastructure/SearchTestHost.cs` öffnen und die dortigen Fabrik- und Aufrufnamen übernehmen (`Create`, `SearchAsync`, `QuickSearchAsync`, `maxConcurrency`) — die Namen oben sind die erwarteten, die tatsächlichen stehen in der Datei. `MaxConcurrency = 1` ist Pflicht: `SqliteTestContext` gibt allen Kontexten dieselbe offene Verbindung, und zwei gleichzeitige Kommandos darauf sind undefiniert. Für den Fall, dass der Seitenindex im Host nicht automatisch gefüllt wird, den phonetischen Test erst laufen lassen, nachdem der Index über den im Host vorgesehenen Weg befüllt wurde.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveSearchTests"`
Expected: Compile error — `SearchCriteria.IncludeArchived` existiert nicht.

- [ ] **Step 3: Kriterium und Abfrage erweitern**

`NOOSE-Website/Models/Common/SearchModels.cs`, in `SearchCriteria` unter `MaxMode`:

```csharp
    /// <summary>Includes archived records. Off by default: the archive is out of the way, not gone.</summary>
    public bool IncludeArchived { get; set; }
```

`NOOSE-Website/Models/Common/SearchQuery.cs`, unter `Deep`:

```csharp
    /// <summary>Include archived records in every provider that knows the archive.</summary>
    public bool IncludeArchived { get; init; }
```

und in `SearchQuery.From` (Zeile 49 ff.) neben `Deep = criteria.MaxMode`:

```csharp
            IncludeArchived = criteria.IncludeArchived,
```

- [ ] **Step 4: Die sieben `Visible`-Helfer**

In `RecordSearchProviders.cs` bei den Zeilen 77 (`Person`), 188 (`Faction`), 273 (`PersonGroup`), 354 (`Party`), 439 (`Operation`), 523 (`Case`) und in `OperationsSearchProviders.cs:75` (`Taskforce`): direkt nach dem Aufbau von `q` — also nach der `OnlyPartnerVisible`/`OnlyVisible`-Zuweisung und **vor** dem Tag-Filter — diese zwei Zeilen einfügen:

```csharp
        // one line for both waves: SearchAsync, ResolveIdsAsync, QuickAsync and the fuzzy pass all come through here
        q = query.IncludeArchived ? q : q.OnlyActive();
```

Wo der Helfer das Ergebnis nicht in einer lokalen Variablen `q` hält, sondern direkt zurückgibt (z. B. ein einzeiliger `Taskforce`-Helfer), zuerst in eine lokale Variable auflösen und dann die Zeile einfügen.

- [ ] **Step 5: Facette auf der Suchseite**

`NOOSE-Website/Components/Pages/Search/SearchPage.razor` — Feld zu den übrigen Modus-Feldern:

```csharp
    private bool _includeArchived;
```

In die Modus-Leiste, direkt hinter den Deep-Scan-Schalter (Zeile 41–43):

```razor
            <MudTooltip Text="Nimmt archivierte Akten mit auf. Standardmäßig bleiben sie außen vor.">
                <MudCheckBox T="bool" @bind-Value="_includeArchived" Label="Archiv einschließen" Dense="true" Color="Color.Default" />
            </MudTooltip>
```

In `BuildCriteria()` (Zeile 263 ff.):

```csharp
        IncludeArchived = _includeArchived,
```

Wenn die Seite den Zustand in der URL hält (dort, wo `_fuzzy` bei Zeile 402 wieder eingelesen wird), `_includeArchived` nach demselben Muster mitschreiben und -lesen.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveSearchTests"`
Expected: PASS (4 Fälle).

- [ ] **Step 7: Run the search suites**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~Search"`
Expected: PASS — inklusive `SearchCatalogTests`, `SearchCoverageTests` und `HandbookSideIndexTests`.

- [ ] **Step 8: Commit**

```bash
git add NOOSE-Website NOOSE-Website.Tests
git commit -m "Hide archived records from search behind a facet"
```

---

### Task 7: Auswertungen — Dashboard, Statistik, Score, Aktualität

**Files:**
- Modify: `NOOSE-Website/Services/DashboardService.cs`
- Modify: `NOOSE-Website/Services/Statistics/StatisticsService.cs`, `ThreatStatisticsService.cs`, `ThroughputStatisticsService.cs`, `NetworkStatisticsService.cs`
- Modify: `NOOSE-Website/Services/Threat/ThreatScoreService.cs:359` und `:378`
- Test: `NOOSE-Website.Tests/Services/Integration/ArchiveEvaluationTests.cs`

**Interfaces:**
- Consumes: `RecordArchive.OnlyActive` (Task 2).
- Produces: keine neuen Signaturen — nur Verhaltensänderung.

- [ ] **Step 1: Write the failing test**

`NOOSE-Website.Tests/Services/Integration/ArchiveEvaluationTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Archived records stop skewing the numbers — that is the whole point of the feature.</summary>
public sealed class ArchiveEvaluationTests
{
    // mirrors DashboardServiceTests.Build: the service indexes settings[nameof(T)] for all seven types
    private static DashboardService BuildDashboard(SqliteTestContext ctx)
    {
        var requests = Substitute.For<IRequestService>();
        requests.GetOpenCountAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(0);

        var settings = new RecencySettings(10, 30, true);
        var recency = Substitute.For<IRecencyService>();
        recency.GetAllSettingsAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, RecencySettings>
        {
            [nameof(Person)] = settings,
            [nameof(Faction)] = settings,
            [nameof(PersonGroup)] = settings,
            [nameof(Party)] = settings,
            [nameof(Operation)] = settings,
            [nameof(Taskforce)] = settings,
            [nameof(Case)] = settings,
        });
        return new DashboardService(ctx.Factory, requests, recency);
    }

    private static async Task<SqliteTestContext> StockAsync()
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        db.People.Add(Seed.Person("aktiv", "Aktiv"));
        db.People.Add(Seed.Person("archiv", "Archiv", p => p.IsArchived = true));
        db.Factions.Add(Seed.Faction("f-aktiv", "Aktiv"));
        db.Factions.Add(Seed.Faction("f-archiv", "Archiv", f => f.IsArchived = true));
        await db.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task The_dashboard_counts_only_the_active_stock()
    {
        using var ctx = await StockAsync();
        var metrics = await BuildDashboard(ctx).GetMetricsAsync(isLeadership: true, meId: "lead");
        Assert.Equal(1, metrics.People);
        Assert.Equal(1, metrics.FactionsAndGroups);
    }

    [Fact]
    public async Task An_archived_record_is_never_due_for_update()
    {
        using var ctx = await StockAsync();
        var stale = await BuildDashboard(ctx).GetUpdateNeedAsync(isLeadership: true, meId: "lead");
        Assert.DoesNotContain(stale, s => s.Name == "Archiv");
    }
}
```

`GetMetricsAsync(bool isLeadership, string? meId, …)` liefert `DashboardMetrics(People, FactionsAndGroups, Operations, OpenCases, OpenRequests, Classified, StaleRecords)` — `NOOSE-Website/Models/Dashboard/DashboardModels.cs:18`. Den Aufbau oben gegen `DashboardServiceTests.Build` (`DashboardServiceTests.cs:25-37`) gegenprüfen, falls sich der Konstruktor inzwischen geändert hat.

Den Score-Lauf prüft ein dritter Fall in derselben Klasse. Er baut `ThreatScoreService` nach dem Muster aus `NOOSE-Website.Tests/Services/Threat/` (dort stehen die vorhandenen Score-Tests samt Konstruktor-Attrappen), ruft `NewCalculateAllPeopleScoresAsync()` und prüft:

```csharp
        await using var db = ctx.NewContext();
        Assert.Null((await db.People.SingleAsync(p => p.Id == "archiv")).ScoreCalculatedAt);
        Assert.NotNull((await db.People.SingleAsync(p => p.Id == "aktiv")).ScoreCalculatedAt);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveEvaluationTests"`
Expected: FAIL — die Zählungen liefern 2 statt 1.

- [ ] **Step 3: Score-Lauf**

`NOOSE-Website/Services/Threat/ThreatScoreService.cs`, Zeilen 359 und 378:

```csharp
        // an archived record does not decay; the last value stays on the file
        var ids = await db.Factions.OnlyActive().Select(f => f.Id).ToListAsync(cancellationToken);
```

```csharp
        var ids = await db.People.OnlyActive().Select(p => p.Id).ToListAsync(cancellationToken);
```

Die beiden `Preview*Distribution`-Methoden (Zeilen 391 und 416) ebenso — eine Vorschau soll denselben Bestand zeigen wie der Lauf.

- [ ] **Step 4: Dashboard**

`NOOSE-Website/Services/DashboardService.cs` — an **jede** Abfrage auf `db.People`, `db.Factions`, `db.PersonGroups`, `db.Parties`, `db.Operations`, `db.Cases`, `db.Taskforces` ein `.OnlyActive()` direkt hinter das DbSet hängen. Die Stellen: Zeilen 24–28, 31, 38, 46–52, 68–78, 98, 114, 142, 157, 172, 187, 202, 223, 240, 259, 281, 298. Beispiel Zeile 24:

```csharp
        var people = await db.People.OnlyActive().CountAsync(p => isLeadership || !p.IsClassified, cancellationToken);
```

Bei den Taskforce-Zeilen kommt `.OnlyActive()` **hinter** `OnlyVisible(db, isLeadership, meId)`:

```csharp
            + await db.Taskforces.OnlyVisible(db, isLeadership, meId).OnlyActive()
                .CountAsync(t => t.Status == TaskforceStatus.Requested, cancellationToken)
```

Zum Schluss in der Datei nach `db.People`, `db.Factions`, `db.PersonGroups`, `db.Parties`, `db.Operations`, `db.Cases`, `db.Taskforces` greppen und sicherstellen, dass keine Fundstelle ohne `.OnlyActive()` bleibt.

- [ ] **Step 5: Statistik**

Dieselbe Behandlung in den vier Statistikdiensten:
- `StatisticsService.cs`: Zeilen 27, 39, 42, 55, 69, 80, 92, 112
- `ThreatStatisticsService.cs`: Zeilen 29, 33, 101, 109, 142, 147, 172, 176, 199, 201
- `ThroughputStatisticsService.cs`: Zeilen 30, 57, 94, 98
- `NetworkStatisticsService.cs`: Zeile 115

- [ ] **Step 6: Aktualitäts-Ampel**

Die Ampel wird an zwei Orten berechnet:

1. **Dashboard**, Zeilen 68–78: dort steht je Typ schon `!p.AgingDisabled`. Das `.OnlyActive()` aus Step 4 erledigt den Rest.
2. **In den sieben Listen** (Task 9): dort steht die Zeile
   `Level = settings.AgingDisabled || p.AgingDisabled ? RecencyLevel.Fresh : RecencyAssessment.Level(...)`
   (`PeopleList.razor:182`). Sie wird in Task 9 um `|| p.IsArchived` erweitert, damit eine mit „Mit Archiv" eingeblendete Akte nicht rot leuchtet.

`FactionRecency` selbst bleibt unverändert: es beantwortet „wie alt sind die Angaben dieser einen Fraktion", und diese Frage ist auch für eine archivierte Akte richtig beantwortet.

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~ArchiveEvaluationTests"`
Expected: PASS.

- [ ] **Step 8: Run the full suite**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add NOOSE-Website NOOSE-Website.Tests
git commit -m "Take archived records out of the numbers"
```

---

### Task 8: Detailseiten — Band und Knopf

**Files:**
- Modify: die sieben Detailseiten aus der Typ-Tabelle

**Interfaces:**
- Consumes: `ArchiveAsync`/`UnarchiveAsync` der sieben Dienste (Task 4).
- Produces: keine.

Kein Test — `.razor` ist ohne bUnit nicht testbar; die Abnahme ist Build plus Klick durch die App.

- [ ] **Step 1: Band und Knopf in `PersonDetail.razor`**

Das Band kommt in den `else`-Zweig, direkt **vor** den öffentlichen Ausschreibungs-Block (vor Zeile 25):

```razor
    @if (_person.IsArchived)
    {
        <MudAlert Severity="Severity.Info" Variant="Variant.Outlined" Dense="true" Class="mb-4">
            <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
                <div>
                    <MudText Typo="Typo.body2">
                        Archiviert am @(_person.ArchivedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—") — diese Akte
                        erscheint nicht in Listen, Auswahllisten und der Standardsuche.
                    </MudText>
                    @if (!string.IsNullOrWhiteSpace(_person.ArchiveReason))
                    {
                        <MudText Typo="Typo.caption" Class="mud-text-secondary">Grund: @_person.ArchiveReason</MudText>
                    }
                </div>
                <MudSpacer />
                <AuthorizeView Policy="@Policies.WriteAccess" Context="archiv">
                    <MudButton Size="Size.Small" Variant="Variant.Text" StartIcon="@Icons.Material.Filled.Unarchive"
                               OnClick="UnarchiveAsync">Zurückholen</MudButton>
                </AuthorizeView>
            </MudStack>
        </MudAlert>
    }
```

Der Knopf kommt in die Aktionsleiste, direkt **vor** den Löschen-Knopf (vor Zeile 61):

```razor
        <AuthorizeView Policy="@Policies.WriteAccess" Context="ablegen">
            @if (!_person.IsArchived)
            {
                <MudButton Color="Color.Default" Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Archive"
                           OnClick="ArchiveAsync">Archivieren</MudButton>
            }
        </AuthorizeView>
```

- [ ] **Step 2: Die beiden Methoden im `@code`-Block**

Direkt neben `DeleteAsync` (Zeile 357):

```csharp
    private async Task ArchiveAsync()
    {
        if (_person is null)
        {
            return;
        }
        var reason = await ReasonDialog.ShowAsync(DialogService, "Akte archivieren?",
            "Die Akte verschwindet aus Listen, Auswahllisten und der Standardsuche. Verknüpfungen, Erwähnungen "
            + "und der Graph bleiben unverändert; zurückholen kann sie jeder mit Schreibrecht.",
            confirmText: "Archivieren", label: "Grund (optional)");
        if (reason is null)
        {
            return;
        }
        await PersonService.ArchiveAsync(_person.Id, reason, _user);
        Snackbar.Add("Akte archiviert.", Severity.Success);
        await NewLoadAsync();
    }

    private async Task UnarchiveAsync()
    {
        if (_person is null)
        {
            return;
        }
        await PersonService.UnarchiveAsync(_person.Id, _user);
        Snackbar.Add("Akte ist wieder im aktiven Bestand.", Severity.Success);
        await NewLoadAsync();
    }
```

`ReasonDialog.ShowAsync` gibt bei Abbruch `null` zurück und sonst den (ggf. leeren) Text — `NOOSE-Website/Components/Common/Shared/ReasonDialog.razor:67`.

- [ ] **Step 3: Warnung, wenn die Akte öffentlich ausgeschrieben ist**

Nur in `PersonDetail.razor`: Wenn `_publicBanner is { Status: PublicWantedStatus.Veroeffentlicht }`, den Dialogtext um diesen Satz ergänzen (zweiter Parameter von `ShowAsync`, per String-Verkettung):

```csharp
            + " Achtung: Diese Akte ist öffentlich ausgeschrieben. Die Ausschreibung läuft weiter."
```

- [ ] **Step 4: Dasselbe in den sechs übrigen Detailseiten**

`FactionDetail.razor`, `GroupDetail.razor`, `PartyDetail.razor`, `OperationDetail.razor`, `CaseDetail.razor`, `TaskforceDetail.razor`. Zu ersetzen sind: das Feld (`_person` → `_faction`, `_group`, `_party`, `_operation`, `_case`, `_taskforce` — der tatsächliche Feldname steht jeweils oben im `@code`-Block), der Dienst (`PersonService` → der jeweilige) und das Wort „Akte" im Dialog- und Snackbar-Text („Fraktion", „Personengruppe", „Partei", „Operation", „Vorgang", „Taskforce"). Der Warnsatz aus Step 3 entfällt dort. Das Nachladen heißt in jeder Seite anders — die vorhandene Methode neben deren `DeleteAsync` benutzen.

- [ ] **Step 5: Vorgangs-Status koppeln**

In `CaseDetail.razor`: steht der Vorgang auf `CaseStatus.Archived` und ist **nicht** archiviert, unter der Statuszeile ein Hinweis mit Knopf:

```razor
    @if (_case.Status == CaseStatus.Archived && !_case.IsArchived)
    {
        <AuthorizeView Policy="@Policies.WriteAccess" Context="vorschlag">
            <MudAlert Severity="Severity.Normal" Variant="Variant.Text" Dense="true" Class="mb-2">
                Dieser Vorgang ist als archiviert gekennzeichnet, steht aber noch im aktiven Bestand.
                <MudButton Size="Size.Small" Variant="Variant.Text" OnClick="ArchiveAsync">Jetzt archivieren</MudButton>
            </MudAlert>
        </AuthorizeView>
    }
```

Der Status wird dabei **nicht** automatisch gesetzt und Archivieren ändert den Status nicht — nur dieser Vorschlag verbindet die beiden.

- [ ] **Step 6: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: 0 Fehler, 0 neue Warnungen.

- [ ] **Step 7: Commit**

```bash
git add NOOSE-Website/Components
git commit -m "Add the archive banner and button to every record page"
```

---

### Task 9: Listen — Filter und Chip

**Files:**
- Modify: die sieben Listen aus der Typ-Tabelle

**Interfaces:**
- Consumes: `GetListAsync(..., ArchiveFilter, ...)` (Task 5).
- Produces: keine.

- [ ] **Step 1: Filter in `PeopleList.razor`**

Feld zu den übrigen Filterfeldern (Zeile 144 ff.):

```csharp
    private ArchiveFilter _archive = ArchiveFilter.Active;
```

In `OnInitializedAsync` nach den anderen `QueryState`-Zeilen (Zeile 155):

```csharp
        _archive = QueryState.ReadEnum<ArchiveFilter>(Nav, "archiv") ?? ArchiveFilter.Active;
```

Das Laden aus `OnInitializedAsync` in eine eigene `LoadAsync()`-Methode ziehen (Rumpf ab Zeile 157 unverändert übernehmen, nur `GetListAsync(ViewerScope.From(user), _archive)` statt `GetListAsync(ViewerScope.From(user))`), damit der Filter neu laden kann:

```csharp
    private async Task ArchiveSelectedAsync(ArchiveFilter value)
    {
        _archive = value;
        await PersistFiltersAsync();
        _load = true;
        await LoadAsync();
        _load = false;
    }
```

`PersistFiltersAsync` um einen Eintrag erweitern (Zeile 233):

```csharp
        ("archiv", _archive == ArchiveFilter.Active ? null : _archive.ToString()));
```

`ResetFiltersAsync` setzt zusätzlich `_archive = ArchiveFilter.Active;` und lädt neu. `HasFilter` bekommt `|| _archive != ArchiveFilter.Active`.

Auswahlfeld in die Toolbar, hinter das Aktualitäts-Feld (Zeile 44–51):

```razor
            <MudSelect T="ArchiveFilter" Value="_archive" ValueChanged="ArchiveSelectedAsync" Label="Archiv" Dense="true"
                       Variant="Variant.Outlined" Style="max-width:170px;" Margin="Margin.Dense" Class="ml-2">
                <MudSelectItem T="ArchiveFilter" Value="ArchiveFilter.Active">Aktiv</MudSelectItem>
                <MudSelectItem T="ArchiveFilter" Value="ArchiveFilter.Including">Mit Archiv</MudSelectItem>
                <MudSelectItem T="ArchiveFilter" Value="ArchiveFilter.Only">Nur Archiv</MudSelectItem>
            </MudSelect>
```

- [ ] **Step 2: Chip und Ampel in der Zeile**

`PersonRow` um ein Feld erweitern (die Record-/Klassendefinition steht unten in der Datei):

```csharp
        public bool IsArchived { get; init; }
```

In der Projektion (Zeile 163 ff.) `IsArchived = p.IsArchived,` ergänzen und die Ampel-Zeile (Zeile 182) erweitern:

```csharp
                Level = settings.AgingDisabled || p.AgingDisabled || p.IsArchived
                    ? RecencyLevel.Fresh
                    : RecencyAssessment.Level(settings.WarningDays, settings.StaleDays, referenceUtc, now),
```

In der Namensspalte (Zeile 57 ff.) hinter den `MudLink`:

```razor
                    @if (context.Item.IsArchived)
                    {
                        <MudChip T="string" Size="Size.Small" Color="Color.Default" Variant="Variant.Outlined" Class="ml-2">Archiviert</MudChip>
                    }
```

- [ ] **Step 3: Dasselbe in den sechs übrigen Listen**

`FactionsList.razor`, `GroupsList.razor`, `PartiesList.razor`, `OperationsList.razor`, `CasesList.razor`, `TaskforceList.razor`. Alle folgen demselben Aufbau: `QueryState`-Lesen in `OnInitializedAsync`, ein `Persist…`-Sammler, eine Projektion in einen Row-Typ, eine Toolbar mit `MudSelect`-Filtern. Bei `TaskforceList.razor:100` lautet der Aufruf

```csharp
        var taskforces = await TaskforceService.GetListAsync(user.MayAllTaskforcesSee(), user.GetAgentId(), _archive,
            partnerAgency: user.GetPartnerAgency(), partnerAgentId: user.GetAgentId());
```

- [ ] **Step 4: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add NOOSE-Website/Components
git commit -m "Add an archive filter to every record list"
```

---

### Task 10: Graph — archivierte Knoten ausgrauen

**Files:**
- Modify: `NOOSE-Website/Models/Graph/GraphModelle.cs:16-31`
- Modify: `NOOSE-Website/Services/Graph/GraphService.cs:259-…`
- Modify: `NOOSE-Website/wwwroot/js/graph.js` und **alle** Importstellen mit `?v=`

**Interfaces:**
- Consumes: die `IsArchived`-Spalte (Task 1).
- Produces: `GraphNode.IsArchived` (bool, Standard `false`), im JSON als `isArchived`.

- [ ] **Step 1: Knoten-Modell erweitern**

`GraphModelle.cs`, als letzter Parameter des `GraphNode`-Records:

```csharp
    bool IsFocus = false,
    bool IsArchived = false);
```

- [ ] **Step 2: Sieben Projektionen in `GraphService.ResolveNodeAsync`**

In jedem Typ-Block die anonyme Projektion um `x.IsArchived` erweitern und beim Bauen des Knotens durchreichen. Beispiel `Person` (Zeile 290 ff.):

```csharp
            var rows = await db.People.Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified, p.Classification, p.CreatedAt, p.ThreatScore, p.IsArchived })
                .ToListAsync(cancellationToken);
```

```csharp
                result[$"{nameof(Person)}:{x.Id}"] = Mk(nameof(Person), x.Id, x.Name, x.CaseNumber, $"/personen/{x.Id}", (int)x.Classification, x.IsClassified)
                    with { CreatedAt = x.CreatedAt, ThreatScore = x.ThreatScore, IsArchived = x.IsArchived };
```

Dasselbe für die Blöcke `Faction`, `PersonGroup`, `Party`, `Operation`, `Case`, `Taskforce` in derselben Methode. Blöcke anderer Typen (z. B. Dokumente) bleiben unangetastet — sie kennen kein Archiv.

- [ ] **Step 3: Darstellung in `graph.js`**

Dort, wo aus einem Knoten das vis-network-Objekt gebaut wird, nach dem Setzen von Farbe und Beschriftung ergänzen:

```javascript
    // an archived record stays in the picture, just quiet
    if (n.isArchived) {
        node.opacity = 0.45;
        node.font = Object.assign({}, node.font, { color: '#8a8a8a' });
        node.shapeProperties = Object.assign({}, node.shapeProperties, { borderDashes: [4, 4] });
    }
```

Die JSON-Schlüssel sind die englischen CLR-Namen in camelCase — C#-Seite und JS-Map müssen synchron bleiben.

- [ ] **Step 4: `?v=` hochzählen**

Alle Importstellen von `graph.js` suchen (`grep -rn "graph.js?v=" NOOSE-Website/`) und **jede** auf dieselbe neue Nummer setzen. Zwei verschiedene Nummern holen zwei Kopien des Moduls.

- [ ] **Step 5: Build und Sichtprüfung**

Run: `dotnet build NOOSE-Website.slnx`
Dann die App starten, eine Akte archivieren und `/graph` öffnen: der Knoten muss blass und gestrichelt sein, seine Kanten müssen stehen bleiben.

- [ ] **Step 6: Commit**

```bash
git add NOOSE-Website
git commit -m "Grey out archived nodes in the relationship graph"
```

---

### Task 11: Handbuch, Glossar, Changelog

**Files:**
- Modify: `NOOSE-Website/Infrastructure/Handbook/Content/RecordsChapter.cs` (nach `art-papierkorb`, Zeile 270)
- Modify: `NOOSE-Website/Infrastructure/Handbook/Content/GlossaryContent.cs`
- Modify: `NOOSE-Website/Infrastructure/Changelog/ChangelogContent.cs` (ans Ende der letzten `SeededRelease`)

**Interfaces:**
- Consumes: nichts.
- Produces: Artikel `art-archiv` mit Slug `akten-archivieren`; Glossarbegriff `beg-archiv`; Changelog-Zeile `2.1.65-archiv`.

- [ ] **Step 1: Handbuch-Artikel**

In `RecordsChapter.cs` direkt **nach** dem `art-papierkorb`-Artikel einfügen:

```csharp
            new Article("art-archiv", "akten-archivieren", "Akten archivieren",
                "Erledigtes aus dem Weg räumen, ohne es zu löschen.",
                """
                <p>Eine Akte, die niemand mehr braucht, muss nicht gelöscht werden. Du kannst sie
                <strong>archivieren</strong>: sie verschwindet aus den Listen, aus den Auswahlfeldern und aus
                der normalen Suche - bleibt aber vollständig erhalten und lesbar.</p>
                <p>Archivieren findest du auf der Akte selbst, neben <em>Löschen</em>. Du kannst einen Grund
                angeben; er steht später oben auf der Akte und im Nachweis. Zurückholen kann sie jeder, der
                schreiben darf - ein Klick auf <em>Zurückholen</em> in dem Hinweisband.</p>
                <p><strong>Archiv ist nicht Papierkorb.</strong> Im Papierkorb liegt Gelöschtes, das nur die
                Führung zurückholt. Das Archiv ist der ruhende Bestand: die Akte ist da, sie steht nur nicht
                mehr im Weg. Verknüpfungen, Erwähnungen und der Beziehungsgraph zeigen sie weiter an.</p>
                <p>Willst du eine archivierte Akte sehen, stell in der Liste den Filter <em>Archiv</em> auf
                <em>Mit Archiv</em> oder <em>Nur Archiv</em>; in der Suche gibt es den Schalter
                <em>Archiv einschließen</em>.</p>
                """,
                RoleplayHtml:
                """
                <p>Eine Behörde sondert aus. Eine aufgelöste Fraktion, eine dauerhaft tote Person, ein
                abgeschlossener Vorgang - das gehört ins Archiv, nicht in den täglichen Umlauf. Wer eine alte
                Akte wieder braucht, holt sie heraus; niemand muss dafür fragen.</p>
                """,
                Steps:
                [
                    new Step("Archive", "Archivieren",
                        "Auf der Akte neben Löschen. Der Grund ist freiwillig, hilft aber dem Nächsten."),
                    new Step("FilterList", "Wiederfinden",
                        "In der Liste den Filter Archiv umstellen, oder in der Suche den Schalter setzen."),
                    new Step("Unarchive", "Zurückholen",
                        "Ein Klick im Hinweisband oben auf der Akte. Sie steht sofort wieder in allen Listen."),
                ]),
```

Kein `NavKey` und kein `DiagrammSchluessel` — der Artikel gehört zu keiner eigenen Seite und braucht keine Zeichnung. Da es ein **neuer** Artikel ist, bleibt `HandbookContent.Revision` unverändert.

- [ ] **Step 2: Glossarbegriff**

In `GlossaryContent.cs` bei den Akten-Begriffen:

```csharp
        new("beg-archiv", "Archiv",
            "Der ruhende Aktenbestand. Eine archivierte Akte bleibt vollständig lesbar, erscheint aber nicht "
            + "in Listen, Auswahlfeldern und der normalen Suche. Nicht zu verwechseln mit dem Papierkorb.",
            ArticleKey: "art-archiv"),
```

- [ ] **Step 3: Changelog-Zeile**

In `ChangelogContent.cs` ans Ende der letzten `SeededRelease` (hinter `2.1.64-kennzeichen-rang`):

```csharp
            Neu("2.1.65-archiv", "Personen, Fraktionen, Gruppen, Parteien, Vorgänge, Operationen und Taskforces "
                + "lassen sich archivieren: sie verschwinden aus Listen und Suche, bleiben aber lesbar und sind "
                + "mit einem Klick wieder da.", "Akten"),
```

Alltagssprache, keine Technik — `ChangelogTests.No_shipped_line_talks_about_the_technology` hält eine Sperrliste dagegen. Kein `Revision`-Bump: eine neue Zeile wird an ihrem fehlenden Key erkannt.

- [ ] **Step 4: Run the handbook and changelog tests**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj --filter "FullyQualifiedName~Handbook|FullyQualifiedName~Changelog|FullyQualifiedName~Glossary"`
Expected: PASS — insbesondere `Every_shipped_slug_is_already_url_clean`, `Every_shipped_term_is_written_only_once` und `Every_shipped_nav_key_names_a_menu_entry`.

- [ ] **Step 5: Commit**

```bash
git add NOOSE-Website/Infrastructure
git commit -m "Document archiving in handbook, glossary and changelog"
```

---

### Task 12: Abnahme

**Files:** keine.

- [ ] **Step 1: Volle Testsuite**

Run: `dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj`
Expected: PASS, ~3.5k Tests.

- [ ] **Step 2: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: 0 Fehler, 0 neue Warnungen.

- [ ] **Step 3: App wirklich starten**

Run: `dotnet run --project NOOSE-Website/NOOSE-Website.csproj`
Die Migration wird beim Start automatisch angewandt (`db.Database.MigrateAsync()`).

Durchklicken: Person archivieren (mit Grund) → verschwindet aus `/personen` → Filter *Nur Archiv* zeigt sie → Detailseite zeigt das Band → Verknüpfungen auf anderen Akten zeigen sie weiter → `/suche` findet sie erst mit *Archiv einschließen* → Strg+K findet sie nicht → `/graph` zeigt sie blass → `/nachweis` zeigt „Archiviert" mit Grund → Zurückholen bringt sie in die Liste und lässt `Geändert am` unverändert.

- [ ] **Step 4: Handbuch prüfen**

`/handbuch/akten-archivieren` öffnen: Artikel, Rollenspiel-Kasten und drei Schritt-Karten müssen da sein; „Archiv" im Text muss eine Erklär-Blase tragen.

- [ ] **Step 5: Abschluss-Commit**

```bash
git add -A
git commit -m "Finish the record archive"
```

---

## Selbstprüfung des Plans

**Spec-Abdeckung:** §4.1 Interface → Task 1 · §4.2 Helfer → Task 2 · §4.3 Migration → Task 1 · §4.4 Schreibweg und Audit-Aktionen → Tasks 3 und 4 · §5.1 Filterstellen → Tasks 5, 6, 7 · §5.2 unberührte Pfade → Task 5 Step 1 (Detailseite) und Task 10 (Graph) · §5.3 `CaseStatus.Archived` → Task 8 Step 5 · §6 Oberfläche → Tasks 8, 9, 10 · §7 Tests → in jeder Aufgabe · §8 Handbuch/Changelog → Task 11.

Die neun Prüfpunkte aus Spec §7 liegen in Task 1 (Rundlauf der Spalten), Task 4 (Schreibrecht, Audit-Zeile, `GeaendertAm`), Task 5 (Liste, Picker, Detailseite, Verknüpfung, Papierkorb-Achse), Task 6 (beide Suchwellen, Kommandopalette) und Task 7 (Zahlen, Score-Lauf).

**Bewusst offen:** zwei Tests übernehmen Namen aus vorhandenen Testhelfern — `ClaimsPrincipalBuilder` (Task 4, für Nur-Lese-Aufsicht/Partner/Demo) und `SearchTestHost` (Task 6, für Fabrik und Suchaufruf). Beide Schritte sagen ausdrücklich, welche Datei zu öffnen ist und was daraus zu übernehmen ist; erfundene Namen stehen nirgends sonst im Plan.
