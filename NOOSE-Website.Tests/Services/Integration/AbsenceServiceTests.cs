using System.Security.Claims;
using NOOSE_Website.Data.Entities.Absences;
using NOOSE_Website.Models.Absences;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="AbsenceService"/> over in-memory SQLite.</summary>
public class AbsenceServiceTests
{
    // The soft-delete rewrite interceptor is DI-only and absent here, so DeleteAsync HARD-deletes.

    private static (AbsenceService Svc, INotificationService Notifications) Build(SqliteTestContext ctx)
    {
        var notifications = Substitute.For<INotificationService>();
        return (new AbsenceService(ctx.Factory, notifications), notifications);
    }

    private static DateOnly Today => DateOnly.FromDateTime(MeetingTime.Local(DateTime.UtcNow));

    private static DateTime AsInput(DateOnly day) => day.ToDateTime(TimeOnly.MinValue);

    private static Absence SeedAbsence(SqliteTestContext ctx, string agentId, DateOnly from, DateOnly to,
        Action<Absence>? configure = null)
    {
        var abs = new Absence
        {
            AgentId = agentId,
            FromDate = from,
            ToDate = to,
            Days = to.DayNumber - from.DayNumber + 1,
            Category = AbsenceCategory.Vacation,
            Reason = "geheim",
        };
        configure?.Invoke(abs);

        using var db = ctx.NewContext();
        if (!db.Users.Any(u => u.Id == agentId))
        {
            db.Users.Add(Seed.Agent(agentId));
        }
        db.Absences.Add(abs);
        db.SaveChanges();
        return abs;
    }

    private static ClaimsPrincipal Owner(string id) => ClaimsPrincipalBuilder.Agent(id).Build();

