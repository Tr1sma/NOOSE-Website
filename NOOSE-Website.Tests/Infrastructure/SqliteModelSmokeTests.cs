using NOOSE_Website.Tests.Infrastructure;
using Xunit;

namespace NOOSE_Website.Tests.Infrastructure;

public class SqliteModelSmokeTests
{
    [Fact]
    public void Model_builds_and_creates_schema_under_sqlite()
    {
        using var ctx = new SqliteTestContext();
        using var db = ctx.NewContext();

        // If OnModelCreating had a SQLite-incompatible config, EnsureCreated in the
        // fixture ctor would have thrown before reaching here.
        Assert.True(db.Model.GetEntityTypes().Any());
    }
}
