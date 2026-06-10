namespace NOOSE_Website.Models.Enums;

/// <summary>Status eines Beförderungsantrags (Phase 5e). Beim Anlegen stets <see cref="Beantragt"/>; die
/// Entscheidung (Deputy Director+/Admin) setzt <see cref="Genehmigt"/> oder <see cref="Abgelehnt"/>.</summary>
public enum BefoerderungStatus
{
    Beantragt = 0,
    Genehmigt = 1,
    Abgelehnt = 2,
}

/// <summary>Anzeigetexte für den Beförderungs-Status (UI-frei).</summary>
public static class BefoerderungStatusAnzeige
{
    public static string Name(BefoerderungStatus status) => status switch
    {
        BefoerderungStatus.Beantragt => "Beantragt",
        BefoerderungStatus.Genehmigt => "Genehmigt",
        BefoerderungStatus.Abgelehnt => "Abgelehnt",
        _ => "—",
    };
}
