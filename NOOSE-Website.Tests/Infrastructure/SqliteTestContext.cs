using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOOSE_Website.Data;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>Owns one open in-memory SQLite connection and hands out contexts against it.</summary>
public sealed class SqliteTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.AmbientTransactionWarning))
            .Options;

        using var db = new AppDbContext(Options);
        db.Database.EnsureCreated();

        Factory = new TestDbContextFactory(Options);
    }

    public DbContextOptions<AppDbContext> Options { get; }

    /// <summary>Factory mirroring the production `IDbContextFactory` injection pattern.</summary>
    public TestDbContextFactory Factory { get; }

    /// <summary>Short-lived context on the shared in-memory database.</summary>
    public AppDbContext NewContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>Test double for the injected `IDbContextFactory&lt;AppDbContext&gt;`.</summary>
public sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}
