namespace NOOSE_Website.Infrastructure.Shares;

/// <summary>Process-wide in-memory broadcaster for live updates of the shares-inbox badge.</summary>
public sealed class SharesBroadcaster
{
    /// <summary>Fired when the open-inbox count may have changed.</summary>
    public event Action? Modified;

    public void Report() => Modified?.Invoke();
}
