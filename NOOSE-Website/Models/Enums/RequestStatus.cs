namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Status eines Antrags im Posteingang-Workflow (Phase 5). Beim Anlegen stets <see cref="Beantragt"/>;
/// die Entscheidung (Senior Special Agent+/Admin) setzt <see cref="Genehmigt"/> oder <see cref="Abgelehnt"/>.
/// </summary>
public enum RequestStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>Anzeigetexte für den Antrags-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class RequestStatusDisplay
{
    public static string Name(RequestStatus status) => status switch
    {
        RequestStatus.Requested => "Beantragt",
        RequestStatus.Approved => "Genehmigt",
        RequestStatus.Rejected => "Abgelehnt",
        _ => "—",
    };

    public static readonly IReadOnlyList<RequestStatus> All = new[]
    {
        RequestStatus.Requested,
        RequestStatus.Approved,
        RequestStatus.Rejected,
    };
}
