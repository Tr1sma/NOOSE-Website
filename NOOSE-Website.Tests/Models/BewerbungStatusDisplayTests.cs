using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Models;

/// <summary>The applicant-facing projections of <see cref="BewerbungStatusDisplay"/>.</summary>
public class BewerbungStatusDisplayTests
{
    [Theory]
    [InlineData(BewerbungStatus.ImTest)]
    [InlineData(BewerbungStatus.ImVorstellungsgespraech)]
    public void ApplicantName_MergesTestAndInterview(BewerbungStatus status)
        => Assert.Equal("Auswahlverfahren", BewerbungStatusDisplay.ApplicantName(status));

    [Theory]
    [InlineData(BewerbungStatus.Eingereicht, "Eingereicht")]
    [InlineData(BewerbungStatus.InSicherheitspruefung, "Sicherheitsüberprüfung")]
    [InlineData(BewerbungStatus.Angenommen, "Angenommen")]
    [InlineData(BewerbungStatus.Abgelehnt, "Abgelehnt")]
    [InlineData(BewerbungStatus.Geschlossen, "Geschlossen")]
    public void ApplicantName_LeavesEveryOtherStageAlone(BewerbungStatus status, string expected)
    {
        Assert.Equal(expected, BewerbungStatusDisplay.ApplicantName(status));
        Assert.Equal(BewerbungStatusDisplay.Name(status), BewerbungStatusDisplay.ApplicantName(status));
    }

    [Fact]
    public void Name_StillDistinguishesTheStages_ForInternalReaders()
    {
        Assert.Equal("Test", BewerbungStatusDisplay.Name(BewerbungStatus.ImTest));
        Assert.Equal("Vorstellungsgespräch", BewerbungStatusDisplay.Name(BewerbungStatus.ImVorstellungsgespraech));
    }

    [Fact]
    public void ApplicantStep_CollapsesInterviewOntoTest()
    {
        Assert.Equal(BewerbungStatus.ImTest, BewerbungStatusDisplay.ApplicantStep(BewerbungStatus.ImVorstellungsgespraech));
        Assert.Equal(BewerbungStatus.ImTest, BewerbungStatusDisplay.ApplicantStep(BewerbungStatus.ImTest));
    }

    [Theory]
    [InlineData(BewerbungStatus.Eingereicht)]
    [InlineData(BewerbungStatus.InSicherheitspruefung)]
    [InlineData(BewerbungStatus.Angenommen)]
    [InlineData(BewerbungStatus.Abgelehnt)]
    [InlineData(BewerbungStatus.Geschlossen)]
    public void ApplicantStep_IsIdentityElsewhere(BewerbungStatus status)
        => Assert.Equal(status, BewerbungStatusDisplay.ApplicantStep(status));

    [Fact]
    public void ApplicantStep_MakesTheChipColourIdenticalForBothStages()
    {
        // a different colour would leak the advancement the label hides
        Assert.Equal(
            BewerbungStatusDisplay.ChipColor(BewerbungStatusDisplay.ApplicantStep(BewerbungStatus.ImTest)),
            BewerbungStatusDisplay.ChipColor(BewerbungStatusDisplay.ApplicantStep(BewerbungStatus.ImVorstellungsgespraech)));
    }
}
