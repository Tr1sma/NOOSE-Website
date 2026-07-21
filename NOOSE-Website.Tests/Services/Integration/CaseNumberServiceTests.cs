using System;
using System.Threading.Tasks;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

public sealed class CaseNumberServiceTests : IDisposable
{
    private readonly SqliteTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    // Happy path (ON DUPLICATE KEY UPDATE increment + format) is SQLite-incompatible: MySQL raw SQL. Only the transaction guard is testable here.

    [Fact]
    public async Task NextAsync_WithoutEnclosingTransaction_ThrowsInvalidOperationException()
    {
        var service = new CaseNumberService();
        await using var db = _ctx.NewContext();

        // No transaction opened on the context -> guard must fail fast.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.NextAsync(db, "P"));
    }

    [Fact]
    public async Task NextAsync_WithoutEnclosingTransaction_MessageMentionsTransaction()
    {
        var service = new CaseNumberService();
        await using var db = _ctx.NewContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.NextAsync(db, "F"));

        Assert.Contains("Transaktion", ex.Message);
    }
}
