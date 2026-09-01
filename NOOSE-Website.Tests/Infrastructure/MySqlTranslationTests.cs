using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
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

    [Fact]
    public void TheOrganisationSnapshot_Translates()
    {
        // the one shape an anonymous visitor triggers on /organisationen and on both hazard rankings
        var sql = _db.OeffentlicheFraktionsprofile.AsNoTracking()
            .Where(p => p.Status == PublicProfileStatus.Veroeffentlicht)
            .OrderByDescending(p => p.PublishedAt)
            .Select(p => new
            {
                p.FactionId,
                p.DisplayName,
                p.Standing,
                p.PublicHazardLevel,
                p.DescriptionHtml,
                p.PublishedAt,
            })
            .ToQueryString();

        Assert.Contains("Einordnung", sql, StringComparison.Ordinal);
        Assert.Contains("OeffentlicheGefahrenstufe", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSuppressionBelt_TranslatesOverTheFactionFiles()
    {
        // the second query the belt is: a subquery using IgnoreQueryFilters would strip the soft-delete filter from
        // the outer set as well, so this shape is the one that must compile
        var ids = new List<string> { "f1" };
        var sql = _db.Factions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id) && !f.IsDeleted
                && !f.IsClassified && !f.IsTRUClassified && !f.IsHRBClassified)
            .Select(f => f.Id)
            .ToQueryString();

        Assert.Contains("IstVerschlusssache", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheManagementProjection_TranslatesOverBothNavigations()
    {
        // the faction case number and the publisher's codename come through two optional navigations; on a deleted
        // faction the join has to yield null rather than fail
        var sql = _db.OeffentlicheFraktionsprofile.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, Akte = p.Faction!.CaseNumber, Von = p.PublishedBy!.Codename })
            .ToQueryString();

        Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
    }

    /// <summary>Mirrors the state set in which a notice occupies its subject.</summary>
    private static readonly PublicWantedStatus[] LiveStates =
    [
        PublicWantedStatus.Entwurf,
        PublicWantedStatus.Beantragt,
        PublicWantedStatus.Veroeffentlicht,
    ];

    [Fact]
    public void TheItemDuplicateCheck_TranslatesOverKindNameAndState()
    {
        // deduplicated on the text rather than on the profile row: the file's profile children are replaced
        // wholesale on every save, so their ids are worthless a moment later
        var sql = _db.OeffentlicheFahndungen
            .Where(f => f.PersonId == "p1" && f.Kind == PublicWantedKind.Fahrzeug && f.DisplayName == "4XYZ123"
                && LiveStates.Contains(f.Status))
            .ToQueryString();

        Assert.Contains("AnzeigeName", sql, StringComparison.Ordinal);
        Assert.Contains("IN (", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheItemKindFilter_TranslatesAsANamedExpression()
    {
        // the kind axis is named rather than spelled out at each site; it has to survive the translation as one
        var sql = _db.OeffentlicheFahndungen
            .AsNoTracking()
            .Where(WantedKinds.ItemRows)
            .Where(f => f.PersonId == "p1")
            .Select(f => new { f.Kind, f.DisplayName })
            .ToQueryString();

        Assert.Contains("Art", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheObjectionDeskProjection_TranslatesOverAllThreeNavigations()
    {
        // notice, citizen profile and decider come through three optional navigations; on a deleted case the last
        // one has to yield null rather than fail
        var sql = _db.FahndungEinsprueche.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                Fahndung = e.Wanted!.CaseNumber,
                Buerger = e.CitizenProfile!.FirstName + " " + e.CitizenProfile.LastName,
                Von = e.DecidedBy!.Codename,
                Vorgang = e.LinkedCase!.CaseNumber,
            })
            .ToQueryString();

        Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
        Assert.Contains("Aktenzeichen", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheObjectionCaps_TranslateIncludingTheNamedOpenPredicate()
    {
        // the per-notice cap names the open predicate rather than spelling it out; it has to survive translation
        var open = _db.FahndungEinsprueche
            .Where(e => e.CitizenProfileId == "b1" && e.WantedId == "f1")
            .Where(ObjectionRules.OpenRows)
            .ToQueryString();
        Assert.Contains("Status", open, StringComparison.Ordinal);

        // and the daily cap widens past the soft-delete filter on purpose. Counted rather than searched: the
        // column is in the SELECT list either way, so only the extra occurrence in the WHERE tells them apart.
        var daily = _db.FahndungEinsprueche.IgnoreQueryFilters()
            .Where(e => e.CitizenProfileId == "b1")
            .ToQueryString();
        var filtered = _db.FahndungEinsprueche
            .Where(e => e.CitizenProfileId == "b1")
            .ToQueryString();
        Assert.True(Occurrences(filtered, "IstGeloescht") > Occurrences(daily, "IstGeloescht"),
            "IgnoreQueryFilters muss den Soft-Delete-Filter aus dem WHERE nehmen.");
    }

    [Fact]
    public void ThePressHubProjection_TranslatesWithItsLimitAndNullFilter()
    {
        // the public hub reads a capped, ordered projection over a nullable case number; the filter is what makes
        // the dictionary key non-null rather than an assumption about it
        var sql = _db.Pressemitteilungen
            .AsNoTracking()
            .Where(x => x.Status == PressReleaseStatus.Veroeffentlicht && x.CaseNumber != null)
            .OrderByDescending(x => x.PublishedAt)
            .Take(50)
            .Select(x => new { x.CaseNumber, x.ContentTitle, x.ContentTeaser, x.ContentHtml, x.PublishedAt })
            .ToQueryString();

        Assert.Contains("Aktenzeichen", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.Contains("IS NOT NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePressPanelProjection_TranslatesTheDraftComparisonAndThePublisherName()
    {
        // the panel compares three snapshot columns against their working copies and reaches the publisher over an
        // optional navigation
        var sql = _db.Pressemitteilungen
            .AsNoTracking()
            .OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                Differs = (x.DraftHtml ?? string.Empty) != (x.ContentHtml ?? string.Empty)
                    || x.Title != (x.ContentTitle ?? string.Empty)
                    || x.Teaser != (x.ContentTeaser ?? string.Empty),
                Publisher = x.PublishedBy!.Codename,
            })
            .ToQueryString();

        Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
        Assert.Contains("EntwurfHtml", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWarningHubQuery_TranslatesIncludingItsExpiryFilter()
    {
        var now = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
        var sql = _db.OeffentlicheWarnungen
            .AsNoTracking()
            .Where(w => w.Status == PublicWarningStatus.Veroeffentlicht && (w.ValidUntil == null || w.ValidUntil > now))
            .OrderByDescending(w => w.PublishedAt)
            .Take(20)
            .Select(w => new { Title = w.ContentTitle ?? string.Empty, Html = w.ContentHtml ?? string.Empty, w.ValidUntil, w.PublishedAt })
            .ToQueryString();

        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.Contains("GueltigBis", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWarningPanelQuery_TranslatesIncludingTheExpiredFlagAndTheOptionalPublisher()
    {
        var now = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
        var sql = _db.OeffentlicheWarnungen
            .AsNoTracking()
            .OrderByDescending(w => w.PublishedAt ?? w.CreatedAt)
            .Select(w => new
            {
                w.Id,
                Differs = (w.DraftHtml ?? string.Empty) != (w.ContentHtml ?? string.Empty)
                    || w.Title != (w.ContentTitle ?? string.Empty),
                Expired = w.ValidUntil != null && w.ValidUntil <= now,
                Publisher = w.PublishedBy!.Codename,
            })
            .ToQueryString();

        Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
        Assert.Contains("EntwurfHtml", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportHubQuery_TranslatesIncludingItsPeriodOrdering()
    {
        var sql = _db.OeffentlicheLageberichte
            .AsNoTracking()
            .Where(r => r.Status == PublicReportStatus.Veroeffentlicht)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Take(24)
            .Select(r => new PublicReportView(r.Year, r.Month, r.ContentTitle ?? string.Empty,
                r.ContentHtml ?? string.Empty, r.PublishedAt))
            .ToQueryString();

        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.Contains("Jahr", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportPanelQuery_TranslatesBothOptionalNavigationsAsLeftJoins()
    {
        var sql = _db.OeffentlicheLageberichte
            .AsNoTracking()
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => new
            {
                r.Id,
                Differs = (r.DraftHtml ?? string.Empty) != (r.ContentHtml ?? string.Empty)
                    || r.Title != (r.ContentTitle ?? string.Empty),
                Publisher = r.PublishedBy!.Codename,
                HasAnchor = r.SituationReport != null,
            })
            .ToQueryString();

        // the anchor is optional so the row survives a deleted monthly report; a required navigation would be INNER
        Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
        Assert.Contains("EntwurfHtml", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportAnchorQuery_TranslatesItsNotExistsSubquery()
    {
        // reads the internal archive minus the months that already have a public text; SQLite takes the shape, and
        // "could not be translated" would only show up on the settings page against MySQL
        var sql = _db.SituationReports
            .AsNoTracking()
            .Where(l => !_db.OeffentlicheLageberichte.Any(r => r.SituationReportId == l.Id))
            .OrderByDescending(l => l.Year).ThenByDescending(l => l.Month)
            .Select(l => new PublicReportAnchor(l.Id, l.Year, l.Month, l.Title))
            .ToQueryString();

        Assert.Contains("Lageberichte", sql, StringComparison.Ordinal);
        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSituationQuery_TranslatesItsKeySetLookup()
    {
        // four rows out of the shared settings table; an IN list is trivial until it is not, and this one hangs
        // behind an anonymous page
        string[] keys =
        [
            SystemSettingKeys.PublicSituationLevel, SystemSettingKeys.PublicSituationNote,
            SystemSettingKeys.PublicSituationSince, SystemSettingKeys.PublicSituationPrevious,
        ];

        var sql = _db.SystemSettings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .Select(s => new { s.Key, s.Value })
            .ToQueryString();

        Assert.Contains("Schluessel", sql, StringComparison.Ordinal);
        Assert.Contains("GefahrenlageStufe", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportTrashQuery_TranslatesWithoutTheSoftDeleteFilter()
    {
        var sql = _db.OeffentlicheLageberichte
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.IsDeleted)
            .OrderByDescending(r => r.DeletedAt)
            .ToQueryString();

        Assert.Contains("IstGeloescht", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePublicLawQuery_TranslatesIncludingItsProjectionIntoARecord()
    {
        // the projection constructs a record inside the query, which is the shape the grouping read path depends on
        var sql = _db.Laws
            .AsNoTracking()
            .Where(l => l.IsPublic)
            .OrderBy(l => l.LawBook).ThenBy(l => l.Paragraph).ThenBy(l => l.Title)
            .Select(l => new { l.LawBook, Entry = new PublicLawEntry(l.Paragraph, l.Title, l.Text, l.Sentence) })
            .ToQueryString();

        Assert.Contains("IstOeffentlich", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCapturedCountQuery_TranslatesAndStaysUncapped()
    {
        var sql = _db.OeffentlicheFahndungen
            .AsNoTracking()
            .Where(f => f.Status == PublicWantedStatus.Gefasst && f.CaseNumber != null && f.CapturedAt != null)
            .Select(f => new { CaseNumber = f.CaseNumber!, f.PersonId, f.Kind })
            .ToQueryString();

        Assert.Contains("Aktenzeichen", sql, StringComparison.Ordinal);
        // the point of counting apart from the archive list: this one carries no display limit
        Assert.DoesNotContain("LIMIT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePublicTipCounts_TranslateTheirSharedPredicates()
    {
        // CountAsync(predicate) compiles to this shape; the predicates come from TipRules rather than being written
        // again in the statistics service, so it is those that have to translate
        var confirmed = _db.Hinweise.AsNoTracking().Where(TipRules.ConfirmedRows).ToQueryString();
        var captures = _db.Hinweise.AsNoTracking().Where(TipRules.CaptureRows).ToQueryString();

        Assert.Contains("Status", confirmed, StringComparison.Ordinal);
        Assert.Contains("Status", captures, StringComparison.Ordinal);
        // confirmed is the wider set, so its WHERE cannot be the narrower one
        Assert.NotEqual(confirmed, captures);
    }

    [Fact]
    public void ThePaidRewardSum_TranslatesWithoutASoftDeleteFilter()
    {
        var sql = _db.HinweisBelohnungen.AsNoTracking().Select(r => r.Amount).ToQueryString();

        Assert.Contains("Betrag", sql, StringComparison.Ordinal);
        // money history is append-only, so there is no filter here that a later change could weaken
        Assert.DoesNotContain("IstGeloescht", sql, StringComparison.Ordinal);
    }

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }
        return count;
    }
}
