using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>Proves the public-area queries translate against the production provider, not just against SQLite.</summary>
/// <remarks>
/// Every other integration test runs on SQLite, which is a different translator: a LINQ shape SQLite accepts can
/// still throw "could not be translated" on Pomelo, and it would do so at runtime, on a page an anonymous visitor
/// opened. <c>ToQueryString()</c> compiles the query without touching a database, so this needs no MySQL server —
/// only the provider. Deliberately not a general model test: it holds the shapes phase 5 introduced.
/// </remarks>
public sealed class MySqlTranslationTests : IDisposable
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql("server=localhost;database=none", ServerVersion.Parse("8.0.0-mysql"))
        .Options);

    public void Dispose() => _db.Dispose();

    /// <summary>Mirrors the status set PublicWantedService uses for the retraction hook.</summary>
    private static readonly PublicWantedStatus[] PubliclyVisible =
    [
        PublicWantedStatus.Veroeffentlicht,
        PublicWantedStatus.Beantragt,
        PublicWantedStatus.Gefasst,
    ];

    [Fact]
    public void TheRetractionHooksStatusSet_TranslatesToAnInClause()
    {
        // a static array in a Where is the one shape the retraction hook depends on, and it is the hook that keeps a
        // classified record off /gefasst — an untranslatable query here would throw exactly when it matters
        var sql = _db.OeffentlicheFahndungen
            .Where(f => f.PersonId == "p1" && PubliclyVisible.Contains(f.Status))
            .ToQueryString();

        Assert.Contains("IN (", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheArchiveQuery_Translates()
    {
        var sql = _db.OeffentlicheFahndungen
            .AsNoTracking()
            .Where(f => f.Status == PublicWantedStatus.Gefasst && f.CaseNumber != null && f.CapturedAt != null)
            .OrderByDescending(f => f.CapturedAt)
            .Take(100)
            .Select(f => new { f.PersonId, f.CaseNumber, f.Kind, f.DisplayName, HasPhoto = f.PhotoFileName != null, f.CapturedAt })
            .ToQueryString();

        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHintLookup_TranslatesIncludingItsOrderingOverTheNavigation()
    {
        // ordering by a navigation property is the part most likely to fall over; it decides the chip order outside
        List<string> ids = ["f1", "f2"];
        var sql = _db.FahndungWarnhinweise
            .AsNoTracking()
            .Where(z => ids.Contains(z.FahndungId) && z.Warnhinweis!.IsActive)
            .OrderBy(z => z.Warnhinweis!.SortOrder).ThenBy(z => z.Warnhinweis!.Name)
            .Select(z => new { z.FahndungId, Label = z.Warnhinweis!.Name, z.Warnhinweis.Colour })
            .ToQueryString();

        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheAssignmentNarrowing_TranslatesWithTwoContains()
    {
        List<string> requested = ["w1"];
        HashSet<string> existing = ["w2"];
        var sql = _db.Warnhinweise
            .Where(w => requested.Contains(w.Id) && (w.IsActive || existing.Contains(w.Id)))
            .Select(w => w.Id)
            .ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUsageCount_Translates()
    {
        var sql = _db.FahndungWarnhinweise
            .GroupBy(z => z.WarnhinweisId)
            .Select(g => new { WarnhinweisId = g.Key, Count = g.Count() })
            .ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSuppressionBelt_TranslatesWithIgnoreQueryFilters()
    {
        List<string> ids = ["p1"];
        var sql = _db.People
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && !p.IsDeleted
                && !p.IsClassified && !p.IsTRUClassified && !p.IsHRBClassified)
            .Select(p => p.Id)
            .ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExpirySweep_Translates()
    {
        var now = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        var sql = _db.OeffentlicheFahndungen
            .Where(f => f.Status == PublicWantedStatus.Veroeffentlicht && f.ExpiresAt != null && f.ExpiresAt <= now)
            .OrderBy(f => f.ExpiresAt)
            .Take(200)
            .ToQueryString();

        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheViewCounterPredicate_Translates()
    {
        // ExecuteUpdate itself cannot be compiled without a connection; its Where clause is the part that could fail
        var now = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        var sql = _db.OeffentlicheFahndungen
            .Where(f => f.CaseNumber == "NOOSE-FA-2026-0001"
                && f.Status == PublicWantedStatus.Veroeffentlicht
                && (f.ExpiresAt == null || f.ExpiresAt > now))
            .ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
    }
}
