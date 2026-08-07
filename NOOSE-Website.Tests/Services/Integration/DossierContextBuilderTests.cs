using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The dossier that leaves the building. Everything here is about what must NOT be in it.</summary>
public sealed class DossierContextBuilderTests
{
    private const string Faction = "f1";
    private const string TruMember = "p-tru";
    private const string LeadershipMember = "p-lead";
    private const string OpenMember = "p-open";

    private static ViewerScope Scope(bool classifiedRead = false, bool tru = false, bool hrb = false,
        bool allTaskforces = false, string? meId = null)
        => new(classifiedRead, allTaskforces, meId, null, IsTru: tru, IsHrb: hrb);

    /// <summary>A faction with three members at three different secrecy levels.</summary>
    private static void SeedFaction(SqliteTestContext ctx, Action<Faction>? configure = null)
    {
        using var db = ctx.NewContext();
        db.Factions.Add(Seed.Faction(id: Faction, name: "Ballas", configure: configure));
        db.People.Add(Seed.Person(id: OpenMember, name: "Otto Offen"));
        db.People.Add(Seed.Person(id: TruMember, name: "Tim Tru", configure: p =>
        {
            p.IsClassified = true;
            p.IsTRUClassified = true;
        }));
        db.People.Add(Seed.Person(id: LeadershipMember, name: "Lena Leitung", configure: p => p.IsClassified = true));
        foreach (var personId in new[] { OpenMember, TruMember, LeadershipMember })
        {
            db.FactionMembers.Add(new FactionMember { FactionId = Faction, PersonId = personId, Rank = "Soldat" });
        }
        db.SaveChanges();
    }

    private static async Task<string> BuildAsync(SqliteTestContext ctx, ViewerScope? scope)
    {
        await using var db = ctx.NewContext();
        var context = await DossierContextBuilder.BuildAsync(db, nameof(Data.Entities.Factions.Faction), Faction, scope);
        Assert.NotNull(context);
        return context!.Value.Text;
    }

    // ---- member masking is per viewer, not per root record ----

    [Fact]
    public async Task TruRoot_DoesNotUnmaskALeadershipMember()
    {
        using var ctx = new SqliteTestContext();
        SeedFaction(ctx, f =>
        {
            f.IsClassified = true;
            f.IsTRUClassified = true;
        });

        // the canonical scope of a TRU record: TRU members are in its audience, leadership-only members are not
        var text = await BuildAsync(ctx, null);

        Assert.Contains("Tim Tru", text);
        Assert.DoesNotContain("Lena Leitung", text);
        Assert.Contains("(Verschlusssache)", text);
    }

    [Fact]
    public async Task OpenRoot_MasksEveryClassifiedMember()
    {
        using var ctx = new SqliteTestContext();
        SeedFaction(ctx);

        var text = await BuildAsync(ctx, null);

        Assert.Contains("Otto Offen", text);
        Assert.DoesNotContain("Tim Tru", text);
        Assert.DoesNotContain("Lena Leitung", text);
    }

    [Fact]
    public async Task LeadershipScope_SeesEveryMember()
    {
        using var ctx = new SqliteTestContext();
        SeedFaction(ctx);

        var text = await BuildAsync(ctx, Scope(classifiedRead: true));

        Assert.Contains("Otto Offen", text);
        Assert.Contains("Tim Tru", text);
        Assert.Contains("Lena Leitung", text);
    }

    [Fact]
    public async Task TruAgentScope_SeesTruButNotLeadership()
    {
        using var ctx = new SqliteTestContext();
        SeedFaction(ctx);

        var text = await BuildAsync(ctx, Scope(tru: true));

        Assert.Contains("Tim Tru", text);
        Assert.DoesNotContain("Lena Leitung", text);
    }

    // ---- linked taskforces gate on membership ----

    [Fact]
    public async Task LinkedTaskforce_StaysHidden_ForANonMember()
    {
        using var ctx = new SqliteTestContext();
        SeedFaction(ctx);
        using (var db = ctx.NewContext())
        {
            db.Taskforces.Add(new Taskforce
            {
                Id = "tf1",
                Name = "Operation Nachtfalke",
                CaseNumber = "NOOSE-TF-2026-0001",
                Scope = TaskforceScope.InternalAgency,
                Status = TaskforceStatus.Approved,
            });
            db.Links.Add(new Link
            {
                SourceType = nameof(Data.Entities.Factions.Faction),
                SourceId = Faction,
                TargetType = nameof(Taskforce),
                TargetId = "tf1",
                Label = "Ermittelt gegen",
            });
            db.SaveChanges();
        }

        var hidden = await BuildAsync(ctx, Scope());
        var visible = await BuildAsync(ctx, Scope(allTaskforces: true));

        Assert.DoesNotContain("Operation Nachtfalke", hidden);
        Assert.DoesNotContain("NOOSE-TF-2026-0001", hidden);
        Assert.Contains("Operation Nachtfalke", visible);
    }

