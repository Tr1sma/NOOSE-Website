namespace NOOSE_Website.Models.Enums;

/// <summary>Inbox request status.</summary>
public enum RequestStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>Display labels.</summary>
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
