using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>Anonymity is structural: what a public projection cannot carry, no page can render by accident.</summary>
/// <remarks>
/// The list of outward types is positive on purpose. An exemption list is granted per file and widens silently the
/// moment someone drops a new outward DTO into an exempt file; naming the types instead makes a new one a decision.
/// </remarks>
public class PublicWantedModelTests
{
    /// <summary>Every type an anonymous visitor's page is allowed to render.</summary>
    private static readonly Type[] Outward =
    [
        typeof(PublicWantedCard),
        typeof(PublicWantedDetail),
        typeof(PublicWantedBoard),
        typeof(PublicWantedPhoto),
        typeof(PublicPageLink),
        typeof(PublicPageView),
        typeof(PublicPageSnapshot),
        // the public shell renders these two as well: the nav tabs and the career page's requirement list
        typeof(PublicModuleState),
        typeof(NOOSE_Website.Models.Recruiting.CareerRequirement),
    ];

    /// <summary>Anything that names an agent, a record id or an internal identifier.</summary>
    private static readonly string[] Forbidden =
    [
        "PersonId", "FraktionId", "FactionId", "AgentId", "UserId",
        "Codename", "RealName", "Klarname", "Dienstgrad", "Rank", "BadgeNumber",
        "PublishedBy", "CreatedBy", "ModifiedBy", "DeletedBy",
    ];

    [Fact]
    public void OutwardModels_CarryNoInternalIdentifier()
    {
        var offenders = Outward
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, p.Name)))
            .Where(x => Forbidden.Any(f => x.Name.Contains(f, StringComparison.Ordinal)))
            .Select(x => $"{x.Type.Name}.{x.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Öffentliche Projektionen dürfen keinen internen Bezeichner tragen: " + string.Join(", ", offenders));
    }

    [Fact]
    public void OutwardModels_CarryNoBareRecordId()
    {
        var offenders = Outward
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, p.Name)))
            .Where(x => x.Name == "Id")
            .Select(x => $"{x.Type.Name}.{x.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Eine öffentliche Projektion wird über ihr Aktenzeichen adressiert, nie über die Zeilen-Id: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void NoOutwardModelExposesANumericThreatScore()
    {
        // the hazard level goes out, the raw 0-100 value does not: it is the output of the scoring algorithm and
        // watched over time it says when NOOSE acted
        var offenders = Outward
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, p.Name, p.PropertyType)))
            .Where(x => x.Name.Contains("Score", StringComparison.Ordinal)
                || (x.Name.Contains("Hazard", StringComparison.Ordinal) && x.PropertyType != typeof(HazardLevel)))
            .Select(x => $"{x.Type.Name}.{x.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Nach außen geht die Gefahrenstufe, nicht der Zahlenwert: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TheWantedDetail_CarriesTheHazardLevelAsAnEnum()
    {
        var property = typeof(PublicWantedDetail).GetProperty(nameof(PublicWantedDetail.HazardLevel));
        Assert.NotNull(property);
        Assert.Equal(typeof(HazardLevel), property!.PropertyType);
    }
}
