namespace NOOSE_Website.Models.Enums;

/// <summary>Status eines Beförderungsantrags (Phase 5e). Beim Anlegen stets <see cref="Beantragt"/>; die
/// Entscheidung (Deputy Director+/Admin) setzt <see cref="Genehmigt"/> oder <see cref="Abgelehnt"/>.</summary>
public enum PromotionStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>Anzeigetexte für den Beförderungs-Status (UI-frei).</summary>
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