    [Fact]
    public async Task LinkedTaskforce_StaysHidden_InACachedBrief()
    {
        using var ctx = new SqliteTestContext();
        SeedFaction(ctx);
        using (var db = ctx.NewContext())
        {
            db.Taskforces.Add(new Taskforce
            {
                Id = "tf1",
                Name = "Operation Nachtfalke",
                CaseNumber = "NOOSE-TF-2026-0001",
                Scope = TaskforceScope.InternalAgency,
                Status = TaskforceStatus.Approved,
            });
            db.Links.Add(new Link
            {
                SourceType = nameof(Data.Entities.Factions.Faction),
                SourceId = Faction,
                TargetType = nameof(Taskforce),
                TargetId = "tf1",
            });
            db.SaveChanges();
        }

        // the cached brief is shared by every reader, so it must never carry a membership-gated record
        var text = await BuildAsync(ctx, null);

        Assert.DoesNotContain("Operation Nachtfalke", text);
    }

    // ---- a person's own affiliations ----

    /// <summary>One person in an open and in a classified faction.</summary>
    private static void SeedPersonWithAffiliations(SqliteTestContext ctx)
    {
        using var db = ctx.NewContext();
        db.People.Add(Seed.Person(id: "p1", name: "Max Mustermann"));
        db.Factions.Add(Seed.Faction(id: "f-open", name: "Ballas"));
        db.Factions.Add(Seed.Faction(id: "f-secret", name: "Geheimbund", configure: f => f.IsClassified = true));
        db.FactionMembers.Add(new FactionMember { FactionId = "f-open", PersonId = "p1", Rank = "Enforcer", IsLead = true });
        db.FactionMembers.Add(new FactionMember { FactionId = "f-secret", PersonId = "p1", Rank = "Kurier" });
        db.SaveChanges();
    }

    private static async Task<string> BuildPersonAsync(SqliteTestContext ctx, ViewerScope? scope)
    {
        await using var db = ctx.NewContext();
        var context = await DossierContextBuilder.BuildAsync(db, nameof(Person), "p1", scope);
        Assert.NotNull(context);
        return context!.Value.Text;
    }

    [Fact]
    public async Task PersonDossier_NamesTheOrganisationsThePersonBelongsTo()
    {
        using var ctx = new SqliteTestContext();
        SeedPersonWithAffiliations(ctx);

        var text = await BuildPersonAsync(ctx, Scope());

        // the roster was readable from the faction only; the person's file never named their own faction
        Assert.Contains("Zugehörigkeiten", text);
        Assert.Contains("Ballas", text);
        Assert.Contains("Enforcer", text);
        Assert.Contains("Leitung", text);
    }

    [Fact]
    public async Task PersonDossier_MasksAClassifiedOrganisation()
    {
        using var ctx = new SqliteTestContext();
        SeedPersonWithAffiliations(ctx);

        var masked = await BuildPersonAsync(ctx, Scope());
        var full = await BuildPersonAsync(ctx, Scope(classifiedRead: true));

        Assert.DoesNotContain("Geheimbund", masked);
        Assert.Contains("(Verschlusssache)", masked);
        Assert.Contains("Geheimbund", full);
    }

    // ---- the model reads German, so the context must be German ----

    [Fact]
    public async Task PersonDossier_NamesClassificationAndRelationsInGerman()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Max Mustermann",
                configure: p => p.Classification = Classification.SuspicionCase));
            db.People.Add(Seed.Person(id: "p2", name: "Erik Gegner"));
            db.PersonRelations.Add(new PersonRelation { PersonAId = "p1", PersonBId = "p2", Type = RelationType.Enemy });
            db.SaveChanges();
        }

        var text = await BuildPersonAsync(ctx, Scope());

        // the brief prompt tells the model to copy names verbatim, so a raw enum identifier ends up in the answer
        Assert.Contains("Verdachtsfall", text);
        Assert.Contains("Feind", text);
        Assert.DoesNotContain("SuspicionCase", text);
        Assert.DoesNotContain("Enemy", text);
    }

    // ---- mention tokens never leave the building ----

    [Fact]
    public async Task MentionTokens_AreStrippedFromFreeText()
    {
        var secret = Guid.NewGuid().ToString();
        using var ctx = new SqliteTestContext();
        SeedFaction(ctx);
        using (var db = ctx.NewContext())
        {
            var faction = db.Factions.First(f => f.Id == Faction);
            faction.Description = $"Kontakt zu {MentionParser.Token(nameof(Person), secret)} bestätigt.";
            db.Comments.Add(new Comment
            {
                EntityType = nameof(Data.Entities.Factions.Faction),
                EntityId = Faction,
                AuthorName = "Codename-x",
                Text = $"Siehe {MentionParser.Token(nameof(Person), secret)}",
            });
            db.SaveChanges();
        }

        var text = await BuildAsync(ctx, Scope(classifiedRead: true));

        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain("@{", text);
        Assert.Contains("Kontakt zu", text);
        Assert.Contains("bestätigt.", text);
    }
}
