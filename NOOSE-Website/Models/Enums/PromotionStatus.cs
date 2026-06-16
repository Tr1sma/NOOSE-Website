namespace NOOSE_Website.Models.Enums;

/// <summary>Promotion request status.</summary>
public enum PromotionStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>Display labels.</summary>
public static class PromotionStatusDisplay
{
    public static string Name(PromotionStatus status) => status switch
    {
        PromotionStatus.Requested => "Beantragt",
        PromotionStatus.Approved => "Genehmigt",
        PromotionStatus.Rejected => "Abgelehnt",
        _ => "—",
    };
}
