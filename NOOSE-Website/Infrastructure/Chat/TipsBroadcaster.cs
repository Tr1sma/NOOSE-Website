namespace NOOSE_Website.Infrastructure.Chat;

/// <summary>Process-wide in-memory broadcaster for live tip-thread updates (internal and citizen-facing).</summary>
public sealed class TipsBroadcaster
{
    /// <summary>Fired with the affected tip id when its messages or status change.</summary>
    public event Action<string>? Modified;

    public void Report(string tipId) => Modified?.Invoke(tipId);
}
