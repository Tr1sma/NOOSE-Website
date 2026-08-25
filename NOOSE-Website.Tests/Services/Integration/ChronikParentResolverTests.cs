using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The chronicle mirrors the timeline fan-out; a child that surfaces there has to resolve to its record here.</summary>
public sealed class ChronikParentResolverTests
{
    [Fact]
    public void PublicWanted_CountsAsAChildOfARecord()
    {
        Assert.True(ChronikParentResolver.IsChild(nameof(OeffentlicheFahndung)));
        Assert.Contains(nameof(OeffentlicheFahndung),
            ChronikParentResolver.AuditTypesFor([nameof(Person)]));
    }

    [Fact]
    public async Task PublicWanted_ResolvesToItsPersonFile()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1"));
            db.OeffentlicheFahndungen.Add(new OeffentlicheFahndung
            {
                Id = "f1", PersonId = "p1", DisplayName = "Max Mustermann",
                Status = PublicWantedStatus.Veroeffentlicht,
            });
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        var map = await ChronikParentResolver.ResolveAsync(read, [(nameof(OeffentlicheFahndung), "f1")]);

        var parent = Assert.Contains((nameof(OeffentlicheFahndung), "f1"), map);
        Assert.Equal(nameof(Person), parent.Type);
        Assert.Equal("p1", parent.Id);
    }

    [Fact]
    public async Task PublicWanted_WithoutAPersonFile_ResolvesToNothing()
    {
        // faction notices arrive in a later phase; PersonId is nullable and ParentRef.Id is not
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.OeffentlicheFahndungen.Add(new OeffentlicheFahndung { Id = "f1", DisplayName = "Ohne Akte" });
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        var map = await ChronikParentResolver.ResolveAsync(read, [(nameof(OeffentlicheFahndung), "f1")]);

        Assert.Empty(map);
    }
}
