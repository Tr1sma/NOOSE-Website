using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="CustomFieldValueService"/> against in-memory SQLite.</summary>
public sealed class CustomFieldValueServiceTests
{
    private static CustomFieldValueService Build(SqliteTestContext ctx)
        => new(ctx.Factory, Substitute.For<INotificationService>());

    // Neither method calls a Permission guard; any principal is accepted by SetAsync.
    private static ClaimsPrincipal Actor()
        => ClaimsPrincipalBuilder.Agent("author").AsAdmin().WithRank(Rank.Director).Build();

    // External partner: only sees a whole-record release of a non-classified record.
    private static ClaimsPrincipal Partner()
        => ClaimsPrincipalBuilder.Agent("partner1").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build();

    private static CustomFieldDefinition Def(string id, string entityType = "Person", string name = "Feld",
        int order = 0, bool active = true, bool mandatory = false, CustomFieldType type = CustomFieldType.Text)
        => new()
        {
            Id = id,
            EntityType = entityType,
            Name = name,
            FieldType = type,
            Order = order,
            IsActive = active,
            Mandatory = mandatory,
        };

    private static CustomFieldValue Value(string defId, string entityType, string entityId, string? value)
        => new()
        {
            CustomFieldDefinitionId = defId,
            EntityType = entityType,
            EntityId = entityId,
            Value = value,
        };

    // ---------- GetForRecordAsync ----------

    [Fact]
    public async Task GetForRecordAsync_ReturnsDefinitionsWithValues_InDisplayOrder()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Zweitname", order: 1));
            db.CustomFieldDefinitions.Add(Def("d2", name: "Erstname", order: 0));
            db.CustomFieldValues.Add(Value("d2", "Person", "p1", "Wert-Erst"));
            // no value stored for d1 -> null in the display
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p1");

