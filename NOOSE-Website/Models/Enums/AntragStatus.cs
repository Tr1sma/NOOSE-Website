namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Status eines Antrags im Posteingang-Workflow (Phase 5). Beim Anlegen stets <see cref="Beantragt"/>;
/// die Entscheidung (Senior Special Agent+/Admin) setzt <see cref="Genehmigt"/> oder <see cref="Abgelehnt"/>.
/// </summary>
public enum AntragStatus
{
    Beantragt = 0,
    Genehmigt = 1,
    Abgelehnt = 2,
}

/// <summary>Anzeigetexte für den Antrags-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class AntragStatusAnzeige
{
    public static string Name(AntragStatus status) => status switch
    {
        AntragStatus.Beantragt => "Beantragt",
        AntragStatus.Genehmigt => "Genehmigt",
        AntragStatus.Abgelehnt => "Abgelehnt",
        _ => "—",
    };

    public static readonly IReadOnlyList<AntragStatus> Alle = new[]
    {
        AntragStatus.Beantragt,
        AntragStatus.Genehmigt,
        AntragStatus.Abgelehnt,
    };
}
