namespace NOOSE_Website.Models.Enums;

/// <summary>Which side of the house a rule watches.</summary>
public enum CounterIntelPartnerScope
{
    Any = 0,
    InternalOnly = 1,
    PartnersOnly = 2,
}

/// <summary>Display labels.</summary>
public static class CounterIntelPartnerScopeDisplay
{
    public static string Name(CounterIntelPartnerScope scope) => scope switch
    {
        CounterIntelPartnerScope.Any => "Alle Agenten",
        CounterIntelPartnerScope.InternalOnly => "nur interne Agenten",
        CounterIntelPartnerScope.PartnersOnly => "nur Partnerbehörden",
        _ => scope.ToString(),
    };

    public static readonly IReadOnlyList<CounterIntelPartnerScope> All =
    [
        CounterIntelPartnerScope.Any,
        CounterIntelPartnerScope.InternalOnly,
        CounterIntelPartnerScope.PartnersOnly,
    ];
}
