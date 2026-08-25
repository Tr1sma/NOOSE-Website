using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The one line between a notice about a person and one about a thing.</summary>
public class WantedKindsTests
{
    [Theory]
    [InlineData(PublicWantedKind.Fahrzeug, true)]
    [InlineData(PublicWantedKind.Waffe, true)]
    [InlineData(PublicWantedKind.Fahndung, false)]
    // both are statements about a human, so they sit on the person side even though nobody issues them yet
    [InlineData(PublicWantedKind.Vermisst, false)]
    [InlineData(PublicWantedKind.Zeugenaufruf, false)]
    [InlineData((PublicWantedKind)99, false)]
    public void IsItem_DrawsTheLine(PublicWantedKind kind, bool expected)
        => Assert.Equal(expected, WantedKinds.IsItem(kind));

    [Fact]
    public void TheQueryTwins_AgreeWithTheInMemoryRule()
    {
        var item = WantedKinds.ItemRows.Compile();
        var person = WantedKinds.PersonRows.Compile();

        foreach (var kind in PublicWantedKindDisplay.All)
        {
            var row = new OeffentlicheFahndung { Kind = kind };
            Assert.Equal(WantedKinds.IsItem(kind), item(row));
            // written out as the complement, so a third family cannot fall through both filters unnoticed
            Assert.NotEqual(item(row), person(row));
        }
    }
}
