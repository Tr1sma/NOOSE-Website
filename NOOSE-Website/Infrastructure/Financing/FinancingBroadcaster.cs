namespace NOOSE_Website.Infrastructure.Financing;

/// <summary>Process-wide in-memory broadcaster for the funding badge; global because every decider recounts when any agent files a request.</summary>
public sealed class FinancingBroadcaster
{
    /// <summary>Fired when the number of requests awaiting a decision may have changed.</summary>
    public event Action? Modified;

    public void Report() => Modified?.Invoke();
}
