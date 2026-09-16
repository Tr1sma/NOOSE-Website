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
