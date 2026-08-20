using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

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
    public void TheBountySum_TranslatesToAGroupedSum()
    {
        // this is the number an anonymous visitor reads; "could not be translated" here is an HTTP 500 on /gesucht
        List<string> ids = ["f1", "f2"];
        var sql = _db.FahndungKopfgeldAnteile
            .AsNoTracking()
            .Where(k => ids.Contains(k.WantedId)
                && (k.Status == BountyShareStatus.Zugesagt || k.Status == BountyShareStatus.Gesichert))
            .GroupBy(k => k.WantedId)
            .Select(g => new { WantedId = g.Key, Total = g.Sum(k => k.Amount) })
            .ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUM(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheCoverageQuery_TranslatesGroupingOverANullableEnum()
    {
        // grouping by Account!.Value is the shape most likely to fall over: a nullable enum key on Pomelo
        var sql = _db.FahndungKopfgeldAnteile
            .AsNoTracking()
            .Where(k => k.Account != null
                && ((k.Origin == BountyOrigin.NooseKasse && k.Status == BountyShareStatus.Zugesagt)
                    || (k.Origin == BountyOrigin.AgentPrivat && k.Status == BountyShareStatus.Gesichert)))
            .GroupBy(k => k.Account!.Value)
            .Select(g => new { Account = g.Key, Total = g.Sum(k => k.Amount) })
            .ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePendingBountyRequestQuery_TranslatesItsCorrelatedExists()
    {
        var sql = _db.Requests
            .Where(a => a.Type == RequestType.Kopfgeld
                && a.Status == RequestStatus.Requested
                && _db.FahndungKopfgeldAnteile.Any(k => k.Id == a.BountyShareId
                    && k.Status == BountyShareStatus.Beantragt))
            .ToQueryString();

        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheBountyRequestInbox_TranslatesItsJoinAndNavigation()
    {
        var sql = _db.Requests
            .Where(a => a.Type == RequestType.Kopfgeld && a.Status == RequestStatus.Requested)
            .OrderBy(a => a.CreatedAt)
            .Join(_db.FahndungKopfgeldAnteile, a => a.BountyShareId, k => k.Id, (a, k) => new { Request = a, Share = k })
            .Select(x => new { x.Request.Id, Name = x.Share.Wanted!.DisplayName, x.Share.Amount, x.Share.Account })
            .ToQueryString();

        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheBountyRequestDeduplication_TranslatesItsNestedExists()
    {
        // two correlated subqueries in one predicate; it runs on a write path, so a failure here is a 500 for an agent
        var sql = _db.Requests
            .Where(a => a.Type == RequestType.Kopfgeld && a.Status == RequestStatus.Requested
                && _db.FahndungKopfgeldAnteile.Any(k => k.Id == a.BountyShareId
                    && k.Status == BountyShareStatus.Beantragt
                    && _db.OeffentlicheFahndungen.Any(f => f.Id == k.WantedId)))
            .Where(a => _db.FahndungKopfgeldAnteile.Any(k => k.Id == a.BountyShareId && k.WantedId == "f1"))
            .ToQueryString();

        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void TheTipQuotaCount_TranslatesWithTheSoftDeleteFilterOff()
    {
        // IgnoreQueryFilters is the point: a deleted tip must still count against the quota
        var since = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var unfiltered = _db.Hinweise.IgnoreQueryFilters()
            .Where(h => h.CitizenProfileId == "profil1" && h.CreatedAt >= since)
            .Select(h => h.Id)
            .ToQueryString();
        var filtered = _db.Hinweise
            .Where(h => h.CitizenProfileId == "profil1" && h.CreatedAt >= since)
            .Select(h => h.Id)
            .ToQueryString();

        Assert.DoesNotContain("IstGeloescht", unfiltered, StringComparison.Ordinal);
        Assert.Contains("IstGeloescht", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTipInboxProjection_TranslatesAcrossThreeOptionalNavigations()
    {
        // citizen profile, notice and handler are all optional; the inbox reads a column from each
        var sql = _db.Hinweise.AsNoTracking()
            .Where(h => h.Status == TipStatus.Neu)
            .OrderByDescending(h => h.Priority).ThenByDescending(h => h.CreatedAt)
            .Take(200)
            .Select(h => new
            {
                h.Id,
                h.CaseNumber,
                CitizenFirstName = h.CitizenProfile!.FirstName,
                WantedCaseNumber = h.Wanted!.CaseNumber,
                HandlerCodename = h.Handler!.Codename,
            })
            .ToQueryString();

        Assert.Contains("LEFT JOIN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheUnreadMessageLookup_Translates()
    {
        var ids = new[] { "h1", "h2" };
        var sql = _db.HinweisNachrichten.AsNoTracking()
            .Where(m => ids.Contains(m.HinweisId) && m.Audience == TipMessageAudience.Buerger && !m.AuthorIsCitizen)
            .Select(m => new { m.HinweisId, m.CreatedAt })
            .ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
    }
    [Fact]
    public void TheDuplicateCandidateWindow_TranslatesInBothReferenceBranches()
    {
        // spelled-out branches: comparing the column to a null variable would translate to "= NULL" and find nothing
        var since = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var withReference = _db.Hinweise.AsNoTracking()
            .Where(h => h.Id != "h1" && h.CreatedAt >= since)
            .Where(h => h.WantedId == "f1")
            .OrderByDescending(h => h.CreatedAt)
            .Take(300)
            .Select(h => new { h.Id, h.Text, h.DuplicateGroupId })
            .ToQueryString();
        var withoutReference = _db.Hinweise.AsNoTracking()
            .Where(h => h.Id != "h1" && h.CreatedAt >= since)
            .Where(h => h.WantedId == null)
            .OrderByDescending(h => h.CreatedAt)
            .Take(300)
            .Select(h => new { h.Id, h.Text, h.DuplicateGroupId })
            .ToQueryString();

        Assert.Contains("LIMIT", withReference, StringComparison.Ordinal);
        Assert.Contains("IS NULL", withoutReference, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDuplicateGroupCount_TranslatesToAGroupedCountOverANullableColumn()
    {
        var groups = new[] { "g1", "g2" };
        var sql = _db.Hinweise.AsNoTracking()
            .Where(h => h.DuplicateGroupId != null && groups.Contains(h.DuplicateGroupId))
            .GroupBy(h => h.DuplicateGroupId!)
            .Select(g => new { Group = g.Key, Count = g.Count() })
            .ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheAdvertisedBountyPerNotice_TranslatesToAGroupedSum()
    {
        // the priority stamper reads the same predicate the public snapshot sums with
        var ids = new[] { "f1", "f2" };
        var sql = _db.FahndungKopfgeldAnteile.AsNoTracking()
            .Where(k => ids.Contains(k.WantedId))
            .Where(BountyShares.Advertised)
            .GroupBy(k => k.WantedId)
            .Select(g => new { WantedId = g.Key, Total = g.Sum(k => k.Amount) })
            .ToQueryString();

        Assert.Contains("SUM(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePriorityStampScope_TranslatesTheOpenRowsPredicate()
    {
        var sql = _db.Hinweise.AsNoTracking()
            .Where(TipRules.OpenRows)
            .Where(h => h.WantedId == "f1")
            .Select(h => new { h.Id, h.WantedId, h.CitizenProfileId, h.Priority })
            .ToQueryString();

        Assert.Contains("Status", sql, StringComparison.Ordinal);
        Assert.Contains("IstGeloescht", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTrustCounterRecount_TranslatesItsConfirmedPredicate()
    {
        var sql = _db.Hinweise.AsNoTracking()
            .Where(TipRules.ConfirmedRows)
            .Where(h => h.CitizenProfileId == "profil1")
            .Select(h => h.Id)
            .ToQueryString();

        Assert.Contains("Status", sql, StringComparison.Ordinal);
        Assert.Contains("IstGeloescht", sql, StringComparison.Ordinal);
    }

    // ---- phase 9: the reward ----

    [Fact]
    public void TheRewardRowsOfANotice_TranslateAcrossThreeNavigations()
    {
        // reward → share and reward → tip → citizen in one projection; the deepest shape the phase introduced
        var ids = new[] { "k1", "k2" };
        var sql = _db.HinweisBelohnungen.AsNoTracking()
            .Where(b => ids.Contains(b.ShareId))
            .OrderByDescending(b => b.PaidAt)
            .Select(b => new
            {
                b.ReceiptNumber,
                TipCaseNumber = b.Tip!.CaseNumber,
                b.Tip!.WantsAnonymity,
                FirstName = b.Tip!.CitizenProfile!.FirstName,
                Origin = b.Share!.Origin,
                b.KassenBuchungId,
            })
            .ToQueryString();

        Assert.Contains("Hinweise", sql, StringComparison.Ordinal);
        Assert.Contains("FahndungKopfgeldAnteile", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReceiptLookup_TranslatesTheWholeChainToTheNotice()
    {
        var sql = _db.HinweisBelohnungen.AsNoTracking()
            .Where(b => b.ReceiptNumber == "NOOSE-BEL-2026-0001")
            .Select(b => new
            {
                b.Amount,
                WantedCaseNumber = b.Tip!.Wanted!.CaseNumber,
                CitizenUserId = b.Tip!.CitizenProfile!.UserId,
            })
            .ToQueryString();

        Assert.Contains("BelegNummer", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOwnRewardsOfACitizen_TranslateOverTheTipNavigation()
    {
        var sql = _db.HinweisBelohnungen.AsNoTracking()
            .Where(b => b.Tip!.CitizenProfileId == "profil1")
            .Select(b => new { b.ReceiptNumber, TipCaseNumber = b.Tip!.CaseNumber, b.Amount, b.PaidAt })
            .ToQueryString();

        Assert.Contains("BuergerProfilId", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePayoutCompareAndSwap_TranslatesTheAdvertisedPredicate()
    {
        // the settle step runs as ExecuteUpdate; what has to translate is its filter
        var sql = _db.FahndungKopfgeldAnteile
            .Where(k => k.WantedId == "f1")
            .Where(BountyShares.Advertised)
            .ToQueryString();

        Assert.Contains("Status", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheChronicleFanInOfAReward_TranslatesItsThreeHops()
    {
        // reward → share → notice → person file, the shape ChronikParentResolver compiles
        var ids = new List<string> { "b1" };
        var sql = _db.HinweisBelohnungen.IgnoreQueryFilters()
            .Where(b => ids.Contains(b.Id) && b.Share!.Wanted!.PersonId != null)
            .Select(b => new { b.Id, ParentId = b.Share!.Wanted!.PersonId! })
            .ToQueryString();

        Assert.Contains("PersonId", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTicketDeskProjection_TranslatesItsSearchAndItsTwoNavigations()
    {
        var sql = _db.Tickets.AsNoTracking()
            .Where(TicketRules.ScopeFilter(TicketInboxScope.Offen))
            .Where(t => t.CaseNumber.Contains("T-2026")
                || t.Subject.Contains("T-2026")
                || t.CitizenProfile!.FirstName.Contains("T-2026")
                || t.CitizenProfile!.LastName.Contains("T-2026"))
            .OrderByDescending(t => t.LastActivityAt)
            .Select(t => new
            {
                t.Id,
                FirstName = t.CitizenProfile!.FirstName,
                HandlerCodename = t.Handler!.Codename,
            })
            .ToQueryString();

        Assert.Contains("LetzteAktivitaetAm", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTicketOpenCap_TranslatesTheOpenRowsPredicate()
    {
        // the cap runs the same expression the badge counts with; an untranslatable one would break opening
        var sql = _db.Tickets.AsNoTracking()
            .Where(t => t.CitizenProfileId == "profil1")
            .Where(TicketRules.OpenRows)
            .ToQueryString();

        Assert.Contains("Status", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTicketDailyCap_TranslatesWithTheSoftDeleteFilterOff()
    {
        // IgnoreQueryFilters is what keeps deleting from refilling the quota
        var since = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        // projected to one column, like the Count the service runs: an entity query lists every column, and
        // the soft-delete flag would then show up in the SELECT rather than in the WHERE this asserts about
        var sql = _db.Tickets.IgnoreQueryFilters()
            .Where(t => t.CitizenProfileId == "profil1" && t.CreatedAt >= since)
            .Select(t => t.Id)
            .ToQueryString();

        Assert.DoesNotContain("IstGeloescht", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTicketUnreadLookup_TranslatesItsAudienceAndSideFilter()
    {
        var ids = new List<string> { "t1" };
        var sql = _db.TicketNachrichten.AsNoTracking()
            .Where(m => ids.Contains(m.TicketId) && m.Audience == TicketMessageAudience.Buerger
                && m.AuthorIsCitizen == false)
            .Select(m => new { m.TicketId, m.CreatedAt })
            .ToQueryString();

        Assert.Contains("VonBuerger", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheActiveTemplateLookup_TranslatesItsKindAndOrder()
    {
        // the one shape the template read path knows, and the automatic confirmation runs it on every submission
        var sql = _db.OeffentlicheVorlagen.AsNoTracking()
            .Where(v => v.Kind == PublicTemplateKind.HinweisEingang && v.IsActive)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Title)
            .Select(v => new { v.Id, v.Kind, v.Title, v.Text, v.IsActive, v.SortOrder })
            .ToQueryString();

        Assert.Contains("Reihenfolge", sql, StringComparison.Ordinal);
        Assert.Contains("IstAktiv", sql, StringComparison.Ordinal);
    }
}