        // ordered by Order then Name: d2 (Order 0) before d1 (Order 1)
        Assert.Equal(new[] { "d2", "d1" }, result.Select(r => r.Definition.Id).ToArray());
        Assert.Equal("Wert-Erst", result[0].Value);
        Assert.Null(result[1].Value);
    }

    [Fact]
    public async Task GetForRecordAsync_OrdersByNameWithinSameOrder()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("z", name: "Zeta", order: 0));
            db.CustomFieldDefinitions.Add(Def("a", name: "Alpha", order: 0));
            db.CustomFieldDefinitions.Add(Def("b", name: "Beta", order: 0));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p1");

        Assert.Equal(new[] { "Alpha", "Beta", "Zeta" }, result.Select(r => r.Definition.Name).ToArray());
    }

    [Fact]
    public async Task GetForRecordAsync_ExcludesInactiveDefinitions()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("active", name: "Aktiv", active: true));
            db.CustomFieldDefinitions.Add(Def("inactive", name: "Inaktiv", active: false));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p1");

        Assert.Single(result);
        Assert.Equal("active", result[0].Definition.Id);
    }

    [Fact]
    public async Task GetForRecordAsync_ExcludesDefinitionsOfOtherEntityType()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("person-def", entityType: "Person"));
            db.CustomFieldDefinitions.Add(Def("faction-def", entityType: "Faction"));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p1");

        Assert.Single(result);
        Assert.Equal("person-def", result[0].Definition.Id);
    }

    [Fact]
    public async Task GetForRecordAsync_ReturnsEmpty_WhenNoDefinitions()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p1");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetForRecordAsync_Partner_ReturnsValues_WhenRecordSharedWhole()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p2"));
            db.CustomFieldDefinitions.Add(Def("d1", name: "Feld"));
            db.CustomFieldValues.Add(Value("d1", "Person", "p2", "sichtbar"));
            // whole-record release to the agency, children included
            db.PartnerShares.Add(new PartnerShare
            {
                EntityType = "Person",
                EntityId = "p2",
                Agency = PartnerAgency.LSPD,
                PartnerAgentId = null,
                IncludesChildren = true,
            });
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p2", scope: ViewerScope.From(Partner()));

        Assert.Single(result);
        Assert.Equal("sichtbar", result[0].Value);
    }

    [Fact]
    public async Task GetForRecordAsync_Partner_ReturnsEmpty_WhenRecordNotShared()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p3"));
            db.CustomFieldDefinitions.Add(Def("d1", name: "Feld"));
            db.CustomFieldValues.Add(Value("d1", "Person", "p3", "geheim"));
            // no PartnerShare row -> not visible to the partner
            db.SaveChanges();
        }
        var svc = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p3", scope: ViewerScope.From(Partner()));

        Assert.Empty(result);
    }

    // ---------- SetAsync ----------

    [Fact]
    public async Task SetAsync_AddsNewValues_ForActiveDefinitions()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Feld1"));
            db.CustomFieldDefinitions.Add(Def("d2", name: "Feld2"));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        await svc.SetAsync("Person", "p1",
            new Dictionary<string, string?> { ["d1"] = "eins", ["d2"] = "zwei" }, Actor());

        using var check = ctx.NewContext();
        var stored = await check.CustomFieldValues
            .Where(v => v.EntityType == "Person" && v.EntityId == "p1")
            .OrderBy(v => v.CustomFieldDefinitionId)
            .ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Equal("eins", stored[0].Value);
        Assert.Equal("zwei", stored[1].Value);
    }

    [Fact]
    public async Task SetAsync_TrimsValues_BeforeStoring()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Feld1"));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        await svc.SetAsync("Person", "p1",
            new Dictionary<string, string?> { ["d1"] = "  padded  " }, Actor());

        using var check = ctx.NewContext();
        var stored = await check.CustomFieldValues.SingleAsync(v => v.EntityId == "p1");
        Assert.Equal("padded", stored.Value);
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingValue_WhenChanged()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Feld1"));
            db.CustomFieldValues.Add(Value("d1", "Person", "p1", "alt"));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        await svc.SetAsync("Person", "p1",
            new Dictionary<string, string?> { ["d1"] = "neu" }, Actor());

        using var check = ctx.NewContext();
        var stored = await check.CustomFieldValues.SingleAsync(v => v.EntityId == "p1");
        Assert.Equal("neu", stored.Value);
    }

    [Fact]
    public async Task SetAsync_RemovesValue_WhenNewValueIsEmpty()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Feld1"));
            db.CustomFieldValues.Add(Value("d1", "Person", "p1", "vorhanden"));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        // empty/whitespace collapses to null -> existing value is removed
        await svc.SetAsync("Person", "p1",
            new Dictionary<string, string?> { ["d1"] = "   " }, Actor());

        using var check = ctx.NewContext();
        // no soft-delete interceptor in tests => hard delete, row is gone from the filtered set
        Assert.False(await check.CustomFieldValues.AnyAsync(v => v.EntityId == "p1"));
    }

    [Fact]
    public async Task SetAsync_KeepsValueUnchanged_WhenIdentical()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Feld1"));
            db.CustomFieldValues.Add(Value("d1", "Person", "p1", "gleich"));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        await svc.SetAsync("Person", "p1",
            new Dictionary<string, string?> { ["d1"] = "gleich" }, Actor());

        using var check = ctx.NewContext();
        var stored = await check.CustomFieldValues.SingleAsync(v => v.EntityId == "p1");
        Assert.Equal("gleich", stored.Value);
    }

    [Fact]
    public async Task SetAsync_Throws_WhenMandatoryFieldMissing_AndPersistsNothing()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Pflichtfeld", mandatory: true));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        // mandatory field absent from the dictionary -> validation fails before any write
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetAsync("Person", "p1", new Dictionary<string, string?>(), Actor()));

        using var check = ctx.NewContext();
        Assert.False(await check.CustomFieldValues.AnyAsync(v => v.EntityId == "p1"));
    }

    [Fact]
    public async Task SetAsync_Throws_WhenMandatoryFieldWhitespace()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Pflichtfeld", mandatory: true));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        // whitespace counts as empty for the mandatory check
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetAsync("Person", "p1",
                new Dictionary<string, string?> { ["d1"] = "   " }, Actor()));
    }

    [Fact]
    public async Task SetAsync_Succeeds_WhenMandatoryFieldProvided()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.CustomFieldDefinitions.Add(Def("d1", name: "Pflichtfeld", mandatory: true));
            db.SaveChanges();
        }
        var svc = Build(ctx);

        await svc.SetAsync("Person", "p1",
            new Dictionary<string, string?> { ["d1"] = "erfuellt" }, Actor());

        using var check = ctx.NewContext();
        var stored = await check.CustomFieldValues.SingleAsync(v => v.EntityId == "p1");
        Assert.Equal("erfuellt", stored.Value);
    }
}
