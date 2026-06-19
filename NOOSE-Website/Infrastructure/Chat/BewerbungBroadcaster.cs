namespace NOOSE_Website.Infrastructure.Chat;

/// <summary>Process-wide in-memory broadcaster for live recruiting-thread updates (internal and applicant-facing).</summary>
public sealed class BewerbungBroadcaster
{
    /// <summary>Fired with the affected application id when its messages or status change.</summary>
    public event Action<string>? Modified;

    public void Report(string bewerbungId) => Modified?.Invoke(bewerbungId);
}
