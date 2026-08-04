using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Verifies that writes bypassing the SaveChanges interceptor still record an audit trail via <see cref="ManualAudit"/>.</summary>
public sealed class AuditCoverageTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static async Task<List<AuditLog>> AuditRowsAsync(SqliteTestContext ctx, string entityType, string entityId)
    {
        await using var db = ctx.NewContext();
        return await db.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .ToListAsync();
    }

    [Fact]
    public async Task RecencyService_SetRecordExemption_WritesAuditRow()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1"));
            db.SaveChanges();
        }
        var svc = new RecencyService(ctx.Factory, new MemoryCache(new MemoryCacheOptions()));

        await svc.SetRecordExemptionAsync(nameof(Person), "p1", true, Leader());

        var rows = await AuditRowsAsync(ctx, nameof(Person), "p1");
        var row = Assert.Single(rows);
        Assert.Equal(AuditAction.Modified, row.Action);
        Assert.Equal("Falcon", row.AgentName);
        var changes = AuditDisplay.Parse(row.ChangesJson);
        Assert.Contains(changes, c => c.Field == "Aktualitäts-Ausnahme" && c.New == "Ja");
    }

    [Fact]
    public async Task ValueListLabelService_Set_WritesAuditRow()
    {
        using var ctx = new SqliteTestContext();
        var svc = new ValueListLabelService(ctx.Factory);

        await svc.SetAsync("Waffen", "glock", "Glock 17", Leader());

        var rows = await AuditRowsAsync(ctx, nameof(EnumLabelOverride), "Waffen:glock");
        var row = Assert.Single(rows);
        Assert.Equal(AuditAction.Modified, row.Action);
        Assert.Contains("Glock 17", row.ChangesJson);
    }

    [Fact]
    public async Task TagService_SetOnRecord_WritesAuditRowAgainstRecord()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1"));
            db.Tags.Add(new Tag { Id = "t1", Name = "Verdächtig" });
            db.SaveChanges();
        }
        var svc = new TagService(ctx.Factory);

        await svc.SetAsync(nameof(Person), "p1", new[] { "t1" }, Leader());

        var rows = await AuditRowsAsync(ctx, nameof(Person), "p1");
        var row = Assert.Single(rows);
        Assert.Equal(AuditAction.Modified, row.Action);
        var changes = AuditDisplay.Parse(row.ChangesJson);
        Assert.Contains(changes, c => c.New.Contains("Verdächtig"));
    }

    [Fact]
    public async Task PersonMergeService_Merge_WritesAuditRowOnTarget()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("src", "Quelle"));
            db.People.Add(Seed.Person("tgt", "Ziel"));
            db.SaveChanges();
        }
        var svc = new PersonMergeService(ctx.Factory);

        await svc.MergeAsync("src", "tgt", Leader());

        var rows = await AuditRowsAsync(ctx, nameof(Person), "tgt");
        var row = Assert.Single(rows);
        Assert.Equal(AuditAction.Modified, row.Action);
        var changes = AuditDisplay.Parse(row.ChangesJson);
        Assert.Contains(changes, c => c.Field == "Zusammengeführt aus" && c.New.Contains("Quelle"));
    }
}
