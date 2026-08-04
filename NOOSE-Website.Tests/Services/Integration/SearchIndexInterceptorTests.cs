using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Search;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="SearchIndexInterceptor"/> (rebuilds the phonetic/stem side-index).</summary>
public sealed class SearchIndexInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SearchIndexInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SearchIndexInterceptor())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.AmbientTransactionWarning))
            .Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    private AppDbContext New() => new(_options);
    public void Dispose() => _connection.Dispose();

    [Fact]
    public void SavingPerson_CreatesPhoneticAndStemRows_KeyedToTheRecord()
    {
        using (var db = New())
        {
            db.People.Add(Seed.Person("p1", "Maier"));
            db.SaveChanges();
        }

        using var check = New();
        var keys = check.SearchPhoneticKeys.Where(k => k.EntityId == "p1").ToList();
        Assert.Contains(keys, k => k.Key == ColognePhonetic.Encode("Maier"));
        Assert.All(keys, k => Assert.Equal("p1", k.SourceId));
        Assert.True(check.SearchStemTokens.Any(s => s.EntityId == "p1"));
    }

    [Fact]
    public void RenamingPerson_ReplacesIndexRows()
    {
        using (var db = New())
        {
            db.People.Add(Seed.Person("p1", "Maier"));
            db.SaveChanges();
        }
        using (var db = New())
        {
            var p = db.People.Single(x => x.Id == "p1");
            p.Name = "Schmidt";
            db.SaveChanges();
        }

        using var check = New();
        var keys = check.SearchPhoneticKeys.Where(k => k.EntityId == "p1").Select(k => k.Key).ToList();
        Assert.Contains(ColognePhonetic.Encode("Schmidt"), keys);
        Assert.DoesNotContain(ColognePhonetic.Encode("Maier"), keys);
    }

    [Fact]
    public void Alias_IndexedUnderPerson_RemovingAliasKeepsPersonRows()
    {
        using (var db = New())
        {
            db.People.Add(Seed.Person("p1", "Maier"));
            db.SaveChanges();
        }
        using (var db = New())
        {
            db.PersonAliases.Add(new PersonAlias { Id = "a1", PersonId = "p1", AliasName = "Petrow" });
            db.SaveChanges();
        }

        using (var check = New())
        {
            var byPerson = check.SearchPhoneticKeys.Where(k => k.EntityId == "p1").ToList();
            Assert.Contains(byPerson, k => k.SourceId == "p1" && k.Key == ColognePhonetic.Encode("Maier"));
            Assert.Contains(byPerson, k => k.SourceId == "a1" && k.Key == ColognePhonetic.Encode("Petrow"));
        }

        using (var db = New())
        {
            db.PersonAliases.Remove(db.PersonAliases.Single(x => x.Id == "a1"));
            db.SaveChanges();
        }

        using var after = New();
        Assert.DoesNotContain(after.SearchPhoneticKeys, k => k.SourceId == "a1");
        Assert.Contains(after.SearchPhoneticKeys, k => k.SourceId == "p1"); // the person's own name row survives
    }
}
