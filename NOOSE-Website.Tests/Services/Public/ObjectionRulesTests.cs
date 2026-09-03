using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The state machine of an objection, and the quota that lives beside it.</summary>
public class ObjectionRulesTests
{
    // ---- only the named person may object ----

    [Theory]
    [InlineData("Max", "Mustermann", "Max Mustermann")]
    [InlineData("max", "mustermann", "MAX MUSTERMANN")]
    [InlineData("Max", "Mustermann", "  Max   Mustermann  ")]
    [InlineData("Jörg", "Müller", "Joerg Mueller")]
    [InlineData("Joerg", "Mueller", "Jörg Müller")]
    [InlineData("Hans", "Weiß", "Hans Weiss")]
    public void SpellingNoiseDoesNotBlockTheNamedPerson(string first, string last, string displayName)
        => Assert.True(ObjectionRules.NamesCitizen(first, last, displayName));

    [Theory]
    [InlineData("Anna", "Meier", "Anna Meyer")]
    [InlineData("Max", "Mustermann", "Moritz Mustermann")]
    [InlineData("Max", "Mustermann", "Mustermann")]
    [InlineData("", "Mustermann", "Max Mustermann")]
    [InlineData("Max", "", "Max Mustermann")]
    public void ADifferentNameIsADifferentPerson(string first, string last, string displayName)
        => Assert.False(ObjectionRules.NamesCitizen(first, last, displayName));

    [Fact]
    public void AnEmptyProfileNeverMatches()
    {
        // otherwise a nameless account would match a nameless notice and pass the gate
        Assert.False(ObjectionRules.NamesCitizen(null, null, null));
        Assert.False(ObjectionRules.NamesCitizen("  ", "  ", "  "));
    }

    [Fact]
    public void OnlyAPersonNoticeCanBeDisputed()
    {
        Assert.True(ObjectionRules.MayObject(PublicWantedKind.Fahndung));
        Assert.False(ObjectionRules.MayObject(PublicWantedKind.Fahrzeug));
        Assert.False(ObjectionRules.MayObject(PublicWantedKind.Waffe));
    }

    [Fact]
    public void EveryNoticeKindIsDecided()
    {
        // a new kind must be a decision, not an accident of the item predicate
        foreach (var kind in Enum.GetValues<PublicWantedKind>())
        {
            Assert.Equal(!WantedKinds.IsItem(kind), ObjectionRules.MayObject(kind));
        }
    }

    [Theory]
    [InlineData(ObjectionStatus.Neu, true)]
    [InlineData(ObjectionStatus.InPruefung, true)]
    [InlineData(ObjectionStatus.Angenommen, false)]
    [InlineData(ObjectionStatus.Abgelehnt, false)]
    public void IsOpen_SplitsPendingFromDecided(ObjectionStatus status, bool expected)
        => Assert.Equal(expected, ObjectionRules.IsOpen(status));

    [Fact]
    public void TheQueryTwin_AgreesWithTheInMemoryRule()
    {
        var open = ObjectionRules.OpenRows.Compile();
        foreach (var status in ObjectionStatusDisplay.All)
        {
            Assert.Equal(ObjectionRules.IsOpen(status),
                open(new NOOSE_Website.Data.Entities.Public.FahndungEinspruch { Status = status }));
        }
    }

    [Fact]
    public void NoStatus_TransitionsToItself()
    {
        foreach (var status in ObjectionStatusDisplay.All)
        {
            Assert.False(ObjectionRules.IsTransitionAllowed(status, status));
        }
    }

    [Fact]
    public void NeuIsNeverReachableAgain()
    {
        foreach (var from in ObjectionStatusDisplay.All)
        {
            Assert.False(ObjectionRules.IsTransitionAllowed(from, ObjectionStatus.Neu));
        }
    }

    [Fact]
    public void ADecisionMayGoBackIntoReview()
    {
        // new evidence arrives after a rejection often enough; the alternative is the citizen filing again and
        // spending a quota slot on it
        Assert.True(ObjectionRules.IsTransitionAllowed(ObjectionStatus.Abgelehnt, ObjectionStatus.InPruefung));
        Assert.True(ObjectionRules.IsTransitionAllowed(ObjectionStatus.Angenommen, ObjectionStatus.InPruefung));
        Assert.False(ObjectionRules.IsTransitionAllowed(ObjectionStatus.Abgelehnt, ObjectionStatus.Angenommen));
    }

    [Fact]
    public void AllowedTargets_MatchTheTransitionRule()
    {
        foreach (var from in ObjectionStatusDisplay.All)
        {
            var targets = ObjectionRules.AllowedTargets(from);
            Assert.All(ObjectionStatusDisplay.All, to =>
                Assert.Equal(ObjectionRules.IsTransitionAllowed(from, to), targets.Contains(to)));
        }
    }

    [Fact]
    public void TheNoteCap_LeavesRoomInsideTheObjectionItself()
        => Assert.True(ObjectionRules.MaxNoteLength < ObjectionRules.MaxLength);
}
