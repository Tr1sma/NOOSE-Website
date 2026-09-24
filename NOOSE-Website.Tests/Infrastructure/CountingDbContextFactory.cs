using NOOSE_Website.Data;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>Context factory over a test database that counts every successful SaveChanges.</summary>
public sealed class CountingDbContextFactory(SqliteTestContext ctx) : IDbContextFactory<AppDbContext>
{
    public int Saves { get; private set; }

    public AppDbContext CreateDbContext()
    {
        var db = ctx.NewContext();
        db.SavedChanges += (_, _) => Saves++;
        return db;
    }
}
