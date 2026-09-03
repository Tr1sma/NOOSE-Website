using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Public;

public class CaptureRulesTests
{
    [Fact]
    public void Anonymity_isRefusedOnThisPath()
    {
        // money needs a recipient, and the payout already refuses a hidden tipster. Refusing it up front is the
        // difference between "not offered" and "offered, then overruled"
        Assert.False(CaptureRules.AllowsAnonymity);
    }

    [Fact]
    public void OnlyAWantedNoticeCanBeAnsweredWithACapture()
    {
        Assert.True(CaptureRules.MayReport(PublicWantedKind.Fahndung));
        // nobody apprehends a car or a gun
        Assert.False(CaptureRules.MayReport(PublicWantedKind.Fahrzeug));
        Assert.False(CaptureRules.MayReport(PublicWantedKind.Waffe));
        // and nobody apprehends a missing person or a witness appeal either: finding someone who is missing is a
        // sighting, which belongs in the tip form
        Assert.False(CaptureRules.MayReport(PublicWantedKind.Vermisst));
        Assert.False(CaptureRules.MayReport(PublicWantedKind.Zeugenaufruf));
    }

    [Fact]
    public void EveryWantedKind_isDecided()
    {
        // the same shape as the other coverage guards: a new kind must be answered, not defaulted
        foreach (var kind in PublicWantedKindDisplay.All)
        {
            var decided = CaptureRules.MayReport(kind);
            Assert.Equal(kind == PublicWantedKind.Fahndung, decided);
        }
    }

    [Fact]
    public void UrgentMeansTheHolderIsStillHoldingTheirCatch()
    {
        Assert.True(CaptureRules.IsUrgent(TipKind.Ergreifung, TipHandover.Festgehalten));
        Assert.False(CaptureRules.IsUrgent(TipKind.Ergreifung, TipHandover.Uebergeben));
        Assert.False(CaptureRules.IsUrgent(TipKind.Ergreifung, null));
        // an observation is never urgent in this sense, whatever else is set on the row
        Assert.False(CaptureRules.IsUrgent(TipKind.Beobachtung, TipHandover.Festgehalten));
    }

    [Fact]
    public void IsCapture_readsTheKindAndNothingElse()
    {
        Assert.True(CaptureRules.IsCapture(TipKind.Ergreifung));
        Assert.False(CaptureRules.IsCapture(TipKind.Beobachtung));
    }

    [Fact]
    public void TheDailyQuota_isItsOwnNumberAndNotTheTipOne()
    {
        // deliberately not TipTrust.QuotaFor: a busy tipping day must not block a real handover, and the tier
        // rewards good tips rather than urgency. Two numbers here is the point, not drift
        Assert.NotEqual(TipRules.PerDay, CaptureRules.PerDay);
        Assert.True(CaptureRules.PerDay > 0);
        // and it is not scaled by the trust tier, so it stays below the lowest tip allowance
        Assert.True(CaptureRules.PerDay < TipTrust.QuotaFor(0));
    }

    [Fact]
    public void TheLocationBounds_fitTheColumn()
    {
        Assert.True(CaptureRules.MinLocationLength > 0);
        // the column is varchar(200); a longer bound would only fail on MySQL
        Assert.Equal(200, CaptureRules.MaxLocationLength);
        Assert.True(CaptureRules.MinLocationLength < CaptureRules.MaxLocationLength);
    }

    [Fact]
    public void TheLocationBound_matchesTheColumn()
    {
        // the bound is written twice on purpose - Data must not reach into Services - so a test holds the two
        // numbers together. MySQL truncates a longer value without a word
        using var ctx = new SqliteTestContext();
        using var db = ctx.NewContext();
        var configured = db.Model
            .FindEntityType(typeof(Hinweis))!
            .FindProperty(nameof(Hinweis.HandoverLocation))!
            .GetMaxLength();
        Assert.Equal(CaptureRules.MaxLocationLength, configured);
    }

    [Fact]
    public void EveryRefusalTextSaysSomething()
    {
        foreach (var text in new[]
                 {
                     CaptureRules.NoticeRequired, CaptureRules.KindRefused, CaptureRules.SelfRefused,
                     CaptureRules.AlreadyOpen, CaptureRules.LocationRequired,
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
        }
    }
}