    private static ClaimsPrincipal Leader(string id = "lead", string codename = "Chief")
        => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.SupervisorySpecialAgent).WithCodename(codename).Build();

    private static ClaimsPrincipal OnlyReader(string id = "tl")
        => ClaimsPrincipalBuilder.Agent(id).AsTeamLead().Build();

    // ---------- GetListAsync ----------

    [Fact]
    public async Task GetListAsync_NotMayAll_ReturnsOnlyOwn()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(3));
        SeedAbsence(ctx, "a2", Today.AddDays(1), Today.AddDays(3));

        var rows = await svc.GetListAsync(mayAll: false, meId: "a1");

        var row = Assert.Single(rows);
        Assert.Equal("a1", row.AgentId);
        Assert.Equal("Codename-a1", row.Codename);
        Assert.Equal("geheim", row.Reason); // own reason is visible
    }

    [Fact]
    public async Task GetListAsync_MayAll_ReturnsAll_NewestFirst()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(2));   // later FromDate
        SeedAbsence(ctx, "a2", Today.AddDays(-10), Today.AddDays(-8)); // earlier FromDate

        var rows = await svc.GetListAsync(mayAll: true, meId: "someoneElse");

        Assert.Equal(2, rows.Count);
        // ordered by FromDate descending
        Assert.Equal("a1", rows[0].AgentId);
        Assert.Equal("a2", rows[1].AgentId);
        // mayAll => reason visible even for others, and MayEdit is always true
        Assert.All(rows, r => Assert.Equal("geheim", r.Reason));
        Assert.All(rows, r => Assert.True(r.MayEdit));
    }

    [Fact]
    public async Task GetListAsync_DateWindow_FiltersByOverlap()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var before = SeedAbsence(ctx, "a1", Today.AddDays(-20), Today.AddDays(-15));
        var overlap = SeedAbsence(ctx, "a1", Today.AddDays(-2), Today.AddDays(2));
        var after = SeedAbsence(ctx, "a1", Today.AddDays(15), Today.AddDays(20));

        var rows = await svc.GetListAsync(mayAll: false, meId: "a1", from: Today.AddDays(-5), to: Today.AddDays(5));

        var row = Assert.Single(rows);
        Assert.Equal(overlap.Id, row.Id);
        Assert.DoesNotContain(rows, r => r.Id == before.Id);
        Assert.DoesNotContain(rows, r => r.Id == after.Id);
    }

    [Fact]
    public async Task GetListAsync_MayEdit_TrueForFutureOwn_FalseForPastOwn()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var past = SeedAbsence(ctx, "a1", Today.AddDays(-10), Today.AddDays(-5));
        var future = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5));

        var rows = await svc.GetListAsync(mayAll: false, meId: "a1");

        Assert.False(rows.Single(r => r.Id == past.Id).MayEdit);
        Assert.True(rows.Single(r => r.Id == future.Id).MayEdit);
    }

    // ---------- GetDetailAsync ----------

    [Fact]
    public async Task GetDetailAsync_Own_ReturnsEntity()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today, Today.AddDays(2));

        var result = await svc.GetDetailAsync(abs.Id, mayAll: false, meId: "a1");

        Assert.NotNull(result);
        Assert.Equal(abs.Id, result!.Id);
    }

    [Fact]
    public async Task GetDetailAsync_NotVisibleToOther_ReturnsNull_ButMayAllSees()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today, Today.AddDays(2));

        Assert.Null(await svc.GetDetailAsync(abs.Id, mayAll: false, meId: "a2"));
        Assert.NotNull(await svc.GetDetailAsync(abs.Id, mayAll: true, meId: "a2"));
    }

    // ---------- GetTrashAsync ----------

    [Fact]
    public async Task GetTrashAsync_ReturnsOnlyDeleted_WithAgent()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var deleted = SeedAbsence(ctx, "a1", Today, Today.AddDays(1),
            a => { a.IsDeleted = true; a.DeletedAt = DateTime.UtcNow; });
        SeedAbsence(ctx, "a2", Today, Today.AddDays(1)); // live, excluded

        var trash = await svc.GetTrashAsync();

        var row = Assert.Single(trash);
        Assert.Equal(deleted.Id, row.Id);
        Assert.NotNull(row.Agent);
        Assert.Equal("Codename-a1", row.Agent!.Codename);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_PersistsAbsence_WithComputedDays()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var input = new AbsenceInput
        {
            From = AsInput(Today.AddDays(1)),
            To = AsInput(Today.AddDays(4)),
            Category = AbsenceCategory.Sick,
            Reason = "  krank  ",
        };

        var created = await svc.CreateAsync(input, Owner("a1"));

        using var db = ctx.NewContext();
        var row = Assert.Single(db.Absences.ToList());
        Assert.Equal(created.Id, row.Id);
        Assert.Equal("a1", row.AgentId);
        Assert.Equal(Today.AddDays(1), row.FromDate);
        Assert.Equal(Today.AddDays(4), row.ToDate);
        Assert.Equal(4, row.Days); // inclusive span
        Assert.Equal(AbsenceCategory.Sick, row.Category);
        Assert.Equal("krank", row.Reason); // trimmed
    }

    [Fact]
    public async Task CreateAsync_OnlyReader_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var input = new AbsenceInput { From = AsInput(Today), To = AsInput(Today.AddDays(1)) };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateAsync(input, OnlyReader()));

        using var db = ctx.NewContext();
        Assert.Empty(db.Absences.ToList());
    }

    [Fact]
    public async Task CreateAsync_NoAgentContext_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        // Anonymous passes the write-access guard but has no agent id.
        var input = new AbsenceInput { From = AsInput(Today), To = AsInput(Today.AddDays(1)) };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateAsync(input, ClaimsPrincipalBuilder.Anonymous()));
    }

    [Fact]
    public async Task CreateAsync_ToBeforeFrom_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var input = new AbsenceInput { From = AsInput(Today.AddDays(5)), To = AsInput(Today.AddDays(1)) };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateAsync(input, Owner("a1")));
    }

    // ---------- RefreshAsync ----------

    [Fact]
    public async Task RefreshAsync_Owner_UpdatesAndResetsAcknowledgement()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5), a =>
        {
            a.AcknowledgedAt = DateTime.UtcNow;
            a.AcknowledgedById = "someLead";
            a.AcknowledgedByName = "Lead";
            a.Reason = "old";
        });

        var input = new AbsenceInput
        {
            From = AsInput(Today.AddDays(2)),
            To = AsInput(Today.AddDays(10)),
            Category = AbsenceCategory.RpBreak,
            Reason = "  neu  ",
        };

        await svc.RefreshAsync(abs.Id, input, Owner("a1"));

        using var db = ctx.NewContext();
        var row = db.Absences.Single(a => a.Id == abs.Id);
        Assert.Equal(Today.AddDays(2), row.FromDate);
        Assert.Equal(Today.AddDays(10), row.ToDate);
        Assert.Equal(9, row.Days);
        Assert.Equal(AbsenceCategory.RpBreak, row.Category);
        Assert.Equal("neu", row.Reason);
        Assert.Null(row.AcknowledgedAt);
        Assert.Null(row.AcknowledgedById);
        Assert.Null(row.AcknowledgedByName);
    }

    [Fact]
    public async Task RefreshAsync_OnlyReader_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5));
        var input = new AbsenceInput { From = AsInput(Today.AddDays(1)), To = AsInput(Today.AddDays(5)) };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.RefreshAsync(abs.Id, input, OnlyReader()));
    }

    [Fact]
    public async Task RefreshAsync_NonLeadershipNonOwner_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5));
        var input = new AbsenceInput { From = AsInput(Today.AddDays(1)), To = AsInput(Today.AddDays(5)) };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.RefreshAsync(abs.Id, input, Owner("a2")));
    }

    [Fact]
    public async Task RefreshAsync_NotFound_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var input = new AbsenceInput { From = AsInput(Today.AddDays(1)), To = AsInput(Today.AddDays(5)) };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefreshAsync("missing", input, Owner("a1")));
    }

    // ---------- DeleteAsync ----------

    [Fact]
    public async Task DeleteAsync_Owner_HardDeletesRow()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5));

        await svc.DeleteAsync(abs.Id, Owner("a1"));

        using var db = ctx.NewContext();
        Assert.False(db.Absences.Any(a => a.Id == abs.Id));
        // no soft-delete interceptor in tests => physically gone, not just flagged
        Assert.False(db.Absences.IgnoreQueryFilters().Any(a => a.Id == abs.Id));
    }

    [Fact]
    public async Task DeleteAsync_OnlyReader_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DeleteAsync(abs.Id, OnlyReader()));

        using var db = ctx.NewContext();
        Assert.True(db.Absences.Any(a => a.Id == abs.Id));
    }

    // ---------- RestoreAsync ----------

    [Fact]
    public async Task RestoreAsync_Leadership_UndeletesRow()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today, Today.AddDays(2),
            a => { a.IsDeleted = true; a.DeletedAt = DateTime.UtcNow; a.DeletedById = "x"; });

        await svc.RestoreAsync(abs.Id, Leader());

        using var db = ctx.NewContext();
        var row = db.Absences.Single(a => a.Id == abs.Id); // visible again in the filtered set
        Assert.False(row.IsDeleted);
        Assert.Null(row.DeletedAt);
        Assert.Null(row.DeletedById);
    }

    [Fact]
    public async Task RestoreAsync_NonLeadership_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today, Today.AddDays(2),
            a => { a.IsDeleted = true; a.DeletedAt = DateTime.UtcNow; });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.RestoreAsync(abs.Id, Owner("a1")));

        using var db = ctx.NewContext();
        Assert.True(db.Absences.IgnoreQueryFilters().Single(a => a.Id == abs.Id).IsDeleted);
    }

    // ---------- AcknowledgeAsync ----------

    [Fact]
    public async Task AcknowledgeAsync_Leadership_SetsAcknowledgement()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5));

        await svc.AcknowledgeAsync(abs.Id, Leader("lead", "Chief"));

        using var db = ctx.NewContext();
        var row = db.Absences.Single(a => a.Id == abs.Id);
        Assert.NotNull(row.AcknowledgedAt);
        Assert.Equal("lead", row.AcknowledgedById);
        Assert.Equal("Chief", row.AcknowledgedByName);
    }

    [Fact]
    public async Task AcknowledgeAsync_AlreadyAcknowledged_Idempotent()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var when = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5), a =>
        {
            a.AcknowledgedAt = when;
            a.AcknowledgedById = "firstLead";
            a.AcknowledgedByName = "First";
        });

        await svc.AcknowledgeAsync(abs.Id, Leader("secondLead", "Second"));

        using var db = ctx.NewContext();
        var row = db.Absences.Single(a => a.Id == abs.Id);
        Assert.Equal(when, row.AcknowledgedAt);
        Assert.Equal("firstLead", row.AcknowledgedById); // unchanged
        Assert.Equal("First", row.AcknowledgedByName);
    }

    [Fact]
    public async Task AcknowledgeAsync_NonLeadership_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var abs = SeedAbsence(ctx, "a1", Today.AddDays(1), Today.AddDays(5));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.AcknowledgeAsync(abs.Id, Owner("a1")));

        using var db = ctx.NewContext();
        Assert.Null(db.Absences.Single(a => a.Id == abs.Id).AcknowledgedAt);
    }
}
