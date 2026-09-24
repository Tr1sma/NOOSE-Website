using NOOSE_Website.Models.Factions;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>When two route entries are the same route.</summary>
public sealed class DrugRouteRulesTests
{
    [Theory]
    [InlineData("Kokain Nord", "kokain nord")]
    [InlineData("  Kokain   Nord ", "KOKAIN NORD")]
    [InlineData("Kokain\tNord", "Kokain Nord")]
    [InlineData("Meth Süd", "METH SÜD")]
    public void Case_and_spacing_do_not_count(string a, string b)
        => Assert.Equal(DrugRouteRules.Key(a), DrugRouteRules.Key(b));

    [Fact]
    public void A_typo_stays_a_different_route()
        => Assert.NotEqual(DrugRouteRules.Key("Kokain-Nord"), DrugRouteRules.Key("Kokain Nord"));

    [Fact]
    public void Empty_is_no_route()
    {
        Assert.Equal(string.Empty, DrugRouteRules.Key(null));
        Assert.Equal(string.Empty, DrugRouteRules.Key("   "));
    }

    [Fact]
    public void A_list_keeps_each_route_once_and_the_first_spelling()
    {
        var routes = DrugRouteRules.Distinct(
        [
            new StockInput { Designation = "Kokain Nord", Quantity = "Hafen" },
            new StockInput { Designation = "" },
            new StockInput { Designation = " kokain  nord", Quantity = "Flughafen" },
            new StockInput { Designation = "Meth Süd" },
        ]);

        Assert.Equal(["Kokain Nord", "Meth Süd"], routes.Select(r => r.Designation));
        Assert.Equal("Hafen", routes[0].Quantity);
    }
}
