namespace NOOSE_Website.Models.Enums;

/// <summary>Operational status of a confidential informant.</summary>
public enum InformantStatus
{
    Active = 0,
    Inactive = 1,
    /// <summary>Cover blown — no longer usable.</summary>
    Burned = 2,
}

/// <summary>Reliability grade (A = most reliable … F = unreliable).</summary>
public enum InformantReliability
{
    A = 0, B = 1, C = 2, D = 3, E = 4, F = 5,
}

/// <summary>Display labels for informant enums.</summary>
public static class InformantEnumDisplay
{
    public static string Status(InformantStatus status) => status switch
    {
        InformantStatus.Active => "Aktiv",
        InformantStatus.Inactive => "Inaktiv",
        InformantStatus.Burned => "Verbrannt",
        _ => status.ToString(),
    };

    public static string Reliability(InformantReliability grade) => $"Stufe {grade}";
}
